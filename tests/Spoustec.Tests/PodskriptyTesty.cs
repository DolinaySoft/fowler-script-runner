using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spoustec.Json;
using Spoustec.Skript;
using static Spoustec.Tests.Pomocnik;

namespace Spoustec.Tests
{
    /// <summary>
    /// slozka skriptu: hlavni skript predane casti nacita pod-skripty (import) jen ze slozky skriptu; chyba
    /// v pod-skriptu se ohlasi s jeho nazvem a radkem; otisk v app zahrnuje vsechny nactene soubory
    /// </summary>
    [TestClass]
    public sealed class PodskriptyTesty
    {
        const string HlavniSPodskriptem =
            "import { prvni } from \"./pole/prvni.js\";\n" +
            "export function hodnot(data) {\n" +
            "  const poznamky = [];\n" +
            "  const audio = {};\n" +
            "  audio.acAvg = prvni(data.audio, poznamky, data);\n" +
            "  return { audio: audio, note: poznamky };\n" +
            "}\n";

        [TestMethod]
        public void PodskriptySeNactouZPodslozekAOtiskJeZahrnuje()
        {
            Beh.Zpracovani z = Spust(HlavniSPodskriptem, null, null,
                "pole/prvni.js", "import { pocet } from \"../spolecne.js\";\nexport function prvni(audio, poznamky) {\n  poznamky.push('frekvencí ' + pocet(audio.hz));\n  return { R: 12.5 };\n}\n",
                "spolecne.js", "export function pocet(seznam) { return seznam.length; }\n");
            Assert.IsFalse(z.Selhal, z.Chyba);
            Assert.AreEqual("12.5", Pole(JsonCteni.Precti(z.TextOdpovedi), "audio.acAvg.R").Cislo);
            Assert.AreEqual("frekvencí 10", z.Poznamka);
            VysledekSkriptu beh = z.Beh("audio").BehSkriptu;
            CollectionAssert.AreEqual(new[] { "audio.js", "pole/prvni.js", "spolecne.js" }, beh.Soubory.Select(s => s.Nazev).ToArray());
            Assert.AreEqual(SouborSkriptu.OtiskSady(beh.Soubory), z.Beh("audio").Otisk);
            Assert.AreEqual($"{Verze.Nazev}; audio.js #{z.Beh("audio").Otisk.Substring(0, 8)}", z.App);

            //zmena pod-skriptu zmeni otisk v app, i kdyz hlavni skript zustal stejny
            Beh.Zpracovani jiny = Spust(HlavniSPodskriptem, null, null,
                "pole/prvni.js", "import { pocet } from \"../spolecne.js\";\nexport function prvni(audio, poznamky) {\n  poznamky.push('frekvencí ' + pocet(audio.hz));\n  return { R: 12.5 };\n}\n",
                "spolecne.js", "export function pocet(seznam) { return seznam.length + 0; }\n");
            Assert.AreEqual(z.Beh("audio").BehSkriptu.Soubory[0].Sha256, jiny.Beh("audio").BehSkriptu.Soubory[0].Sha256);
            Assert.AreNotEqual(z.App, jiny.App);
        }

        [TestMethod]
        public void ChybaVPodskriptuUvadiJehoNazevARadek()
        {
            Beh.Zpracovani z = Spust(HlavniSPodskriptem, null, null,
                "pole/prvni.js", "export function prvni(audio, poznamky) {\n  const ucho = audio.X;\n  return { R: ucho.ac[0] };\n}\n");
            Assert.AreEqual("Skript selhal: TypeError: Cannot read properties of undefined (reading 'ac') (pole/prvni.js, řádek 3)", z.Chyba);
            Assert.IsNull(JsonCteni.Precti(z.TextOdpovedi).Polozka("audio"));

            z = Spust(HlavniSPodskriptem, null, null, "pole/prvni.js", "export function prvni(audio, poznamky) {\n  return { R: ;\n}\n");
            Obsahuje(z.Chyba, "Skript selhal: SyntaxError: Unexpected token");
            Obsahuje(z.Chyba, "(pole/prvni.js, řádek 2)");
            Neobsahuje(z.Chyba, Path.GetTempPath());

            z = Spust(HlavniSPodskriptem, null, null, "pole/prvni.js", "export function druhy() { return null; }\n");
            Obsahuje(z.Chyba, "SyntaxError");
            Obsahuje(z.Chyba, "prvni");
        }

        [TestMethod]
        public void ChybejiciPodskript()
        {
            Beh.Zpracovani z = Spust("// hlavni skript\nimport { who } from \"./who.js\";\nexport function hodnot(data) { return null; }\n");
            Assert.AreEqual("Skript selhal: import \"./who.js\": skript who.js ve složce skriptů není (audio.js, řádek 2)", z.Chyba);
            Assert.AreEqual("audio.js", z.Beh("audio").BehSkriptu.Soubory.Single().Nazev);
        }

