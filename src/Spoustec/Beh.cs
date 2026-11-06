using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Spoustec.Format;
using Spoustec.Json;
using Spoustec.Skript;

namespace Spoustec
{
    /// <summary>
    /// Prubeh behu: precist predana data, odebrat pacienta, spustit hlavni skript predane casti ze slozky skriptu,
    /// formalne zkontrolovat jeho vysledek a zapsat odpoved. Spoustec sam nic nepocita ani nevyhodnocuje.
    ///
    /// Z Fowleru: spoustec.exe "vstup" "vystup" ["slozka skriptu"] (bez slozky "skripty" ve slozce programu, relativni
    /// cesta je vuci slozce programu). Hlavni skript casti je audio.js, tymp.js nebo speech.js ve slozce skriptu, sam
    /// muze nacitat pod-skripty ze stejne slozky. Navratovy kod 0 = odpoved je zapsana, i kdyz skript selhal nebo pro
    /// cast neni (duvod je v poznamce odpovedi, aby ho uzivatel videl ve Fowleru); jiny kod Fowler ohlasi a nic nenacte.
    /// </summary>
    public static class Beh
    {
        public const int KodOk = 0;
        public const int KodParametry = 1;      //chybi parametry
        public const int KodVstup = 2;          //vstupni soubor nelze precist nebo neobsahuje platna data
        public const int KodFormat = 3;         //data v jinem formatu nebo novejsi verzi, nez spoustec zna
        public const int KodZapis = 4;          //odpoved nelze zapsat

        public const string VychoziSlozkaSkriptu = "skripty";

        public static string SlozkaProgramu
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        /// <summary>beh hlavniho skriptu jedne predane casti</summary>
        public sealed class BehCasti
        {
            public string Cast { get; set; }                    //audio, tymp, speech
            public string HlavniSkript { get; set; }            //nazev hlavniho skriptu, napr. audio.js; null = ve slozce neni
            public string Jazyk { get; set; }                   //popis jazyka, null = skript se nespustil
            public VysledekSkriptu BehSkriptu { get; set; }     //null = skript se nespustil
            public KontrolovanyVysledek Kontrola { get; set; }  //null = skript se nespustil nebo selhal
            public string Chyba { get; set; }                   //poznamka o chybe, null = skript probehl

            /// <summary>otisk hlavniho skriptu a pod-skriptu (SouborSkriptu.OtiskSady), null = nic nenacteno</summary>
            public string Otisk
            {
                get { return (BehSkriptu != null) ? BehSkriptu.Otisk : null; }
            }

            /// <summary>do app v odpovedi: "audio.js #1a2b3c4d"</summary>
            public string Popis
            {
                get { return $"{HlavniSkript ?? Cast + ".js"} #{SouborSkriptu.Kratky(Otisk)}"; }
            }
        }

        /// <summary>vysledek zpracovani jednoho vstupu (stejny z Fowleru, --run i --test)</summary>
        public sealed class Zpracovani
        {
            public SlozkaSkriptu Slozka { get; set; }
            public List<BehCasti> Behy { get; private set; }        //po jednom pro kazdou predanou cast
            public KontrolovanyVysledek Kontrola { get; set; }      //platne casti a poznamky vsech behu dohromady
            public string App { get; set; }
            public string Poznamka { get; set; }
            public JsonHodnota Odpoved { get; set; }
            public string TextOdpovedi { get; set; }

            public Zpracovani()
            {
                Behy = new List<BehCasti>();
            }

            public bool Selhal { get { return Behy.Any(b => b.Chyba != null); } }

            /// <summary>chyby vsech behu (kazda na radku), null = zadna</summary>
            public string Chyba
            {
                get { return Selhal ? string.Join("\n", Behy.Where(b => b.Chyba != null).Select(b => b.Chyba)) : null; }
            }

            /// <summary>beh casti audio, tymp nebo speech, null = cast nebyla predana</summary>
            public BehCasti Beh(string cast)
            {
                return Behy.FirstOrDefault(b => b.Cast == cast);
            }
        }

