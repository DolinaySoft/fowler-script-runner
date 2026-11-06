using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Spoustec.Skript
{
    /// <summary>
    /// Spusteni hlavniho skriptu v jednom jazyce. Kazdy jazyk ma stejne rozhrani: hlavni skript obsahuje funkci
    /// hodnot(data), dostane data jako text JSON a vrati vysledek jako Hodnota; pod-skripty smi nacitat jen ze slozky
    /// skriptu. Jazyk se vybira podle pripony souboru (Jazyky.Pro), dalsi jazyk (napr. Python jako samostatny proces)
    /// se prida dalsi tridou do Jazyky.Vsechny.
    /// </summary>
    public interface IJazykSkriptu
    {
        /// <summary>nazev a verze do logu a protokolu testu, napr. "JavaScript (Jint 4.17.0)"</summary>
        string Popis { get; }

        /// <summary>pripony souboru vcetne tecky, napr. ".js"</summary>
        string[] Pripony { get; }

        /// <summary>
        /// spusti hlavni skript (plna cesta, soubor ve slozce skriptu) a zavola hodnot(data); dataJson je predany JSON
        /// bez bloku patient; zadna chyba skriptu nesmi vyjit ven jako vyjimka, vrati se jako VysledekSkriptu.Chyba
        /// </summary>
        VysledekSkriptu Spust(SlozkaSkriptu slozka, string hlavniSkript, string dataJson, Limity limity);
    }

    /// <summary>vysledek behu hlavniho skriptu</summary>
    public sealed class VysledekSkriptu
    {
        public Hodnota Vysledek { get; set; }           //co vratila hodnot(data); null kdyz skript selhal
        public string Chyba { get; set; }               //null = probehl; jinak popis pro uzivatele (bez mista)
        public string SouborChyby { get; private set; } //kde chyba nastala (cesta vuci slozce skriptu), kdyz je znamo
        public int? Radek { get; private set; }
        public string Podrobnosti { get; set; }         //do logu (zasobnik volani apod.)
        public List<SouborSkriptu> Soubory { get; private set; }  //nactene soubory v poradi nacteni, prvni je hlavni skript
        public List<string> Konzole { get; private set; }   //vystup console.log/warn/error (zkraceny)
        public TimeSpan Doba { get; set; }

        public VysledekSkriptu()
        {
            Soubory = new List<SouborSkriptu>();
            Konzole = new List<string>();
        }

        public bool Selhal { get { return Chyba != null; } }

        /// <summary>otisk vsech nactenych souboru (SouborSkriptu.OtiskSady), null = nic nenacteno</summary>
        public string Otisk
        {
            get
            {
                lock (Soubory)
                    return SouborSkriptu.OtiskSady(Soubory);
            }
        }

        /// <summary>misto chyby: soubor vuci slozce skriptu a radek (radek bez souboru se nepouzije)</summary>
        public void Misto(string soubor, int? radek)
        {
            SouborChyby = soubor;
            Radek = (soubor != null && radek.HasValue && radek.Value > 0) ? radek : null;
        }

        /// <summary>
        /// chyba do poznamky v odpovedi, napr. "Skript selhal: TypeError: ... (ztrata-fowler.js, radek 14)";
        /// bez znameho mista se uvede hlavni skript
        /// </summary>
        public string PoznamkaChyby(string hlavniSkript)
        {
            return $"Skript selhal: {Chyba} ({SouborChyby ?? hlavniSkript}" + (Radek.HasValue ? $", řádek {Radek.Value})" : ")");
        }
    }

    public static class Jazyky
    {
        public static readonly IJazykSkriptu[] Vsechny = { new JazykJavaScript() };

        /// <summary>jazyk podle pripony souboru, null = nepodporovany</summary>
        public static IJazykSkriptu Pro(string soubor)
        {
            string pripona = Path.GetExtension(soubor) ?? "";
            return Vsechny.FirstOrDefault(j => j.Pripony.Any(p => string.Equals(p, pripona, StringComparison.OrdinalIgnoreCase)));
        }

        public static string PodporovanePripony()
        {
            return string.Join(", ", Vsechny.SelectMany(j => j.Pripony));
        }
    }
}
