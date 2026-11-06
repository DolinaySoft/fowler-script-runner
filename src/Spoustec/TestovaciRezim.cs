using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Spoustec.Format;
using Spoustec.Json;
using Spoustec.Skript;

namespace Spoustec
{
    /// <summary>
    /// Testovaci rezim: spoustec.exe --test "slozka skriptu" "slozka s testy". Kazdy pripad je dvojice souboru
    ///   pripad01.json              vstup fowler-exam (smyslena data)
    ///   pripad01.ocekavano.json    ocekavany vysledek ve tvaru, ktery vraci hodnot() (audio, tymp, speech)
    /// Hlavni skript predane casti se spusti stejne jako z Fowleru (vcetne formalni kontroly) a pole se porovnaji
    /// s ocekavanim. Protokol (podklad pro validacni dokumentaci pracoviste) uvadi vsechny pouzite skripty s SHA-256
    /// a ulozi se do slozky s testy jako protokol-slozka-datum-cas.txt. Navratovy kod 0 = vsechny pripady OK.
    /// </summary>
    public static class TestovaciRezim
    {
        public const int KodNeproslo = 5;   //nektery pripad ROZDIL nebo CHYBA
        public const string PriponaOcekavani = ".ocekavano.json";

        enum Stav { OK, ROZDIL, CHYBA }

