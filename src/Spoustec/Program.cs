using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Spoustec
{
    /// <summary>
    /// spoustec.exe "vstup" "vystup" ["slozka skriptu"]         beh z Fowleru (bez okna)
    /// spoustec.exe --run "slozka skriptu" "vstup" ["vystup"]   ladeni bez Fowleru
    /// spoustec.exe --test "slozka skriptu" "slozka s testy"    testovaci rezim s protokolem
    /// spoustec.exe                                        informace o programu (kod 1)
    /// </summary>
    static class Program
    {
        public const string SouborVypisuRun = "spoustec-run.txt";

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int MessageBoxW(IntPtr okno, string text, string titulek, uint typ);
        const uint IkonaInformace = 0x40;

        [STAThread]
        static int Main(string[] parametry)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Zapis("Neocekavana chyba: " + e.ExceptionObject);
            try
            {
                if (parametry.Length == 0)
                {
                    MessageBoxW(IntPtr.Zero, Informace(), Verze.Nazev, IkonaInformace);
                    return Beh.KodParametry;
                }
                string prepinac = parametry[0].ToLowerInvariant();
                if (prepinac == "--run" || prepinac == "--test")
                {
                    Vypis vypis = Vypis.ProKonzoli();
                    string[] dalsi = parametry.Skip(1).ToArray();
                    if (prepinac == "--test")
                        return TestovaciRezim.Spust(dalsi, vypis);
                    int kod = Beh.SpustRun(dalsi, vypis);
                    string soubor = Path.Combine(Log.Slozka, SouborVypisuRun);
                    try
                    {
                        vypis.Uloz(soubor);
                    }
                    catch (Exception ex)
                    {
                        Log.Zapis($"{SouborVypisuRun} nelze ulozit: {ex.Message}");
                    }
                    return kod;
                }
                if (prepinac == "--help" || prepinac == "-h" || prepinac == "/?")
                {
                    Vypis.ProKonzoli().Radek(Informace());
                    return Beh.KodParametry;
                }
                //Fowler mohl pri cteni fowler.ini odebrat uvozovky na krajich parametru (PrikazovaRadka)
                string[] opravene = PrikazovaRadka.OpravOdebraneUvozovky(Environment.CommandLine);
                if (opravene != null)
                {
                    Log.Zapis("Parametry z Fowleru bez uvozovek na krajich (odebrany pri cteni fowler.ini), doplneny: " +
                        string.Join(" | ", opravene));
                    parametry = opravene;
                }
                return Beh.SpustZFowleru(parametry);
            }
            catch (Exception ex)
            {
                Log.Zapis("Neocekavana chyba: " + ex);
                return Beh.KodZapis;
            }
        }

        static string Informace()
        {
            return Verze.Nazev + " – spouštěč skriptů pracoviště pro Fowler.\n\n" +
                "Spustí skript pracoviště nad daty, která předá Fowler, a výsledek skriptu formálně zkontroluje. " +
                "Sám nic nepočítá ani nevyhodnocuje.\n\n" +
                "Skripty v JavaScriptu jsou ve složce skriptů pracoviště: pro každou část vyšetření hlavní skript " +
                "s funkcí hodnot(data) (audio.js, tymp.js, speech.js), který může načítat další skripty (pod-skripty) " +
                "ze stejné složky.\n\n" +
                "Nastavení ve Fowleru: Nastavení programu, tlačítko Extra..., Externí program pro hodnocení měření:\n" +
                "  program: cesta k spoustec.exe\n" +
                "  parametry: \"%VSTUP%\" \"%VYSTUP%\" \"složka skriptů\"\n" +
                $"  (bez složky v parametrech se použije složka {Beh.VychoziSlozkaSkriptu} ve složce spouštěče)\n\n" +
                "Ladění: spoustec.exe --run složka_skriptů vstup.json [odpoved.json]\n" +
                "Test:   spoustec.exe --test složka_skriptů složka_s_testy\n\n" +
                "Popis je v README.md a docs\\skripty.md ve složce programu.";
        }
    }
}
