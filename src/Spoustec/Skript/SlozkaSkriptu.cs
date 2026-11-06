using System;
using System.IO;
using Spoustec.Format;

namespace Spoustec.Skript
{
    /// <summary>
    /// Slozka skriptu pracoviste. Pro kazdou cast vysetreni v ni muze byt hlavni skript pojmenovany podle casti
    /// (audio.js, tymp.js, speech.js). Hlavni skript muze nacist dalsi skripty (pod-skripty) ze stejne slozky nebo
    /// jejich podslozek, skripty mimo slozku nacist nejdou. Spoustec spusti jen hlavni skript predane casti.
    /// </summary>
    public sealed class SlozkaSkriptu
    {
        public string Cesta { get; private set; }       //plna cesta
        public string Chyba { get; private set; }       //null = slozka existuje, jinak veta pro uzivatele

        public static SlozkaSkriptu Otevri(string cesta)
        {
            SlozkaSkriptu slozka = new SlozkaSkriptu();
            try
            {
                slozka.Cesta = Path.GetFullPath(cesta).TrimEnd('\\', '/');
                if (slozka.Cesta.EndsWith(":"))
                    slozka.Cesta += "\\";
            }
            catch (Exception ex)
            {
                slozka.Cesta = cesta;
                slozka.Chyba = $"Neplatná cesta ke složce skriptů ({cesta}): {ex.Message}";
                return slozka;
            }
            if (File.Exists(slozka.Cesta))
                slozka.Chyba = $"Místo složky skriptů je zadaný soubor {slozka.Cesta}. Zadejte složku, ve které jsou hlavní " +
                    $"skripty ({string.Join(", ", Array.ConvertAll(PredanaData.ZnameCasti, c => c + ".js"))}).";
            else if (!Directory.Exists(slozka.Cesta))
                slozka.Chyba = "Složka skriptů nebyla nalezena: " + slozka.Cesta;
            return slozka;
        }

        /// <summary>cesta k hlavnimu skriptu casti (napr. audio.js), null = ve slozce neni; jazyk podle pripony</summary>
        public string HlavniSkript(string cast)
        {
            if (Chyba != null)
                return null;
            foreach (IJazykSkriptu jazyk in Jazyky.Vsechny)
            {
                foreach (string pripona in jazyk.Pripony)
                {
                    string soubor = Path.Combine(Cesta, cast + pripona);
                    if (File.Exists(soubor))
                        return soubor;
                }
            }
            return null;
        }

        /// <summary>
        /// cesta souboru vuci slozce skriptu s lomitky "/" (jak se pise v import), napr. "spolecne/cteni-dat.js";
        /// null = soubor neni ve slozce skriptu
        /// </summary>
        public string Relativni(string soubor)
        {
            if (string.IsNullOrEmpty(soubor))
                return null;
            string plna;
            try
            {
                plna = Path.GetFullPath(soubor);
            }
            catch (Exception)
            {
                return null;
            }
            string zaklad = Cesta.EndsWith("\\") ? Cesta : Cesta + "\\";
            if (!plna.StartsWith(zaklad, StringComparison.OrdinalIgnoreCase) || plna.Length == zaklad.Length)
                return null;
            return plna.Substring(zaklad.Length).Replace('\\', '/');
        }
    }
}
