using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spoustec.Format;
using Spoustec.Json;
using Spoustec.Skript;
using static Spoustec.Tests.Pomocnik;

namespace Spoustec.Tests
{
    /// <summary>beh skriptu: data, izolace, chyby a limity; chyba skriptu = odpoved bez hodnoceni s poznamkou</summary>
    [TestClass]
    public sealed class SkriptTesty
    {
        [TestMethod]
        public void SkriptCteniPacientaDostaneUndefined()
        {
            Beh.Zpracovani z = Spust("export function hodnot(data) {\n  return { note: typeof data.patient + ' ' + JSON.stringify(data).indexOf('5704150000') };\n}");
            Assert.IsFalse(z.Selhal, z.Chyba);
            Assert.AreEqual("undefined -1", z.Poznamka);
        }

        [TestMethod]
        public void SkriptDostaneDataJakoObjektyJavaScriptu()
        {
            Beh.Zpracovani z = Spust("export function hodnot(data) {\n" +
                "  var ac = data.audio.R.ac;\n" +
                "  return { note: [Array.isArray(ac), ac.length, ac[9], ac[0] === 15, data.audio.R.bc[0] === null, data.exam.age.y, data.fmt].join(' ') };\n}");
            Assert.AreEqual("true 10 NR true true 69 fowler-exam", z.Poznamka);
        }

        [TestMethod]
        public void BezPristupuKClr()
        {
            Beh.Zpracovani z = Spust("export function hodnot(data) {\n" +
                "  return { note: [typeof System, typeof importNamespace, typeof require, typeof clrHelper, typeof process, typeof window].join(' ') };\n}");
            Assert.AreEqual("undefined undefined undefined undefined undefined undefined", z.Poznamka);
        }

        [TestMethod]
        public void StrictMode()
        {
            Beh.Zpracovani z = Spust("export function hodnot(data) {\n  prumer = 5;\n  return null;\n}");
            Obsahuje(z.Chyba, "ReferenceError");
            Obsahuje(z.Chyba, "(audio.js, řádek 2)");
        }

        [TestMethod]
        public void SyntaktickaChybaMaRadek()
        {
            Beh.Zpracovani z = Spust("export function hodnot(data) {\n  var a = 1;\n  return { audio: { acAvg: { R: 1 } };\n}\n");
            Assert.IsTrue(z.Selhal);
            Obsahuje(z.Chyba, "Skript selhal: SyntaxError");
            Obsahuje(z.Chyba, "(audio.js, řádek 3)");
            OdpovedBezHodnoceni(z);
        }

        [TestMethod]
        public void VyjimkaMaRadek()
        {
            Beh.Zpracovani z = Spust("export function hodnot(data) {\n  var x = data.audio;\n  return { note: x.neni.R };\n}");
            Obsahuje(z.Chyba, "Skript selhal: TypeError");
            Obsahuje(z.Chyba, "(audio.js, řádek 3)");
            OdpovedBezHodnoceni(z);

            z = Spust("export function hodnot(data) {\n\n  throw new Error('vlastní chyba');\n}");
            Obsahuje(z.Chyba, "Error: vlastní chyba (audio.js, řádek 3)");
        }

        [TestMethod]
        public void ChybiFunkceHodnot()
        {
            Beh.Zpracovani z = Spust("export function hodnoty(data) { return null; }");
            Assert.AreEqual("Skript selhal: hlavní skript neexportuje funkci hodnot (musí obsahovat export function hodnot(data) { ... }) (audio.js)", z.Chyba);
            OdpovedBezHodnoceni(z);
            //funkce bez export neni z hlavniho skriptu videt
            Obsahuje(Spust("function hodnot(data) { return null; }").Chyba, "neexportuje funkci hodnot");
            Obsahuje(Spust("export var hodnot = 5;").Chyba, "hodnot není funkce");
            Assert.IsFalse(Spust("export const hodnot = (data) => null;").Selhal);
        }

        [TestMethod]
        public void NekonecnaSmyckaSkonciVLimituSRadkem()
        {
            Stopwatch stopky = Stopwatch.StartNew();
            Beh.Zpracovani z = Spust("export function hodnot(data) {\n  var i = 0;\n  while (true) {\n    i++;\n  }\n}", null, new Limity { CasSekund = 1 });
            Assert.IsTrue(stopky.Elapsed.TotalSeconds < 3, "trvalo " + stopky.Elapsed);
            Obsahuje(z.Chyba, "překročen časový limit 1 s");
            Obsahuje(z.Chyba, "(audio.js, řádek 4)");
            OdpovedBezHodnoceni(z);
        }

        [TestMethod]
        public void VelkaAlokacePametiSkonciVLimituSRadkem()
        {
            Stopwatch stopky = Stopwatch.StartNew();
            Beh.Zpracovani z = Spust("export function hodnot(data) {\n  var a = [];\n  while (true) {\n    a.push(new Array(1000).fill(1.5));\n  }\n}");
            Assert.IsTrue(stopky.Elapsed.TotalSeconds < 5, "trvalo " + stopky.Elapsed);
            Obsahuje(z.Chyba, "překročen limit paměti 64 MB");
            Obsahuje(z.Chyba, "(audio.js, řádek 4)");
            OdpovedBezHodnoceni(z);

            z = Spust("export function hodnot(data) {\n  var a = new Array(9000000);\n  a.fill(1.5);\n  return null;\n}");
            Obsahuje(z.Chyba, "limit paměti");
        }