        /// <summary>
        /// pro kazdou predanou cast spusti jeji hlavni skript nad daty a sestavi odpoved; chyba skriptu ani kontroly
        /// nevyjde ven jako vyjimka
        /// </summary>
        public static Zpracovani Zpracuj(PredanaData data, SlozkaSkriptu slozka, Limity limity)
        {
            Zpracovani z = new Zpracovani();
            z.Slozka = slozka;
            z.Kontrola = new KontrolovanyVysledek();
            foreach (string cast in data.Casti)
            {
                BehCasti b = SpustCast(data, cast, slozka, limity);
                z.Behy.Add(b);
                if (b.Kontrola == null)
                {
                    z.Kontrola.PoznamkySkriptu.Add(b.Chyba);
                    continue;
                }
                foreach (KeyValuePair<string, JsonHodnota> platna in b.Kontrola.Casti.Polozky)
                    z.Kontrola.Casti.Nastav(platna.Key, platna.Value);
                z.Kontrola.PoznamkySkriptu.AddRange(b.Kontrola.PoznamkySkriptu);
                z.Kontrola.PoznamkySpoustece.AddRange(b.Kontrola.PoznamkySpoustece);
            }
            z.App = $"{Verze.Nazev}; " + string.Join(", ", z.Behy.Select(b => b.Popis));
            z.Poznamka = Odpoved.Poznamka(z.Kontrola.PoznamkySkriptu, z.Kontrola.PoznamkySpoustece);
            if (z.Poznamka == "")
                z.Poznamka = null;
            DateTime ted = DateTime.Now;
            z.Odpoved = Odpoved.Sestav(data, z.Kontrola.Casti, z.Poznamka, z.App, ted);
            z.TextOdpovedi = Odpoved.Text(z.Odpoved);
            if (Odpoved.Velikost(z.TextOdpovedi) > Odpoved.MaxVelikost)
            {
                z.Poznamka = "Odpověď by byla větší než 1 MB, hodnocení se nevloží (zkontrolujte, co skript vrací).";
                z.Odpoved = Odpoved.Sestav(data, null, z.Poznamka, z.App, ted);
                z.TextOdpovedi = Odpoved.Text(z.Odpoved);
            }
            return z;
        }

        //hlavni skript casti: najit ve slozce, spustit, vysledek zkontrolovat (jen tuto cast)
        static BehCasti SpustCast(PredanaData data, string cast, SlozkaSkriptu slozka, Limity limity)
        {
            BehCasti b = new BehCasti();
            b.Cast = cast;
            try
            {
                if (slozka.Chyba != null)
                {
                    b.Chyba = slozka.Chyba;
                    return b;
                }
                string soubor = slozka.HlavniSkript(cast);
                if (soubor == null)
                {
                    b.Chyba = $"Pro část {cast} ({PredanaData.PopisCasti(cast)}) není ve složce skriptů hlavní skript {cast}.js, " +
                        $"nic se neurčuje (složka {slozka.Cesta}).";
                    return b;
                }
                b.HlavniSkript = Path.GetFileName(soubor);
                IJazykSkriptu jazyk = Jazyky.Pro(soubor);
                b.Jazyk = jazyk.Popis;
                b.BehSkriptu = jazyk.Spust(slozka, soubor, data.DataProSkript, limity);
                if (b.BehSkriptu.Selhal)
                    b.Chyba = b.BehSkriptu.PoznamkaChyby(b.HlavniSkript);
                else
                    b.Kontrola = KontrolaVysledku.Zkontroluj(b.BehSkriptu.Vysledek, new[] { cast });
            }
            catch (Exception ex)
            {
                b.Chyba = "Vnitřní chyba spouštěče: " + ex.Message;
                b.Kontrola = null;
                Log.Zapis("Vnitrni chyba: " + ex);
            }
            return b;
        }

