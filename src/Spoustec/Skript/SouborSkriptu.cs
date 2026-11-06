using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Spoustec.Skript
{
    /// <summary>
    /// soubor skriptu (hlavni skript nebo pod-skript): text v UTF-8 (BOM se toleruje) a SHA-256 jeho obsahu, podle
    /// ktereho je dohledatelne, kterou verzi skriptu hodnoceni vzniklo (odpoved app, log, protokol testu)
    /// </summary>
    public sealed class SouborSkriptu
    {
        public string Cesta { get; private set; }       //plna cesta
        public string Nazev { get; private set; }       //cesta vuci slozce skriptu, napr. "audio.js", "spolecne/cteni-dat.js"
        public string Sha256 { get; private set; }      //64 hex znaku malymi pismeny, null = soubor nelze precist
        public string Text { get; private set; }        //null = chyba
        public string Chyba { get; private set; }       //null = soubor je v poradku, jinak veta pro uzivatele

        /// <summary>prvnich 8 hex znaku SHA-256 (do app v odpovedi), "--------" kdyz soubor nelze precist</summary>
        public string KratkyOtisk
        {
            get { return Kratky(Sha256); }
        }

        /// <summary>nacte soubor; nazev = cesta vuci slozce skriptu (do poznamek a protokolu), null = nazev souboru</summary>
        public static SouborSkriptu Nacti(string cesta, string nazev = null)
        {
            SouborSkriptu skript = new SouborSkriptu();
            try
            {
                skript.Cesta = Path.GetFullPath(cesta);
            }
            catch (Exception)
            {
                skript.Cesta = cesta;
            }
            skript.Nazev = nazev ?? Path.GetFileName(cesta);
            byte[] data;
            try
            {
                if (!File.Exists(skript.Cesta))
                {
                    skript.Chyba = "Skript nebyl nalezen: " + skript.Cesta;
                    return skript;
                }
                data = File.ReadAllBytes(skript.Cesta);
            }
            catch (Exception ex)
            {
                skript.Chyba = "Skript nelze přečíst: " + ex.Message;
                return skript;
            }
            skript.Sha256 = Otisk(data);
            int od = (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) ? 3 : 0;
            try
            {
                skript.Text = new UTF8Encoding(false, true).GetString(data, od, data.Length - od);
            }
            catch (DecoderFallbackException)
            {
                skript.Chyba = "Skript není uložený v kódování UTF-8 (uložte ho v editoru jako UTF-8): " + skript.Nazev;
            }
            return skript;
        }

        /// <summary>
        /// otisk vsech souboru, ze kterych hodnoceni vzniklo (hlavni skript a pod-skripty): SHA-256 textu, ktery ma
        /// pro kazdy soubor radek "SHA-256  nazev" (konec radku LF) serazene podle nazvu, stejne jako vypis sha256sum;
        /// null = zadny soubor nebyl nacten
        /// </summary>
        public static string OtiskSady(IEnumerable<SouborSkriptu> soubory)
        {
            List<string> radky = soubory.Where(s => s.Sha256 != null).Select(s => s.Sha256 + "  " + s.Nazev + "\n")
                .Distinct().OrderBy(r => r.Substring(66), StringComparer.Ordinal).ToList();
            if (radky.Count == 0)
                return null;
            return Otisk(new UTF8Encoding(false).GetBytes(string.Concat(radky)));
        }

        /// <summary>prvnich 8 hex znaku otisku, "--------" kdyz otisk neni</summary>
        public static string Kratky(string otisk)
        {
            return (otisk != null) ? otisk.Substring(0, 8) : "--------";
        }

        /// <summary>SHA-256 jako 64 hex znaku malymi pismeny</summary>
        public static string Otisk(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                StringBuilder sb = new StringBuilder(64);
                foreach (byte b in sha.ComputeHash(data))
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
