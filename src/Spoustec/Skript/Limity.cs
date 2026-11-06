using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Spoustec.Skript
{
    /// <summary>
    /// Limity behu skriptu. Vychozi hodnoty plati bez nastaveni; zmenit je lze v souboru spoustec.ini ve slozce
    /// programu (popis v docs/skripty.md):
    ///   [limity]
    ///   cas=5        sekundy behu skriptu celkem (nacteni i volani hodnot)
    ///   pamet=64     MB pameti, o kterou muze skript zvetsit pamet programu
    ///   rekurze=256  nejvetsi hloubka vnoreni volani funkci
    /// Neplatna hodnota se ohlasi do logu a pouzije se vychozi.
    /// </summary>
    public sealed class Limity
    {
        public const string SouborNastaveni = "spoustec.ini";

        public int CasSekund { get; set; }
        public int PametMB { get; set; }
        public int HloubkaRekurze { get; set; }

        public Limity()
        {
            CasSekund = 5;
            PametMB = 64;
            HloubkaRekurze = 256;
        }

        public override string ToString()
        {
            return $"čas {CasSekund} s, paměť {PametMB} MB, hloubka rekurze {HloubkaRekurze}";
        }

        /// <summary>limity ze spoustec.ini ve slozce; bez souboru vychozi; problemy s nastavenim do varovani</summary>
        public static Limity Nacti(string slozka, List<string> varovani)
        {
            Limity limity = new Limity();
            string soubor = Path.Combine(slozka, SouborNastaveni);
            if (!File.Exists(soubor))
                return limity;
            string[] radky;
            try
            {
                radky = File.ReadAllLines(soubor, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                varovani.Add($"{SouborNastaveni} nelze přečíst ({ex.Message}), platí výchozí limity");
                return limity;
            }
            foreach (string celyRadek in radky)
            {
                string radek = celyRadek;
                int komentar = radek.IndexOfAny(new[] { ';', '#' });
                if (komentar >= 0)
                    radek = radek.Substring(0, komentar);
                radek = radek.Trim();
                if (radek.Length == 0 || radek.StartsWith("["))
                    continue;
                int rovnitko = radek.IndexOf('=');
                string klic = (rovnitko < 0) ? radek : radek.Substring(0, rovnitko).Trim().ToLowerInvariant();
                string hodnota = (rovnitko < 0) ? "" : radek.Substring(rovnitko + 1).Trim();
                switch (klic)
                {
                    case "cas":
                        limity.CasSekund = Cislo(klic, hodnota, 1, 600, limity.CasSekund, varovani);
                        break;
                    case "pamet":
                        limity.PametMB = Cislo(klic, hodnota, 16, 4096, limity.PametMB, varovani);
                        break;
                    case "rekurze":
                        limity.HloubkaRekurze = Cislo(klic, hodnota, 16, 10000, limity.HloubkaRekurze, varovani);
                        break;
                    default:
                        varovani.Add($"{SouborNastaveni}: neznámé nastavení \"{radek}\" se ignoruje");
                        break;
                }
            }
            return limity;
        }

        static int Cislo(string klic, string text, int min, int max, int vychozi, List<string> varovani)
        {
            int cislo;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out cislo) && cislo >= min && cislo <= max)
                return cislo;
            varovani.Add($"{SouborNastaveni}: {klic}={text} je neplatné (povoleno {min} až {max}), platí {vychozi}");
            return vychozi;
        }
    }
}
