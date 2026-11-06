using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Spoustec.Tests.Pomocnik;

namespace Spoustec.Tests
{
    /// <summary>
    /// ukazkove slozky skriptu v priklady/ (sablona, ukazka) jsou soucasti releasu: musi bezet na vsech vzorovych
    /// datech a nesmi vracet zadne pole hodnoceni (spoustec ani jeho dokumentace nesmi nabizet hotove hodnoceni,
    /// README - Odpovednost)
    /// </summary>
    [TestClass]
    public sealed class PrikladyTesty
    {
        static readonly string[] SlozkySkriptu = { "sablona", "ukazka" };

        [TestMethod]
        public void UkazkyNicNehodnoti()
        {
            string[] vstupy = Directory.GetFiles(Priklady, "*.json");
            Assert.AreEqual(3, vstupy.Length);
            CollectionAssert.AreEquivalent(SlozkySkriptu, Directory.GetDirectories(Priklady).Select(Path.GetFileName).Where(n => n != "testy").ToArray());
            foreach (string slozka in SlozkySkriptu)
            {
                foreach (string vstup in vstupy)
                {
                    Beh.Zpracovani z = SpustSlozku(Path.Combine(Priklady, slozka), File.ReadAllText(vstup));
                    string pripad = slozka + " na " + Path.GetFileName(vstup);
                    Assert.IsFalse(z.Selhal, pripad + ": " + z.Chyba);
                    Assert.AreEqual(0, z.Kontrola.Casti.Polozky.Count, pripad + " vrací pole hodnocení");
                    Assert.AreEqual(0, z.Kontrola.PoznamkySpoustece.Count, pripad + ": " + z.Poznamka);
                    Assert.IsFalse(string.IsNullOrEmpty(z.Poznamka), pripad + " nevrací poznámku");
                }
            }
        }

        [TestMethod]
        public void SablonaBezPodskriptuNicNeurci()
        {
            Beh.Zpracovani z = SpustSlozku(Path.Combine(Priklady, "sablona"), Priklad("tympanometrie.json"));
            Assert.AreEqual("Šablona: hlavní skript tymp.js nemá zapojený žádný pod-skript, nic se neurčuje.", z.Poznamka);
        }

        [TestMethod]
        public void ZapojenaKostraPodskriptuNicNeurci()
        {
            //postup z docs/navod.md: zkopirovat sablonu, kostru pod-skriptu zapojit v hlavnim skriptu
            string slozka = NovaSlozka();
            foreach (string soubor in Directory.GetFiles(Path.Combine(Priklady, "sablona")))
                File.Copy(soubor, Path.Combine(slozka, Path.GetFileName(soubor)));
            File.Copy(Path.Combine(slozka, "sablona-podskriptu.js"), Path.Combine(slozka, "pole.js"));
            string puvodni = File.ReadAllText(Path.Combine(slozka, "audio.js"));
            string audio = puvodni
                .Replace("//   import { ztrataFowler } from \"./ztrata-fowler.js\";", "import { urciPole } from \"./pole.js\";")
                .Replace("  //   audio.fowler = ztrataFowler(data.audio, poznamky, data);", "  audio.fowler = urciPole(data.audio, poznamky, data);")
                .Replace("  poznamky.push(\"Šablona: hlavní skript audio.js nemá zapojený žádný pod-skript, nic se neurčuje.\");", "");
            //kdyz se sablona zmeni a nahrazeni se neprovedou, test na to upozorni
            Obsahuje(audio, "\nimport { urciPole } from \"./pole.js\";");
            Obsahuje(audio, "\n  audio.fowler = urciPole(data.audio, poznamky, data);");
            Neobsahuje(audio, "nemá zapojený");
            File.WriteAllText(Path.Combine(slozka, "audio.js"), audio);

            Beh.Zpracovani z = SpustSlozku(slozka);
            Assert.IsFalse(z.Selhal, z.Chyba);
            Assert.AreEqual("Kostra pod-skriptu: výpočet není doplněný, nic se neurčuje.", z.Poznamka);
            Assert.AreEqual(0, z.Kontrola.Casti.Polozky.Count);
            CollectionAssert.AreEqual(new[] { "audio.js", "pole.js", "cteni-dat.js" }, z.Beh("audio").BehSkriptu.Soubory.Select(s => s.Nazev).ToArray());
        }

        [TestMethod]
        public void UkazkaVypoctuJenDoPoznamky()
        {
            //ukazka-vypoctu.js pocita velicinu bez audiologickeho vyznamu, hlavni skript ji jen vypise do poznamky
            Beh.Zpracovani z = SpustSlozku(Path.Combine(Priklady, "ukazka"), Priklad("audio.json"));
            Assert.IsFalse(z.Selhal, z.Chyba);
            Assert.AreEqual(0, z.Kontrola.Casti.Polozky.Count);
            Obsahuje(z.Poznamka, "průměrná frekvence naměřených prahů vzduchu [kHz] {\"R\":2.04,\"L\":2.64,\"total\":2.36}.");
            CollectionAssert.Contains(z.Beh("audio").BehSkriptu.Soubory.Select(s => s.Nazev).ToList(), "ukazka-vypoctu.js");
        }

        [TestMethod]
        public void UkazkaVypoctuUchoBezPrahu()
        {
            //vpravo jen NR a nemereno: ucho se vynecha s duvodem, total se neurci; vlevo nemereno na 2000 Hz,
            //(1000 + 3000 + 6000) / 3 Hz = 3.333 kHz -> 3.33 (polovinu zaokrouhli vzorova data: 2.6375 -> 2.64)
            string vstup = "{\"fmt\":\"fowler-exam\",\"v\":1,\"app\":\"Fowler\",\"created\":\"2026-10-29T10:15:31\"," +
                "\"exam\":{\"time\":\"2026-10-28T09:42:00\"},\"audio\":{\"legacy\":false,\"hz\":[1000,2000,3000,6000]," +
                "\"R\":{\"ac\":[\"NR\",null,\"NR\",null]},\"L\":{\"ac\":[25,null,30,40]}}}";
            Beh.Zpracovani z = SpustSlozku(Path.Combine(Priklady, "ukazka"), vstup);
            Assert.IsFalse(z.Selhal, z.Chyba);
            Assert.AreEqual(0, z.Kontrola.Casti.Polozky.Count);
            Obsahuje(z.Poznamka, "Ukázka výpočtu vpravo: žádný naměřený práh vzduchu, nelze určit.");
            Obsahuje(z.Poznamka, "průměrná frekvence naměřených prahů vzduchu [kHz] {\"L\":3.33}.");
        }

        [TestMethod]
        public void CteniDatJeVSabloneAUkazceStejne()
        {
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(Priklady, "sablona", "cteni-dat.js")),
                File.ReadAllBytes(Path.Combine(Priklady, "ukazka", "cteni-dat.js")));
        }
    }
}