        [TestMethod]
        public void BeznySkriptSDocasnymiHodnotamiLimitPametiNeprekroci()
        {
            //vytvori mnohem vic docasnych dat nez 64 MB, ale nic si nedrzi
            Beh.Zpracovani z = Spust("export function hodnot(data) {\n  var n = 0;\n  for (var i = 0; i < 300000; i++) {\n" +
                "    var t = JSON.stringify(data.audio) + i;\n    n += t.length > 0 ? 1 : 0;\n  }\n  return { note: 'n=' + n };\n}",
                null, new Limity { CasSekund = 30 });
            Assert.IsFalse(z.Selhal, z.Chyba);
            Assert.AreEqual("n=300000", z.Poznamka);
        }

        [TestMethod]
        public void NekonecnaRekurze()
        {
            Beh.Zpracovani z = Spust("function f(n) { return f(n + 1) + 1; }\nexport function hodnot(data) {\n  return f(0);\n}");
            Obsahuje(z.Chyba, "hloubka vnoření volání funkcí 256");
            Obsahuje(z.Chyba, "funkce f");
            OdpovedBezHodnoceni(z);
        }

        [TestMethod]
        public void RegularniVyrazSkonciVLimitu()
        {
            Stopwatch stopky = Stopwatch.StartNew();
            Beh.Zpracovani z = Spust("export function hodnot(data) {\n  return { note: String(/^(a+)+$/.test('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!')) };\n}", null, new Limity { CasSekund = 1 });
            Assert.IsTrue(stopky.Elapsed.TotalSeconds < 4, "trvalo " + stopky.Elapsed);
            Assert.IsTrue(z.Selhal);
        }

        [TestMethod]
        public void AsyncFunkceNeniPodporovana()
        {
            Obsahuje(Spust("export async function hodnot(data) { return null; }").Chyba, "Promise");
        }

        [TestMethod]
        public void ChybySlozkyAHlavnihoSkriptu()
        {
            //slozka neni: odpoved bez hodnoceni s poznamkou
            string slozka = NovaSlozka();
            Beh.Zpracovani z = SpustSlozku(Path.Combine(slozka, "neni"));
            Obsahuje(z.Chyba, "Složka skriptů nebyla nalezena");
            Assert.AreEqual(Verze.Nazev + "; audio.js #--------", z.App);
            OdpovedBezHodnoceni(z);

            //misto slozky soubor
            Obsahuje(SpustSlozku(ZapisSoubor(slozka, "hodnoceni.js", "")).Chyba, "Místo složky skriptů je zadaný soubor");

            //pro predanou cast neni hlavni skript (pracoviste ji nehodnoti): nic se neurci, duvod v poznamce
            ZapisSoubor(slozka, "audio.js", "export function hodnot(data) { return { audio: { acAvg: { R: 10 } } }; }");
            z = SpustSlozku(slozka, Priklad("tympanometrie.json"));
            Assert.AreEqual("Pro část tymp (tympanometrie) není ve složce skriptů hlavní skript tymp.js, nic se neurčuje " +
                $"(složka {slozka}).", z.Poznamka);
            Assert.AreEqual(Verze.Nazev + "; tymp.js #--------", z.App);
            Assert.IsNull(JsonCteni.Precti(z.TextOdpovedi).Polozka("tymp"));

            //hlavni skript v jinem kodovani nez UTF-8, s BOM
            File.WriteAllText(Path.Combine(slozka, "audio.js"), "// poznámka\nexport function hodnot(data) { return null; }", Encoding.GetEncoding(1250));
            z = SpustSlozku(slozka);
            Obsahuje(z.Chyba, "UTF-8");
            Obsahuje(z.Chyba, "(audio.js)");
            File.WriteAllText(Path.Combine(slozka, "audio.js"), "export function hodnot(data) { return { note: 'čeština' }; }", new UTF8Encoding(true));
            Assert.AreEqual("čeština", SpustSlozku(slozka).Poznamka);
        }

        [TestMethod]
        public void AppObsahujeVerziNazevAOtiskSkriptu()
        {
            string text = "export function hodnot(data) { return null; }";
            Beh.Zpracovani z = Spust(text);
            string otisk = SouborSkriptu.Otisk(Encoding.UTF8.GetBytes(text));
            //otisk = SHA-256 vypisu ve tvaru sha256sum (radek "SHA-256  nazev" pro kazdy nacteny soubor)
            string sada = SouborSkriptu.Otisk(Encoding.UTF8.GetBytes(otisk + "  audio.js\n"));
            Assert.AreEqual($"{Verze.Nazev}; audio.js #{sada.Substring(0, 8)}", z.App);
            Assert.AreEqual(sada, z.Beh("audio").Otisk);
            Assert.AreEqual(otisk, z.Beh("audio").BehSkriptu.Soubory.Single().Sha256);
        }

        [TestMethod]
        public void KonzoleSeZachyti()
        {
            Beh.Zpracovani z = Spust("export function hodnot(data) {\n  console.log('a', 1, {x: [1, 2]}, null, undefined);\n  console.warn('w');\n" +
                "  for (var i = 0; i < 500; i++) console.log(i);\n  return null;\n}");
            List<string> konzole = z.Beh("audio").BehSkriptu.Konzole;
            Assert.AreEqual("a 1 {\"x\":[1,2]} null undefined", konzole[0]);
            Assert.AreEqual("varování: w", konzole[1]);
            Assert.AreEqual(201, konzole.Count);
            Obsahuje(konzole.Last(), "vynechán");
        }

        static void OdpovedBezHodnoceni(Beh.Zpracovani z)
        {
            JsonHodnota odpoved = JsonCteni.Precti(z.TextOdpovedi);
            Assert.AreEqual("fowler-eval", odpoved.Polozka("fmt").Text);
            Assert.IsNull(odpoved.Polozka("audio"));
            Assert.AreEqual(z.Chyba, odpoved.Polozka("note").Text);
        }
    }
}
