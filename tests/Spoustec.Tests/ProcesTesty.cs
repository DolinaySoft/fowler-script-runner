using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spoustec.Json;
using Spoustec.Skript;
using static Spoustec.Tests.Pomocnik;

namespace Spoustec.Tests
{
    /// <summary>spoustec.exe jako proces: beh jako z Fowleru, --run a --test, navratove kody</summary>
    [TestClass]
    public sealed class ProcesTesty
    {
        static readonly string[] PrikladyVstupu = { "audio.json", "tympanometrie.json", "slovni.json" };

        [TestMethod]
        public void RunUkazkyNaVsechPrikladech()
        {
            string slozka = NovaSlozka();
            foreach (string priklad in PrikladyVstupu)
            {
                string vystup = Path.Combine(slozka, "odpoved-" + priklad);
                VysledekProcesu p = Proces(slozka, "--run", Path.Combine(Priklady, "ukazka"), Path.Combine(Priklady, priklad), vystup);
                Assert.AreEqual(Beh.KodOk, p.Kod, priklad);
                JsonHodnota vstup = JsonCteni.PrectiSoubor(Path.Combine(Priklady, priklad));
                JsonHodnota odpoved = JsonCteni.PrectiSoubor(vystup);
                Assert.AreEqual("fowler-eval", odpoved.Polozka("fmt").Text);
                Assert.AreEqual("1", odpoved.Polozka("v").Cislo);
                Assert.IsTrue(Regex.IsMatch(odpoved.Polozka("app").Text, @"^spoustec \S+; (audio|tymp|speech)\.js #[0-9a-f]{8}$"), odpoved.Polozka("app").Text);
                Assert.IsTrue(Regex.IsMatch(odpoved.Polozka("created").Text, @"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d$"));
                Assert.AreEqual(Pole(vstup, "patient.pid").Text, Pole(odpoved, "patient.pid").Text);
                Assert.AreEqual(Pole(vstup, "patient.rc").Text, Pole(odpoved, "patient.rc").Text);
                Assert.AreEqual(Pole(vstup, "exam.time").Text, Pole(odpoved, "exam.time").Text);
                Assert.AreEqual(2, odpoved.Polozka("patient").Polozky.Count);
                Obsahuje(odpoved.Polozka("note").Text, "Skript běží, předáno vyšetření z ");
                //ukazka nic nehodnoti
                Assert.IsTrue(odpoved.Polozky.All(o => new[] { "fmt", "v", "app", "created", "patient", "exam", "note" }.Contains(o.Key)));
                //soubor UTF-8 bez BOM
                Assert.AreNotEqual(0xEF, File.ReadAllBytes(vystup)[0]);
            }
        }

        [TestMethod]
        public void RunVypisSkryjePacienta()
        {
            string slozka = NovaSlozka();
            Assert.AreEqual(Beh.KodOk, Proces(slozka, "--run", Path.Combine(Priklady, "ukazka"), Path.Combine(Priklady, "audio.json")).Kod);
            string vypis = File.ReadAllText(Path.Combine(Path.GetDirectoryName(SpoustecExe), Program.SouborVypisuRun), Encoding.UTF8);
            Obsahuje(vypis, "Skript běží");
            Neobsahuje(vypis, "5704150000");
            Neobsahuje(vypis, "4F0C1E2A");
        }