        public static int Spust(string[] parametry, Vypis vypis)
        {
            if (parametry.Length < 2)
            {
                vypis.Radek("Použití: spoustec.exe --test \"složka skriptů\" \"složka s testy\"");
                return Beh.KodParametry;
            }
            Limity limity = Beh.NactiLimity();
            SlozkaSkriptu skripty = SlozkaSkriptu.Otevri(parametry[0]);
            if (skripty.Chyba != null)
            {
                vypis.Radek(skripty.Chyba);
                return Beh.KodVstup;
            }
            string slozka = Path.GetFullPath(parametry[1]);
            if (!Directory.Exists(slozka))
            {
                vypis.Radek("Složka s testy neexistuje: " + slozka);
                return Beh.KodVstup;
            }
            List<string> pripady = Directory.GetFiles(slozka, "*.json")
                .Where(f => !f.EndsWith(PriponaOcekavani, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            if (pripady.Count == 0)
            {
                vypis.Radek($"Ve složce nejsou žádné testovací případy (*.json s očekáváním *{PriponaOcekavani}): {slozka}");
                return Beh.KodVstup;
            }

            DateTime cas = DateTime.Now;
            Vypis protokol = Vypis.BezKonzole();
            Zahlavi(protokol, cas, skripty, slozka, limity);
            Zahlavi(vypis, cas, skripty, slozka, limity);

            Dictionary<Stav, int> pocty = new Dictionary<Stav, int> { { Stav.OK, 0 }, { Stav.ROZDIL, 0 }, { Stav.CHYBA, 0 } };
            //pouzite skripty: "audio.js #otisk" -> nactene soubory (pro validacni dokumentaci)
            SortedDictionary<string, List<SouborSkriptu>> pouzite = new SortedDictionary<string, List<SouborSkriptu>>(StringComparer.Ordinal);
            foreach (string pripad in pripady)
            {
                List<string> radky = new List<string>();
                List<string> konzole = new List<string>();
                Stav stav = Pripad(pripad, skripty, limity, radky, konzole, pouzite);
                pocty[stav]++;
                string nazev = Path.GetFileNameWithoutExtension(pripad);
                string stavText = (stav == Stav.ROZDIL) ? "ROZDÍL" : stav.ToString();
                foreach (Vypis v in new[] { protokol, vypis })
                {
                    v.Radek($"[{stavText}] {nazev}");
                    foreach (string r in radky)
                        v.Radek("    " + r.Replace("\n", "\n    "));
                }
                foreach (string r in konzole)
                    vypis.Radek("    console: " + r.Replace("\n", "\n    "));
            }

            bool proslo = pocty[Stav.OK] == pripady.Count;
            foreach (Vypis v in new[] { protokol, vypis })
            {
                v.Radek();
                v.Radek("Použité skripty");
                v.Radek("---------------");
                v.Radek("Otisk = SHA-256 seznamu načtených souborů (řádky „SHA-256  soubor“ seřazené podle názvu, jako sha256sum).");
                if (pouzite.Count == 0)
                    v.Radek("(žádný skript se nenačetl)");
                foreach (KeyValuePair<string, List<SouborSkriptu>> p in pouzite)
                {
                    v.Radek($"{p.Key.Substring(0, p.Key.IndexOf(' '))}: otisk {p.Key.Substring(p.Key.IndexOf(' ') + 1)}");
                    foreach (SouborSkriptu soubor in p.Value)
                        v.Radek($"  {soubor.Sha256 ?? "(nelze přečíst)",-64}  {soubor.Nazev}");
                }
                v.Radek();
                v.Radek("Souhrn");
                v.Radek("------");
                v.Radek($"Případů: {pripady.Count}, OK: {pocty[Stav.OK]}, ROZDÍL: {pocty[Stav.ROZDIL]}, CHYBA: {pocty[Stav.CHYBA]}");
                v.Radek("Výsledek: " + (proslo ? "PROŠLO – všechny případy odpovídají očekávání" : "NEPROŠLO – některé případy neodpovídají očekávání"));
            }

            string nazevProtokolu = $"protokol-{Path.GetFileName(skripty.Cesta.TrimEnd('\\'))}-{cas:yyyy-MM-dd_HH-mm-ss}.txt";
            string souborProtokolu = Path.Combine(slozka, nazevProtokolu);
            try
            {
                protokol.Uloz(souborProtokolu);
                vypis.Radek();
                vypis.Radek("Protokol: " + souborProtokolu);
            }
            catch (Exception ex)
            {
                vypis.Radek();
                vypis.Radek($"Protokol nelze uložit ({souborProtokolu}): {ex.Message}");
                return Beh.KodZapis;
            }
            Log.Zapis($"Test {skripty.Cesta} ({string.Join(", ", pouzite.Keys)}): {pripady.Count} pripadu, OK {pocty[Stav.OK]}, " +
                $"ROZDIL {pocty[Stav.ROZDIL]}, CHYBA {pocty[Stav.CHYBA]}; protokol {souborProtokolu}");
            return proslo ? Beh.KodOk : KodNeproslo;
        }

        static void Zahlavi(Vypis v, DateTime cas, SlozkaSkriptu skripty, string slozka, Limity limity)
        {
            v.Radek("PROTOKOL TESTU SKRIPTŮ");
            v.Radek("======================");
            v.Radek($"Datum a čas testu:  {cas.ToString("d. M. yyyy H:mm:ss", CultureInfo.InvariantCulture)}");
            v.Radek($"Spouštěč:           {Verze.Nazev} (fowler-script-runner)");
            v.Radek($"Jazyk skriptů:      {string.Join(", ", Jazyky.Vsechny.Select(j => j.Popis))}");
            v.Radek($"Složka skriptů:     {skripty.Cesta}");
            v.Radek($"Složka s testy:     {slozka}");
            v.Radek($"Limity:             {limity}");
            v.Radek();
            v.Radek("Pro každý případ se spustí hlavní skript předané části (audio.js, tymp.js, speech.js) i s pod-skripty,");
            v.Radek("které načte; jejich SHA-256 jsou v části Použité skripty. Porovnávají se pole výsledku po formální");
            v.Radek("kontrole spouštěče (tak, jak by je dostal Fowler), čísla podle hodnoty v JSON bez tolerance;");
            v.Radek("poznámky se neporovnávají, jen vypisují.");
            v.Radek();
            v.Radek("Případy");
            v.Radek("-------");
        }

        static Stav Pripad(string soubor, SlozkaSkriptu skripty, Limity limity, List<string> radky, List<string> konzole,
            SortedDictionary<string, List<SouborSkriptu>> pouzite)
        {
            string souborOcekavani = soubor.Substring(0, soubor.Length - ".json".Length) + PriponaOcekavani;
            radky.Add($"vstup:     {Path.GetFileName(soubor)}  SHA-256 {OtiskSouboru(soubor)}");
            radky.Add($"očekávání: {Path.GetFileName(souborOcekavani)}  " +
                (File.Exists(souborOcekavani) ? "SHA-256 " + OtiskSouboru(souborOcekavani) : "(soubor chybí)"));

            PredanaData data;
            try
            {
                data = PredanaData.PrectiSoubor(soubor);
            }
            catch (ChybaVstupuException ex)
            {
                radky.Add("CHYBA: vstupní data nelze použít: " + ex.Message);
                return Stav.CHYBA;
            }
            Beh.Zpracovani z = Beh.Zpracuj(data, skripty, limity);
            Beh.ZapisDoLogu(z, data, "--test " + Path.GetFileName(soubor));
            foreach (Beh.BehCasti b in z.Behy)
            {
                if (b.BehSkriptu == null)
                    continue;
                konzole.AddRange(b.BehSkriptu.Konzole);
                List<SouborSkriptu> soubory;
                lock (b.BehSkriptu.Soubory)
                    soubory = b.BehSkriptu.Soubory.ToList();
                radky.Add($"skripty:   {b.Popis} ({string.Join(", ", soubory.Select(s => s.Nazev))})");
                if (b.Otisk != null)
                    pouzite[b.HlavniSkript + " " + b.Otisk] = soubory;
            }
            if (z.Selhal)
            {
                radky.Add("CHYBA: " + z.Chyba);
                return Stav.CHYBA;
            }

            Stav stav;
            if (!File.Exists(souborOcekavani))
            {
                radky.Add("CHYBA: chybí soubor s očekávaným výsledkem " + Path.GetFileName(souborOcekavani));
                stav = Stav.CHYBA;
            }
            else
            {
                JsonHodnota ocekavano = null;
                string chyba = null;
                try
                {
                    ocekavano = JsonCteni.PrectiSoubor(souborOcekavani);
                    if (ocekavano.Typ != TypJson.Objekt)
                        chyba = "očekávaný výsledek musí být objekt { \"audio\": { ... } }";
                }
                catch (Exception ex) when (ex is ChybaJsonException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    chyba = ex.Message;
                }
                if (chyba != null)
                {
                    radky.Add($"CHYBA: očekávaný výsledek nelze použít ({Path.GetFileName(souborOcekavani)}): {chyba}");
                    stav = Stav.CHYBA;
                }
                else
                {
                    List<Porovnani.Rozdil> rozdily = Porovnani.Porovnej(ocekavano, z.Kontrola.Casti);
                    foreach (Porovnani.Rozdil r in rozdily)
                        radky.Add($"ROZDÍL {r.Cesta}: očekáváno {r.Ocekavano}, výsledek {r.Vysledek}");
                    int poli = Porovnani.Porovnej(JsonHodnota.NovyObjekt(), z.Kontrola.Casti).Count;
                    if (rozdily.Count == 0)
                        radky.Add((poli == 0) ? "shoda: skript neurčil žádné pole, očekávání také žádné" : $"shoda ve všech polích ({poli})");
                    stav = (rozdily.Count == 0) ? Stav.OK : Stav.ROZDIL;
                }
            }
            if (!string.IsNullOrEmpty(z.Poznamka))
                radky.Add("poznámka: " + z.Poznamka);
            return stav;
        }

        static string OtiskSouboru(string soubor)
        {
            try
            {
                return SouborSkriptu.Otisk(File.ReadAllBytes(soubor));
            }
            catch (Exception)
            {
                return "(nelze přečíst)";
            }
        }
    }
}