        /// <summary>
        /// zaznam behu do logu: verze, slozka skriptu, pro kazdou cast nactene soubory s plnym SHA-256, doba, chyby
        /// a poznamky; bez pacienta
        /// </summary>
        public static void ZapisDoLogu(Zpracovani z, PredanaData data, string rezim)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Beh {rezim}: {Verze.Nazev}, data z {data.App}, predana cast {string.Join(", ", data.Casti)}");
            sb.Append($"\nSlozka skriptu {z.Slozka.Cesta}");
            foreach (BehCasti b in z.Behy)
            {
                if (b.BehSkriptu == null)
                {
                    sb.Append($"\nCast {b.Cast}: skript se nespustil");
                    continue;
                }
                sb.Append($"\nCast {b.Cast}: hlavni skript {b.HlavniSkript}, {b.Jazyk}, otisk {b.Otisk ?? "-"}, doba behu {b.BehSkriptu.Doba.TotalMilliseconds:0} ms");
                lock (b.BehSkriptu.Soubory)
                {
                    foreach (SouborSkriptu s in b.BehSkriptu.Soubory)
                        sb.Append($"\n  {s.Nazev} SHA-256 {s.Sha256 ?? "-"}");
                }
                foreach (string radek in b.BehSkriptu.Konzole)
                    sb.Append("\nconsole: " + radek);
                if (!string.IsNullOrEmpty(b.BehSkriptu.Podrobnosti))
                    sb.Append("\nPodrobnosti chyby:\n" + Odpoved.Zkrat(b.BehSkriptu.Podrobnosti.Trim(), 2000));
            }
            sb.Append("\nZapsane casti: " + (z.Kontrola.Casti.Polozky.Count > 0 ? string.Join(", ", z.Kontrola.Casti.Polozky.Select(p => p.Key)) : "zadne"));
            if (!string.IsNullOrEmpty(z.Poznamka))
                sb.Append("\nPoznamka: " + z.Poznamka);
            Log.Zapis(sb.ToString());
        }

        /// <summary>slozka skriptu z parametru; prazdna = "skripty" ve slozce programu, relativni cesta vuci slozce programu</summary>
        public static SlozkaSkriptu SlozkaZParametru(string parametr)
        {
            string cesta = string.IsNullOrWhiteSpace(parametr) ? VychoziSlozkaSkriptu : parametr.Trim();
            //Fowler relativni cesty v parametrech neprevadi, slozka se hleda vuci slozce programu
            if (!Path.IsPathRooted(cesta))
                cesta = Path.Combine(SlozkaProgramu, cesta);
            return SlozkaSkriptu.Otevri(cesta);
        }

        /// <summary>beh z Fowleru: spoustec.exe "vstup" "vystup" ["slozka skriptu"]</summary>
        public static int SpustZFowleru(string[] parametry)
        {
            if (parametry.Length < 2)
            {
                Log.Zapis("Chybi parametry: spoustec.exe \"vstupni soubor\" \"vystupni soubor\" [\"slozka skriptu\"]");
                return KodParametry;
            }
            if (parametry.Length > 3)
                Log.Zapis($"Parametry navic se ignoruji ({parametry.Length - 3})");
            Limity limity = NactiLimity();
            SlozkaSkriptu slozka = SlozkaZParametru((parametry.Length >= 3) ? parametry[2] : null);

            PredanaData data;
            try
            {
                data = PredanaData.PrectiSoubor(parametry[0]);
            }
            catch (ChybaVstupuException ex)
            {
                Log.Zapis($"Vstupni data nelze pouzit (kod {ex.Kod}): {ex.Message}");
                return ex.Kod;
            }
            Zpracovani z = Zpracuj(data, slozka, limity);
            ZapisDoLogu(z, data, "z Fowleru");
            try
            {
                Odpoved.Zapis(parametry[1], z.TextOdpovedi);
            }
            catch (Exception ex)
            {
                Log.Zapis("Odpoved nelze zapsat: " + ex.Message);
                return KodZapis;
            }
            return KodOk;
        }

        /// <summary>limity ze spoustec.ini ve slozce programu, problemy s nastavenim do logu</summary>
        public static Limity NactiLimity()
        {
            List<string> varovani = new List<string>();
            Limity limity = Limity.Nacti(SlozkaProgramu, varovani);
            foreach (string v in varovani)
                Log.Zapis(v);
            return limity;
        }