        [TestMethod]
        public void BehJakoZFowleru()
        {
            string slozka = NovaSlozka();
            string vystup = Path.Combine(slozka, "hodnoceni_vystup.json");
            //relativni cesta ke slozce skriptu je vuci slozce programu, ne vuci pracovni slozce
            string relativni = Path.Combine("..", "..", "..", "..", "..", "priklady", "ukazka");
            Assert.IsTrue(Directory.Exists(Path.Combine(Path.GetDirectoryName(SpoustecExe), relativni)));
            Assert.AreEqual(Beh.KodOk, Proces(slozka, Path.Combine(Priklady, "audio.json"), vystup, relativni).Kod);
            Obsahuje(JsonCteni.PrectiSoubor(vystup).Polozka("note").Text, "Skript běží");

            //bez slozky v parametrech slozka skripty ve slozce programu (tam neni): odpoved s poznamkou, kod 0
            File.Delete(vystup);
            Assert.AreEqual(Beh.KodOk, Proces(slozka, Path.Combine(Priklady, "audio.json"), vystup).Kod);
            JsonHodnota odpoved = JsonCteni.PrectiSoubor(vystup);
            Obsahuje(odpoved.Polozka("note").Text, "Složka skriptů nebyla nalezena");
            Obsahuje(odpoved.Polozka("note").Text, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(SpoustecExe)), Beh.VychoziSlozkaSkriptu));
            Assert.AreEqual("5704150000", Pole(odpoved, "patient.rc").Text);
        }

        [TestMethod]
        public void FowlerOdebralUvozovkyNaKrajichParametru()
        {
            //fowler.ini: parametry="%VSTUP%" "%VYSTUP%" "slozka" -> GetPrivateProfileString odebere krajni uvozovky
            //a Fowler spusti: "spoustec.exe" vstup" "vystup" "slozka skriptu (vse s mezerami)
            string slozka = Path.Combine(NovaSlozka(), "Fowler data");
            Directory.CreateDirectory(slozka);
            string skripty = Path.Combine(slozka, "moje skripty");
            Directory.CreateDirectory(skripty);
            ZapisSoubor(skripty, "audio.js", "export function hodnot(data) { return { audio: { acAvg: { R: 20 } } }; }\n");
            string vstup = ZapisSoubor(slozka, "hodnoceni vstup.json", Priklad("audio.json"));
            string vystup = Path.Combine(slozka, "hodnoceni vystup.json");
            Assert.AreEqual(Beh.KodOk, ProcesSPrikazem(slozka, $"{vstup}\" \"{vystup}\" \"{skripty}").Kod);
            Assert.AreEqual("20", Pole(JsonCteni.PrectiSoubor(vystup), "audio.acAvg.R").Cislo);

            //totez bez slozky skriptu (vychozi parametry "%VSTUP%" "%VYSTUP%" zapsane v fowler.ini)
            File.Delete(vystup);
            Assert.AreEqual(Beh.KodOk, ProcesSPrikazem(slozka, $"{vstup}\" \"{vystup}").Kod);
            Obsahuje(JsonCteni.PrectiSoubor(vystup).Polozka("note").Text, "Složka skriptů nebyla nalezena");
        }

        [TestMethod]
        public void OpravaPrikazoveRadky()
        {
            CollectionAssert.AreEqual(new[] { @"C:\F\vstup.json", @"C:\F\vystup.json", @"C:\F\moje skripty" },
                PrikazovaRadka.OpravOdebraneUvozovky("\"C:\\Program Files\\spoustec.exe\" C:\\F\\vstup.json\" \"C:\\F\\vystup.json\" \"C:\\F\\moje skripty"));
            CollectionAssert.AreEqual(new[] { @"C:\F\vstup.json", @"C:\F\vystup.json" },
                PrikazovaRadka.OpravOdebraneUvozovky("spoustec.exe C:\\F\\vstup.json\" \"C:\\F\\vystup.json  "));
            //radky, ktere opravu nepotrebuji nebo ji nedovoli
            Assert.IsNull(PrikazovaRadka.OpravOdebraneUvozovky("\"spoustec.exe\" \"C:\\F\\vstup.json\" \"C:\\F\\vystup.json\""));
            Assert.IsNull(PrikazovaRadka.OpravOdebraneUvozovky("\"spoustec.exe\" C:\\F\\vstup.json"));
            Assert.IsNull(PrikazovaRadka.OpravOdebraneUvozovky("\"spoustec.exe\""));
            Assert.IsNull(PrikazovaRadka.OpravOdebraneUvozovky("\"spoustec.exe\" a\"b"));
            Assert.IsNull(PrikazovaRadka.OpravOdebraneUvozovky("spoustec.exe C:\\F\\vstup.json \"C:\\y z\\vystup.json\" C:\\skripty"));
        }

        [TestMethod]
        public void NavratoveKody()
        {
            string slozka = NovaSlozka();
            string skript = Path.Combine(Priklady, "ukazka");
            string vystup = Path.Combine(slozka, "vystup.json");
            Assert.AreEqual(Beh.KodParametry, Proces(slozka, Path.Combine(Priklady, "audio.json")).Kod);
            Assert.AreEqual(Beh.KodVstup, Proces(slozka, ZapisSoubor(slozka, "neplatny.json", "{"), vystup, skript).Kod);
            Assert.AreEqual(Beh.KodVstup, Proces(slozka, Path.Combine(slozka, "neni.json"), vystup, skript).Kod);
            string jinyFormat = Priklad("audio.json").Replace("fowler-exam", "jiny");
            Assert.AreEqual(Beh.KodFormat, Proces(slozka, ZapisSoubor(slozka, "jiny.json", jinyFormat), vystup, skript).Kod);
            Assert.AreEqual(Beh.KodZapis, Proces(slozka, Path.Combine(Priklady, "audio.json"), Path.Combine(slozka, "neni", "vystup.json"), skript).Kod);
            Assert.IsFalse(File.Exists(vystup));
        }

        [TestMethod]
        public void NekonecnaSmyckaSVychozimLimitem()
        {
            string slozka = NovaSlozka();
            string skript = Path.Combine(slozka, "skripty");
            Directory.CreateDirectory(skript);
            ZapisSoubor(skript, "audio.js", "export function hodnot(data) {\n  for (;;) {\n    data.x = 1;\n  }\n}\n");
            string vystup = Path.Combine(slozka, "vystup.json");
            VysledekProcesu p = Proces(slozka, Path.Combine(Priklady, "audio.json"), vystup, skript);
            Assert.AreEqual(Beh.KodOk, p.Kod);
            Assert.IsTrue(p.Doba.TotalSeconds < new Limity().CasSekund + 3, "trvalo " + p.Doba);
            Obsahuje(JsonCteni.PrectiSoubor(vystup).Polozka("note").Text, "překročen časový limit 5 s (nekonečná smyčka?) (audio.js, řádek 3)");
        }

        [TestMethod]
        public void TestovaciRezimSProtokolem()
        {
            string slozka = NovaSlozka();
            string text = "// synteticky skript testu: vrati pevnou hodnotu\nexport function hodnot(data) {\n  return { audio: { acAvg: { R: 12.34 } }, note: 'pevná hodnota' };\n}\n";
            string skript = Path.Combine(slozka, "konstanta");
            Directory.CreateDirectory(skript);
            ZapisSoubor(skript, "audio.js", text);
            string testy = Path.Combine(slozka, "testy");
            Directory.CreateDirectory(testy);
            ZapisSoubor(testy, "pripad01.json", Priklad("audio.json"));
            ZapisSoubor(testy, "pripad01.ocekavano.json", "{ \"audio\": { \"acAvg\": { \"R\": 12.340 } }, \"note\": \"neporovnává se\" }");
            Assert.AreEqual(Beh.KodOk, Proces(slozka, "--test", skript, testy).Kod);
            string protokol = Directory.GetFiles(testy, "protokol-konstanta-*.txt").Single();
            string obsah = File.ReadAllText(protokol, Encoding.UTF8);
            Obsahuje(obsah, "Složka skriptů:     " + skript);
            string otisk = SouborSkriptu.Otisk(Encoding.UTF8.GetBytes(text));
            Obsahuje(obsah, "  " + otisk + "  audio.js");
            Obsahuje(obsah, "audio.js: otisk " + SouborSkriptu.Otisk(Encoding.UTF8.GetBytes(otisk + "  audio.js\n")));
            Obsahuje(obsah, "[OK] pripad01");
            Obsahuje(obsah, "Jint 4.17.0");
            Obsahuje(obsah, "PROŠLO");
            File.Delete(protokol);

            ZapisSoubor(testy, "pripad02.json", Priklad("audio.json"));
            ZapisSoubor(testy, "pripad02.ocekavano.json", "{ \"audio\": { \"acAvg\": { \"R\": 12.3, \"L\": 40 } } }");
            ZapisSoubor(testy, "pripad03.json", Priklad("audio.json").Replace("fowler-exam", "jiny"));
            ZapisSoubor(testy, "pripad03.ocekavano.json", "{}");
            Assert.AreEqual(TestovaciRezim.KodNeproslo, Proces(slozka, "--test", skript, testy).Kod);
            obsah = File.ReadAllText(Directory.GetFiles(testy, "protokol-konstanta-*.txt").Single(), Encoding.UTF8);
            Obsahuje(obsah, "[OK] pripad01");
            Obsahuje(obsah, "[ROZDÍL] pripad02");
            Obsahuje(obsah, "ROZDÍL audio.acAvg.R: očekáváno 12.3, výsledek 12.34");
            Obsahuje(obsah, "ROZDÍL audio.acAvg.L: očekáváno 40, výsledek neurčeno");
            Obsahuje(obsah, "[CHYBA] pripad03");
            Obsahuje(obsah, "Případů: 3, OK: 1, ROZDÍL: 1, CHYBA: 1");
            Obsahuje(obsah, "NEPROŠLO");
        }

        [TestMethod]
        public void TestyUkazkyVPrikladechProjdou()
        {
            string testy = Path.Combine(Priklady, "testy");
            Assert.AreEqual(Beh.KodOk, Proces(null, "--test", Path.Combine(Priklady, "ukazka"), testy).Kod);
            foreach (string protokol in Directory.GetFiles(testy, "protokol-*.txt"))
                File.Delete(protokol);
        }
    }
}
