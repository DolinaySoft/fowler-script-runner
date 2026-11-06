using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spoustec.Format;
using Spoustec.Json;
using Spoustec.Skript;

namespace Spoustec.Tests
{
    static class Pomocnik
    {
        public static string Priklady
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "priklady"); }
        }

        public static string Priklad(string nazev)
        {
            return File.ReadAllText(Path.Combine(Priklady, nazev), Encoding.UTF8);
        }

        /// <summary>nova prazdna slozka pro jeden test</summary>
        public static string NovaSlozka()
        {
            string slozka = Path.Combine(Path.GetTempPath(), "spoustec-testy", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(slozka);
            return slozka;
        }

        public static string ZapisSoubor(string slozka, string nazev, string text)
        {
            string soubor = Path.Combine(slozka, nazev);
            File.WriteAllText(soubor, text, new UTF8Encoding(false));
            return soubor;
        }

        /// <summary>
        /// zpracuje vstup (stejne jako pri behu z Fowleru) skriptem v nove slozce skriptu: skriptJs je hlavni skript
        /// predane casti (audio.js, tymp.js, speech.js), podskripty dvojice nazev, text
        /// </summary>
        public static Beh.Zpracovani Spust(string skriptJs, string vstupJson = null, Limity limity = null, params string[] podskripty)
        {
            string slozka = NovaSlozka();
            PredanaData data = PredanaData.PrectiText(vstupJson ?? Priklad("audio.json"));
            ZapisSoubor(slozka, data.Casti[0] + ".js", skriptJs);
            for (int i = 0; i + 1 < podskripty.Length; i += 2)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(slozka, podskripty[i])));
                ZapisSoubor(slozka, podskripty[i], podskripty[i + 1]);
            }
            return Beh.Zpracuj(data, SlozkaSkriptu.Otevri(slozka), limity ?? new Limity());
        }

        /// <summary>zpracuje vstup skripty z dane slozky</summary>
        public static Beh.Zpracovani SpustSlozku(string slozka, string vstupJson = null, Limity limity = null)
        {
            return Beh.Zpracuj(PredanaData.PrectiText(vstupJson ?? Priklad("audio.json")), SlozkaSkriptu.Otevri(slozka), limity ?? new Limity());
        }

        /// <summary>hlavni skript, ktery vrati pevny vysledek (vyraz JavaScriptu)</summary>
        public static Beh.Zpracovani Vrat(string vysledekJs, string vstupJson = null)
        {
            return Spust("export function hodnot(data) {\n  return " + vysledekJs + ";\n}\n", vstupJson);
        }

        public static void Obsahuje(string text, string cast)
        {
            Assert.IsTrue(text != null && text.Contains(cast), $"Očekáváno \"{cast}\" v:\n{text}");
        }

        public static void Neobsahuje(string text, string cast)
        {
            Assert.IsTrue(text == null || !text.Contains(cast), $"Neočekáváno \"{cast}\" v:\n{text}");
        }

        /// <summary>hodnota v odpovedi podle cesty, napr. "audio.acAvg.R"; null = neni</summary>
        public static JsonHodnota Pole(JsonHodnota objekt, string cesta)
        {
            JsonHodnota h = objekt;
            foreach (string klic in cesta.Split('.'))
            {
                h = (h != null) ? h.Polozka(klic) : null;
            }
            return h;
        }

        public static string SpoustecExe
        {
            get
            {
                return typeof(Pomocnik).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .First(a => a.Key == "SpoustecExe").Value;
            }
        }

        public sealed class VysledekProcesu
        {
            public int Kod { get; set; }
            public TimeSpan Doba { get; set; }
        }

        /// <summary>spusti spoustec.exe jako proces (vystup do konzole se zahodi, testy ctou soubory)</summary>
        public static VysledekProcesu Proces(string pracovniSlozka, params string[] parametry)
        {
            return ProcesSPrikazem(pracovniSlozka, string.Join(" ", parametry.Select(p => "\"" + p + "\"")));
        }

        /// <summary>spusti spoustec.exe s prikazovou radkou presne tak, jak je zadana (za nazvem programu)</summary>
        public static VysledekProcesu ProcesSPrikazem(string pracovniSlozka, string argumenty)
        {
            ProcessStartInfo info = new ProcessStartInfo(Path.GetFullPath(SpoustecExe))
            {
                Arguments = argumenty,
                UseShellExecute = false,
                WorkingDirectory = pracovniSlozka ?? Path.GetTempPath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Stopwatch stopky = Stopwatch.StartNew();
            using (Process p = Process.Start(info))
            {
                p.StandardOutput.ReadToEndAsync();
                p.StandardError.ReadToEndAsync();
                Assert.IsTrue(p.WaitForExit(60000), "spoustec.exe neskončil do 60 s");
                return new VysledekProcesu { Kod = p.ExitCode, Doba = stopky.Elapsed };
            }
        }
    }
}
