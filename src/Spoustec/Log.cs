using System;
using System.IO;
using System.Text;

namespace Spoustec
{
    /// <summary>
    /// Zaznam prubehu do spoustec.log ve slozce programu; kdyz do ni nelze zapisovat (napr. Program Files),
    /// do %LOCALAPPDATA%\fowler-script-runner. Bez osobnich udaju: pacient se do logu nezapisuje nikdy.
    /// Nad 1 MB se log prejmenuje na .old (uchovaji se dva soubory).
    /// </summary>
    static class Log
    {
        public const string NazevSouboru = "spoustec.log";
        const long MaxVelikost = 1024 * 1024;

        static string slozka;

        /// <summary>slozka logu (a dalsich souboru, ktere program zapisuje sam, napr. spoustec-run.txt)</summary>
        public static string Slozka
        {
            get
            {
                if (slozka == null)
                    slozka = ZapisovatelnaSlozka();
                return slozka;
            }
        }

        static string ZapisovatelnaSlozka()
        {
            string programu = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                using (new FileStream(Path.Combine(programu, NazevSouboru), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                }
                return programu;
            }
            catch (Exception)
            {
                string nahradni = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "fowler-script-runner");
                try
                {
                    Directory.CreateDirectory(nahradni);
                }
                catch (Exception)
                {
                    //log neni nutny pro funkci
                }
                return nahradni;
            }
        }

        /// <summary>zapise zpravu s casem; dalsi radky zpravy se odsadi</summary>
        public static void Zapis(string zprava)
        {
            try
            {
                string soubor = Path.Combine(Slozka, NazevSouboru);
                FileInfo info = new FileInfo(soubor);
                if (info.Exists && info.Length > MaxVelikost)
                {
                    File.Copy(soubor, soubor + ".old", true);
                    File.Delete(soubor);
                }
                string text = zprava.Replace("\r\n", "\n").Replace("\n", Environment.NewLine + "    ");
                File.AppendAllText(soubor, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}", new UTF8Encoding(false));
            }
            catch (Exception)
            {
                //log neni nutny pro funkci
            }
        }
    }
}
