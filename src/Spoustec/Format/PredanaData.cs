using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Spoustec.Json;

namespace Spoustec.Format
{
    /// <summary>vstupni data nelze pouzit; Kod je navratovy kod programu (Beh.KodVstup, Beh.KodFormat)</summary>
    public class ChybaVstupuException : Exception
    {
        public int Kod { get; private set; }

        public ChybaVstupuException(int kod, string zprava, Exception vnitrni = null) : base(zprava, vnitrni)
        {
            Kod = kod;
        }
    }

    /// <summary>
    /// Data predana Fowlerem (fmt "fowler-exam", popis v docs/format.md). Spoustec z nich vezme jen identifikaci
    /// vysetreni pro odpoved (pid, rc, exam.time presne tak, jak byly predany), odebere blok patient a zbytek preda
    /// skriptu beze zmeny jako text JSON (cisla i klice, ktere spoustec nezna, zustanou, jak byly).
    /// Obsah casti vysetreni nekontroluje, to je vec skriptu.
    /// </summary>
    public sealed class PredanaData
    {
        public const string Fmt = "fowler-exam";
        public const int Verze = 1;     //nejvyssi verze formatu, kterou spoustec zna
        public static readonly string[] ZnameCasti = { "audio", "tymp", "speech" };

        /// <summary>nazev casti pro uzivatele: "tónová audiometrie", "tympanometrie", "slovní audiometrie"</summary>
        public static string PopisCasti(string cast)
        {
            switch (cast)
            {
                case "audio": return "tónová audiometrie";
                case "tymp": return "tympanometrie";
                case "speech": return "slovní audiometrie";
                default: return cast;
            }
        }

        public string Pid { get; private set; }             //null = nebyl predan
        public string Rc { get; private set; }              //null = nebylo predano
        public string CasVysetreni { get; private set; }    //exam.time jako text, beze zmeny
        public string App { get; private set; }             //program, ktery data vytvoril (do logu)
        public List<string> Casti { get; private set; }     //predane casti vysetreni (audio, tymp, speech)
        public string DataProSkript { get; private set; }   //JSON bez bloku patient

        PredanaData()
        {
            Casti = new List<string>();
        }

        public static PredanaData PrectiSoubor(string soubor)
        {
            string text;
            try
            {
                text = JsonCteni.PrectiUtf8(soubor);
            }
            catch (ChybaJsonException ex)
            {
                throw new ChybaVstupuException(Beh.KodVstup, ex.Message, ex);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                throw new ChybaVstupuException(Beh.KodVstup, "soubor nelze přečíst: " + ex.Message, ex);
            }
            return PrectiText(text);
        }

        public static PredanaData PrectiText(string text)
        {
            JsonHodnota koren;
            try
            {
                koren = JsonCteni.Precti(text);
            }
            catch (ChybaJsonException ex)
            {
                throw new ChybaVstupuException(Beh.KodVstup, ex.Message, ex);
            }
            if (koren.Typ != TypJson.Objekt)
                throw new ChybaVstupuException(Beh.KodVstup, "data nejsou objekt JSON");

            JsonHodnota fmt = koren.Polozka("fmt");
            if (fmt == null || fmt.Typ != TypJson.Text || fmt.Text != Fmt)
            {
                string popis = (fmt == null) ? "chybí" : (fmt.Typ == TypJson.Text) ? "\"" + Zkrat(fmt.Text, 40) + "\"" : "není text";
                throw new ChybaVstupuException(Beh.KodFormat, $"data nejsou ve formátu {Fmt} (fmt: {popis})");
            }
            int verze = KladneCeleCislo(koren.Polozka("v"));
            if (verze < 1)
                throw new ChybaVstupuException(Beh.KodVstup, "chybí verze formátu (v) nebo je neplatná");
            if (verze > Verze)
                throw new ChybaVstupuException(Beh.KodFormat, $"data mají novější verzi formátu ({verze}), než spouštěč zná ({Verze})");

            PredanaData data = new PredanaData();
            JsonHodnota app = koren.Polozka("app");
            data.App = (app != null && app.Typ == TypJson.Text) ? Zkrat(app.Text, 100) : "?";

            JsonHodnota pacient = koren.Polozka("patient");
            if (pacient != null && pacient.Typ != TypJson.Objekt)
                throw new ChybaVstupuException(Beh.KodVstup, "patient: očekáván objekt");
            data.Pid = TextPacienta(pacient, "pid");
            data.Rc = TextPacienta(pacient, "rc");

            JsonHodnota exam = koren.Polozka("exam");
            JsonHodnota cas = (exam != null) ? exam.Polozka("time") : null;
            if (exam == null || exam.Typ != TypJson.Objekt || cas == null || cas.Typ != TypJson.Text || cas.Text.Length == 0)
                throw new ChybaVstupuException(Beh.KodVstup, "chybí čas vyšetření (exam.time)");
            data.CasVysetreni = cas.Text;

            foreach (string nazev in ZnameCasti)
            {
                JsonHodnota cast = koren.Polozka(nazev);
                if (cast == null)
                    continue;
                if (cast.Typ != TypJson.Objekt)
                    throw new ChybaVstupuException(Beh.KodVstup, $"{nazev}: očekáván objekt");
                data.Casti.Add(nazev);
            }
            if (data.Casti.Count == 0)
                throw new ChybaVstupuException(Beh.KodVstup, "data neobsahují žádnou část vyšetření (audio, tymp, speech)");

            //skript osobni udaje nedostane
            koren.Odeber("patient");
            data.DataProSkript = JsonZapis.Zapis(koren, false);
            return data;
        }

        static string TextPacienta(JsonHodnota pacient, string klic)
        {
            JsonHodnota h = (pacient != null) ? pacient.Polozka(klic) : null;
            if (h == null)
                return null;
            if (h.Typ != TypJson.Text)
                throw new ChybaVstupuException(Beh.KodVstup, $"patient.{klic}: očekáván text");
            return h.Text;
        }

        //kladne cele cislo z JSON (velke jako int.MaxValue), 0 = chybi nebo neni kladne cele cislo
        static int KladneCeleCislo(JsonHodnota h)
        {
            double cislo;
            if (h == null || h.Typ != TypJson.Cislo ||
                !double.TryParse(h.Cislo, NumberStyles.Float, CultureInfo.InvariantCulture, out cislo) ||
                cislo != Math.Floor(cislo) || cislo < 1)
                return 0;
            return (cislo > int.MaxValue) ? int.MaxValue : (int)cislo;
        }

        static string Zkrat(string text, int delka)
        {
            return (text.Length <= delka) ? text : text.Substring(0, delka) + "…";
        }
    }
}