        /// <summary>
        /// ladeni bez Fowleru: spoustec.exe --run "slozka skriptu" "vstup" ["vystup"]; vypise nactene skripty, vystup
        /// console, poznamky a odpoved (pid a rc skryte), odpoved zapise do vystupu, kdyz je zadany; navratovy kod jako
        /// pri behu z Fowleru
        /// </summary>
        public static int SpustRun(string[] parametry, Vypis vypis)
        {
            if (parametry.Length < 2)
            {
                vypis.Radek("Použití: spoustec.exe --run \"složka skriptů\" \"vstup.json\" [\"vystup.json\"]");
                return KodParametry;
            }
            Limity limity = NactiLimity();
            SlozkaSkriptu slozka = SlozkaSkriptu.Otevri(parametry[0]);
            string vstup = Path.GetFullPath(parametry[1]);
            vypis.Radek($"Spouštěč:        {Verze.Nazev}");
            vypis.Radek($"Složka skriptů:  {slozka.Cesta}");
            vypis.Radek($"Vstup:           {vstup}");
            vypis.Radek($"Limity:          {limity}");

            PredanaData data;
            try
            {
                data = PredanaData.PrectiSoubor(vstup);
            }
            catch (ChybaVstupuException ex)
            {
                vypis.Radek();
                vypis.Radek($"Vstupní data nelze použít: {ex.Message}");
                vypis.Radek($"Návratový kód: {ex.Kod}");
                return ex.Kod;
            }
            vypis.Radek($"Předáno:         {string.Join(", ", data.Casti)}");
            Zpracovani z = Zpracuj(data, slozka, limity);
            ZapisDoLogu(z, data, "--run");
            foreach (BehCasti b in z.Behy)
            {
                vypis.Radek();
                if (b.BehSkriptu == null)
                {
                    vypis.Radek($"Část {b.Cast}: skript se nespustil");
                    continue;
                }
                vypis.Radek($"Část {b.Cast}: hlavní skript {b.HlavniSkript}, {b.Jazyk}, doba běhu {b.BehSkriptu.Doba.TotalMilliseconds:0} ms");
                vypis.Radek("Načtené skripty (SHA-256):");
                lock (b.BehSkriptu.Soubory)
                {
                    foreach (SouborSkriptu s in b.BehSkriptu.Soubory)
                        vypis.Radek($"  {s.Sha256 ?? "(nelze přečíst)",-64}  {s.Nazev}");
                }
                vypis.Radek($"Otisk skriptů:   {b.Otisk ?? "-"} (v odpovědi {b.Popis})");
                vypis.Radek("Výstup console:");
                if (b.BehSkriptu.Konzole.Count == 0)
                    vypis.Radek("  (žádný)");
                foreach (string radek in b.BehSkriptu.Konzole)
                    vypis.Radek("  " + radek.Replace("\n", "\n  "));
            }

            vypis.Radek();
            vypis.Radek(z.Selhal ? "Skript selhal nebo chybí, odpověď je bez hodnocení. Poznámka:" : "Poznámka v odpovědi:");
            vypis.Radek(string.IsNullOrEmpty(z.Poznamka) ? "  (žádná)" : "  " + z.Poznamka.Replace("\n", "\n  "));
            vypis.Radek();
            vypis.Radek("Odpověď pro Fowler (pid a rc jsou v souboru odpovědi zkopírované ze vstupu, zde skryté):");
            vypis.Radek(Odpoved.Text(SkrytPacienta(z.Odpoved)));

            int kod = KodOk;
            if (parametry.Length >= 3)
            {
                string vystup = Path.GetFullPath(parametry[2]);
                try
                {
                    Odpoved.Zapis(vystup, z.TextOdpovedi);
                    vypis.Radek();
                    vypis.Radek($"Odpověď zapsána: {vystup}");
                }
                catch (Exception ex)
                {
                    vypis.Radek();
                    vypis.Radek($"Odpověď nelze zapsat: {ex.Message}");
                    kod = KodZapis;
                }
            }
            vypis.Radek($"Návratový kód: {kod}");
            return kod;
        }

        //kopie odpovedi s hodnotami pid a rc nahrazenymi "***" (do vypisu, ktery se uklada i do souboru)
        static JsonHodnota SkrytPacienta(JsonHodnota odpoved)
        {
            JsonHodnota kopie = JsonHodnota.NovyObjekt();
            foreach (KeyValuePair<string, JsonHodnota> p in odpoved.Polozky)
            {
                if (p.Key != "patient")
                {
                    kopie.Nastav(p.Key, p.Value);
                    continue;
                }
                JsonHodnota pacient = JsonHodnota.NovyObjekt();
                foreach (KeyValuePair<string, JsonHodnota> udaj in p.Value.Polozky)
                    pacient.Nastav(udaj.Key, JsonHodnota.Retezec("***"));
                kopie.Nastav(p.Key, pacient);
            }
            return kopie;
        }
    }
}
