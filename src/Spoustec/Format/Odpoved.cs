using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Spoustec.Json;

namespace Spoustec.Format
{
    /// <summary>
    /// Odpoved pro Fowler (fmt "fowler-eval", docs/format.md): hlavicka, identifikace vysetreni presne tak, jak byla
    /// predana (pid, rc, exam.time), platne casti po kontrole a poznamka. Soubor UTF-8 bez BOM, nejvyse 1 MB.
    /// </summary>
    public static class Odpoved
    {
        public const string Fmt = "fowler-eval";
        public const int Verze = 1;
        public const int MaxZnakuPoznamky = 2000;   //Fowler zobrazi jen prvnich 2000 znaku
        public const int MaxVelikost = 1024 * 1024;
        public const string NadpisKontroly = "Kontrola spouštěče:";

        public static JsonHodnota Sestav(PredanaData data, JsonHodnota casti, string poznamka, string app, DateTime vytvoreno)
        {
            JsonHodnota o = JsonHodnota.NovyObjekt();
            o.Nastav("fmt", JsonHodnota.Retezec(Fmt));
            o.Nastav("v", JsonHodnota.CisloInt(Verze));
            o.Nastav("app", JsonHodnota.Retezec(app));
            o.Nastav("created", JsonHodnota.Retezec(vytvoreno.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)));
            //podle identifikace Fowler pozna, ze odpoved patri k predanemu vysetreni
            JsonHodnota pacient = JsonHodnota.NovyObjekt();
            if (data.Pid != null)
                pacient.Nastav("pid", JsonHodnota.Retezec(data.Pid));
            if (data.Rc != null)
                pacient.Nastav("rc", JsonHodnota.Retezec(data.Rc));
            o.Nastav("patient", pacient);
            JsonHodnota exam = JsonHodnota.NovyObjekt();
            exam.Nastav("time", JsonHodnota.Retezec(data.CasVysetreni));
            o.Nastav("exam", exam);
            if (casti != null)
            {
                foreach (KeyValuePair<string, JsonHodnota> cast in casti.Polozky)
                    o.Nastav(cast.Key, cast.Value);
            }
            if (!string.IsNullOrEmpty(poznamka))
                o.Nastav("note", JsonHodnota.Retezec(poznamka));
            return o;
        }

        /// <summary>
        /// poznamka = poznamky skriptu + upozorneni kontroly, nejvyse 2000 znaku; upozorneni kontroly maji prednost,
        /// zkrati se poznamka skriptu
        /// </summary>
        public static string Poznamka(IList<string> poznamkySkriptu, IList<string> upozorneni)
        {
            string skriptu = string.Join("\n", poznamkySkriptu);
            string kontroly = (upozorneni.Count == 0) ? "" : NadpisKontroly + "\n" + string.Join("\n", upozorneni.Select(u => "- " + u));
            kontroly = Zkrat(kontroly, MaxZnakuPoznamky);
            int zbyva = MaxZnakuPoznamky - kontroly.Length - ((kontroly.Length > 0) ? 1 : 0);
            skriptu = (zbyva > 1) ? Zkrat(skriptu, zbyva) : "";
            if (skriptu.Length == 0)
                return kontroly;
            return (kontroly.Length == 0) ? skriptu : skriptu + "\n" + kontroly;
        }

        /// <summary>text nejvyse delka znaku vcetne trojtecky na konci; nerozdeli dvojici UTF-16</summary>
        public static string Zkrat(string text, int delka)
        {
            if (text.Length <= delka)
                return text;
            int n = delka - 1;
            if (n > 0 && char.IsHighSurrogate(text[n - 1]))
                n--;
            return text.Substring(0, n) + "…";
        }

        public static string Text(JsonHodnota odpoved)
        {
            return JsonZapis.Zapis(odpoved, true);
        }

        public static int Velikost(string text)
        {
            return new UTF8Encoding(false).GetByteCount(text);
        }

        public static void Zapis(string soubor, string text)
        {
            File.WriteAllText(soubor, text, new UTF8Encoding(false));
        }
    }
}