        [TestMethod]
        public void SkriptyMimoSlozkuNelzeNacist()
        {
            string venku = NovaSlozka();
            string soubor = ZapisSoubor(venku, "venku.js", "export const x = 1;\n");
            string relativne = "../" + Path.GetFileName(venku) + "/venku.js";
            Obsahuje(Spust("import { x } from \"" + relativne + "\";\nexport function hodnot(data) { return null; }\n").Chyba,
                "mimo složku skriptů nelze načíst (audio.js, řádek 1)");
            Obsahuje(Spust("import { x } from \"" + soubor.Replace('\\', '/') + "\";\nexport function hodnot(data) { return null; }\n").Chyba,
                "cestou ve složce skriptů, která začíná ./");
            Obsahuje(Spust("import x from \"lodash\";\nexport function hodnot(data) { return null; }\n").Chyba,
                "cestou ve složce skriptů, která začíná ./");
            Obsahuje(Spust("import x from \"./data.json\" with { type: \"json\" };\nexport function hodnot(data) { return null; }\n", null, null,
                "data.json", "{}").Chyba, "jen skript s příponou .js");
            //podslozka smi nacist soubor o uroven vys, kdyz je stale ve slozce skriptu
            Assert.IsFalse(Spust(HlavniSPodskriptem, null, null,
                "pole/prvni.js", "import { y } from \"../y.js\";\nexport function prvni() { return { R: y }; }\n",
                "y.js", "export const y = 1;\n").Selhal);
        }

        [TestMethod]
        public void NekonecnaSmyckaVPodskriptuSkonciVLimituSRadkem()
        {
            Stopwatch stopky = Stopwatch.StartNew();
            Beh.Zpracovani z = Spust(HlavniSPodskriptem, null, new Limity { CasSekund = 1 },
                "pole/prvni.js", "export function prvni(audio) {\n  let i = 0;\n  while (true) {\n    i++;\n  }\n}\n");
            Assert.IsTrue(stopky.Elapsed.TotalSeconds < 3, "trvalo " + stopky.Elapsed);
            Obsahuje(z.Chyba, "překročen časový limit 1 s (nekonečná smyčka?) (pole/prvni.js, řádek 4)");
        }

        [TestMethod]
        public void SlozkaSkriptuSMezeramiADiakritikou()
        {
            string slozka = Path.Combine(NovaSlozka(), "Moje skripty č. 1");
            Directory.CreateDirectory(Path.Combine(slozka, "pod složka"));
            ZapisSoubor(slozka, "audio.js", "import { r } from \"./pod složka/hodnota.js\";\nexport function hodnot(data) { return { audio: { acAvg: { R: r } } }; }\n");
            ZapisSoubor(Path.Combine(slozka, "pod složka"), "hodnota.js", "export const r = 20;\n");
            Beh.Zpracovani z = SpustSlozku(slozka);
            Assert.IsFalse(z.Selhal, z.Chyba);
            CollectionAssert.AreEqual(new[] { "audio.js", "pod složka/hodnota.js" }, z.Beh("audio").BehSkriptu.Soubory.Select(s => s.Nazev).ToArray());
        }

        [TestMethod]
        public void KazdaCastMaSvujHlavniSkript()
        {
            //Fowler predava jednu cast; kdyby data obsahovala vic casti, spusti se hlavni skript kazde z nich
            JsonHodnota vstup = JsonCteni.Precti(Priklad("audio.json"));
            vstup.Nastav("tymp", JsonCteni.Precti(Priklad("tympanometrie.json")).Polozka("tymp"));
            string slozka = NovaSlozka();
            ZapisSoubor(slozka, "audio.js", "export function hodnot(data) { return { audio: { acAvg: { R: 10 } }, tymp: { R: { type: 'A' } }, note: 'audio' }; }\n");
            ZapisSoubor(slozka, "tymp.js", "export function hodnot(data) { return { tymp: { L: { type: 'B' } }, note: 'tymp' }; }\n");
            Beh.Zpracovani z = SpustSlozku(slozka, JsonZapis.Zapis(vstup, false));
            JsonHodnota odpoved = JsonCteni.Precti(z.TextOdpovedi);
            Assert.AreEqual("10", Pole(odpoved, "audio.acAvg.R").Cislo);
            Assert.AreEqual("B", Pole(odpoved, "tymp.L.type").Text);
            //hlavni skript urcuje jen svou cast
            Assert.IsNull(Pole(odpoved, "tymp.R"));
            Obsahuje(z.Poznamka, "tymp vynecháno");
            Obsahuje(z.App, "; audio.js #");
            Obsahuje(z.App, ", tymp.js #");
        }

        [TestMethod]
        public void PoleSHodnotouNullNeboPrazdneSeVynechajiBezUpozorneni()
        {
            Beh.Zpracovani z = Vrat("{ audio: { fowler: null, acAvg: { R: 20, L: null }, who: null }, note: [] }");
            Assert.AreEqual(0, z.Kontrola.PoznamkySpoustece.Count, z.Poznamka);
            Assert.AreEqual("{\"acAvg\":{\"R\":20}}", JsonZapis.Zapis(JsonCteni.Precti(z.TextOdpovedi).Polozka("audio"), false));

            z = Vrat("{ tymp: { R: { type: null }, L: {} } }", Priklad("tympanometrie.json"));
            Assert.AreEqual(0, z.Kontrola.PoznamkySpoustece.Count, z.Poznamka);
            Assert.IsNull(JsonCteni.Precti(z.TextOdpovedi).Polozka("tymp"));

            z = Vrat("{ speech: { srt: { R: null }, aided: { srt: {} } } }", Priklad("slovni.json"));
            Assert.AreEqual(0, z.Kontrola.PoznamkySpoustece.Count, z.Poznamka);
            Assert.IsNull(JsonCteni.Precti(z.TextOdpovedi).Polozka("speech"));
        }
    }
}
