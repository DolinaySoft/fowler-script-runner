using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spoustec.Format;
using Spoustec.Json;
using static Spoustec.Tests.Pomocnik;

namespace Spoustec.Tests
{
    /// <summary>
    /// formalni kontrola vysledku: neplatne pole se vynecha s poznamkou (nic se neopravuje ani nezaokrouhluje),
    /// ostatni platna pole se zapisou; skripty vraci synteticke konstanty
    /// </summary>
    [TestClass]
    public sealed class KontrolaTesty
    {
        static Beh.Zpracovani Audio(string audioJs)
        {
            return Vrat("{ audio: " + audioJs + " }");
        }

        static string Cislo(Beh.Zpracovani z, string cesta)
        {
            JsonHodnota h = Pole(z.Odpoved, cesta);
            return (h != null && h.Typ == TypJson.Cislo) ? h.Cislo : null;
        }

        static string Text(Beh.Zpracovani z, string cesta)
        {
            JsonHodnota h = Pole(z.Odpoved, cesta);
            return (h != null && h.Typ == TypJson.Text) ? h.Text : null;
        }

        [TestMethod]
        public void PlatnaPoleAudiaSeZapisouBezeZmeny()
        {
            Beh.Zpracovani z = Audio("{ fowler: { R: 12.5, L: 30, total: 20.1 }, acAvg: { R: 12.34, L: -10, total: 130 }, " +
                "bcAvg: { R: 0 }, who: 'text A', group: '  text B ', sisi: { R: 'text C' } }");
            Assert.IsFalse(z.Selhal, z.Chyba);
            Assert.AreEqual(0, z.Kontrola.PoznamkySpoustece.Count, string.Join("\n", z.Kontrola.PoznamkySpoustece));
            Assert.AreEqual("12.5", Cislo(z, "audio.fowler.R"));
            Assert.AreEqual("30", Cislo(z, "audio.fowler.L"));
            Assert.AreEqual("20.1", Cislo(z, "audio.fowler.total"));
            Assert.AreEqual("12.34", Cislo(z, "audio.acAvg.R"));
            Assert.AreEqual("-10", Cislo(z, "audio.acAvg.L"));
            Assert.AreEqual("130", Cislo(z, "audio.acAvg.total"));
            Assert.AreEqual("0", Cislo(z, "audio.bcAvg.R"));
            Assert.AreEqual("text A", Text(z, "audio.who"));
            Assert.AreEqual("  text B ", Text(z, "audio.group"));
            Assert.AreEqual("text C", Text(z, "audio.sisi.R"));
            Assert.IsNull(z.Poznamka);
        }

        [TestMethod]
        public void NeplatnaCislaSeVynechajiSPoznamkou()
        {
            Beh.Zpracovani z = Audio("{ acAvg: { R: NaN, L: '25', total: 131 }, bcAvg: { R: 33.333, L: 0.1 + 0.2, total: 12.5 }, " +
                "fowler: { R: 100.25, L: 1 / 0, total: undefined } }");
            Assert.IsNull(Pole(z.Odpoved, "audio.acAvg"));
            Assert.IsNull(Cislo(z, "audio.bcAvg.R"));
            Assert.IsNull(Cislo(z, "audio.bcAvg.L"));
            Assert.AreEqual("12.5", Cislo(z, "audio.bcAvg.total"));
            Assert.IsNull(Pole(z.Odpoved, "audio.fowler"));
            string poznamka = z.Poznamka;
            Obsahuje(poznamka, "audio.acAvg.R vynecháno: hodnota je NaN");
            Obsahuje(poznamka, "audio.acAvg.L vynecháno: očekáváno číslo, skript vrátil text \"25\"");
            Obsahuje(poznamka, "audio.acAvg.total vynecháno: 131 je mimo rozsah pole -10 až 130");
            Obsahuje(poznamka, "audio.bcAvg.R vynecháno: 33.333 má víc desetinných míst, než pole dovoluje (nejvýše 2) – zaokrouhlete ve skriptu");
            Obsahuje(poznamka, "audio.bcAvg.L vynecháno: 0.30000000000000004 má víc desetinných míst");
            Obsahuje(poznamka, "audio.fowler.R vynecháno: 100.25 je mimo rozsah pole 0 až 100");
            Obsahuje(poznamka, "audio.fowler.L vynecháno: hodnota je Infinity");
            Obsahuje(poznamka, "audio.fowler.total vynecháno: hodnota je undefined");
            //nic se nezaokrouhlilo: v zapsane casti zustala jen platna hodnota
            Assert.AreEqual("{\"bcAvg\":{\"total\":12.5}}", JsonZapis.Zapis(Pole(z.Odpoved, "audio"), false));

            z = Audio("{ acAvg: { R: 100.25, L: 100.5 } }");
            Obsahuje(z.Poznamka, "audio.acAvg.R vynecháno: 100.25 má 6 znaků, pole pojme nejvýše 5");
            Assert.AreEqual("100.5", Cislo(z, "audio.acAvg.L"));
        }

        [TestMethod]
        public void NullNebolChybejiciUdajSeVynechaBezPoznamky()
        {
            Beh.Zpracovani z = Audio("{ acAvg: { R: null, L: 20 }, who: null, sisi: null }");
            Assert.AreEqual("20", Cislo(z, "audio.acAvg.L"));
            Assert.IsNull(Pole(z.Odpoved, "audio.acAvg.R"));
            Assert.AreEqual(0, z.Kontrola.PoznamkySpoustece.Count);
        }

        [TestMethod]
        public void NeznamyKlicAJinaCast()
        {
            Beh.Zpracovani z = Vrat("{ audio: { acAVG: { R: 20 }, acAvg: { R: 21, X: 1 } }, tymp: { R: { type: 'A' } }, hodnoceni: 1 }");
            Assert.AreEqual("21", Cislo(z, "audio.acAvg.R"));
            Assert.IsNull(Pole(z.Odpoved, "tymp"));
            Obsahuje(z.Poznamka, "audio.acAVG vynecháno: neznámý klíč (myslíte acAvg?)");
            Obsahuje(z.Poznamka, "audio.acAvg.X vynecháno: neznámý klíč (povolené: R, L, total)");
            Obsahuje(z.Poznamka, "tymp vynecháno: Fowler předal část audio");
            Obsahuje(z.Poznamka, "hodnoceni vynecháno: neznámý klíč");
        }

        [TestMethod]
        public void NeplatneTexty()
        {
            string dlouhy = new string('x', 101);
            Beh.Zpracovani z = Audio("{ who: 'a\\nb', group: '   ', sisi: { R: '" + dlouhy + "', L: '" + new string('y', 100) + "' } }");
            Assert.IsNull(Pole(z.Odpoved, "audio.who"));
            Assert.IsNull(Pole(z.Odpoved, "audio.group"));
            Assert.IsNull(Pole(z.Odpoved, "audio.sisi.R"));
            Assert.AreEqual(100, Text(z, "audio.sisi.L").Length);
            Obsahuje(z.Poznamka, "audio.who vynecháno: text obsahuje řídicí znak");
            Obsahuje(z.Poznamka, "audio.group vynecháno: prázdný text");
            Obsahuje(z.Poznamka, "audio.sisi.R vynecháno: text má 101 znaků, pole pojme nejvýše 100");
        }

        [TestMethod]
        public void HodnotyKtereJsonNema()
        {
            Beh.Zpracovani z = Audio("{ who: function () { return 'A'; }, group: new Date(0), acAvg: { get R() { return 20; } }, " +
                "sisi: { R: Symbol('x') } }");
            Assert.IsNull(Pole(z.Odpoved, "audio"));
            Obsahuje(z.Poznamka, "audio.who vynecháno: očekáván text, skript vrátil funkce");
            Obsahuje(z.Poznamka, "audio.group vynecháno: očekáván text, skript vrátil datum (Date)");
            Obsahuje(z.Poznamka, "audio.acAvg.R vynecháno: očekáváno číslo, skript vrátil vlastnost s getterem (get)");
            Obsahuje(z.Poznamka, "audio.sisi.R vynecháno: očekáván text, skript vrátil Symbol");

            //cyklicky odkaz se neprochazi do nekonecna
            z = Vrat("(function () { var o = {}; o.R = o; return { audio: { acAvg: o } }; })()");
            Obsahuje(z.Poznamka, "audio.acAvg.R vynecháno: očekáváno číslo, skript vrátil cyklický odkaz");
        }

        [TestMethod]
        public void MinusNulaJeNula()
        {
            Assert.AreEqual("0", Cislo(Audio("{ acAvg: { R: -0 } }"), "audio.acAvg.R"));
        }

        [TestMethod]
        public void VysledekNeniObjekt()
        {
            Beh.Zpracovani z = Vrat("42");
            Obsahuje(z.Poznamka, "Výsledek funkce hodnot vynechán: očekáván objekt");
            z = Vrat("null");
            Assert.IsNull(z.Poznamka);
            Assert.AreEqual(0, z.Kontrola.Casti.Polozky.Count);
            z = Vrat("undefined");
            Assert.IsNull(z.Poznamka);
        }

        [TestMethod]
        public void Tympanometrie()
        {
            Beh.Zpracovani z = Vrat("{ tymp: { R: { type: 'A', " +
                "ipsi: [ { stim: 500, res: '+' }, { stim: 1000, res: '-' }, { stim: 1000, res: '+' }, { stim: 'WB', res: '-' }, " +
                "        { stim: 2000 }, { stim: 2000.5, res: '+' }, { stim: 'XX', res: '+' }, { stim: 4000, res: 'ano' }, , 7 ], " +
                "decayIpsi: [ { stim: 500, pct: -20 }, { stim: 1000, pct: '-' }, { stim: 2000, pct: 2.5 }, { stim: 4000, pct: null } ] }, " +
                "L: { type: 'ABCDEFGHIJK', contra: 'x' } } }", Priklad("tympanometrie.json"));
            JsonHodnota ipsi = Pole(z.Odpoved, "tymp.R.ipsi");
            Assert.AreEqual("[{\"stim\":500,\"res\":\"+\"},{\"stim\":\"WB\",\"res\":\"-\"}]", JsonZapis.Zapis(ipsi, false));
            Assert.AreEqual("[{\"stim\":500,\"pct\":-20},{\"stim\":1000,\"pct\":\"-\"}]", JsonZapis.Zapis(Pole(z.Odpoved, "tymp.R.decayIpsi"), false));
            Assert.AreEqual("A", Text(z, "tymp.R.type"));
            Assert.IsNull(Pole(z.Odpoved, "tymp.L"));
            string p = z.Poznamka;
            Obsahuje(p, "tymp.R.ipsi vynecháno: podnět 1000 Hz je v seznamu 2×, vynechány všechny jeho položky");
            Obsahuje(p, "tymp.R.ipsi[5] vynecháno: stim musí být frekvence v Hz");
            Obsahuje(p, "tymp.R.ipsi[6] vynecháno: stim musí být");
            Obsahuje(p, "tymp.R.ipsi[7] vynecháno: res musí být \"+\" nebo \"-\", skript vrátil text \"ano\"");
            Obsahuje(p, "tymp.R.ipsi[8] vynecháno: očekáván objekt, skript vrátil prázdné místo v seznamu");
            Obsahuje(p, "tymp.R.ipsi[9] vynecháno: očekáván objekt, skript vrátil 7");
            Obsahuje(p, "tymp.R.decayIpsi[2] vynecháno: pct musí být celé číslo");
            Obsahuje(p, "tymp.L.type vynecháno: text má 11 znaků, pole pojme nejvýše 10");
            Obsahuje(p, "tymp.L.contra vynecháno: očekáván seznam");
            Neobsahuje(p, "ipsi[4]"); //podnet bez vysledku se vynecha bez poznamky
            Neobsahuje(p, "decayIpsi[3]");
        }

        [TestMethod]
        public void SlovniAudiometrie()
        {
            Beh.Zpracovani z = Vrat("{ speech: { srt: { R: 35.5, L: 35.55, bin: 121 }, aided: { srt: { R: 20, L: -10 } } } }", Priklad("slovni.json"));
            Assert.AreEqual("35.5", Cislo(z, "speech.srt.R"));
            Assert.IsNull(Pole(z.Odpoved, "speech.srt.L"));
            Assert.IsNull(Pole(z.Odpoved, "speech.srt.bin"));
            Assert.AreEqual("20", Cislo(z, "speech.aided.srt.R"));
            Assert.AreEqual("-10", Cislo(z, "speech.aided.srt.L"));
            Obsahuje(z.Poznamka, "speech.srt.L vynecháno: 35.55 má víc desetinných míst, než pole dovoluje (nejvýše 1)");
            Obsahuje(z.Poznamka, "speech.srt.bin vynecháno: 121 je mimo rozsah pole -10 až 120");
        }

        [TestMethod]
        public void PoznamkaSkriptuAKontroly()
        {
            Beh.Zpracovani z = Vrat("{ note: ['první', 'druhá', 3], audio: { acAvg: { R: 'x' } } }");
            Assert.AreEqual("první\ndruhá\n" + Odpoved.NadpisKontroly + "\n- note[2] vynecháno: očekáván text, skript vrátil 3.\n" +
                "- audio.acAvg.R vynecháno: očekáváno číslo, skript vrátil text \"x\".", z.Poznamka);
            Assert.AreEqual(z.Poznamka, Pole(z.Odpoved, "note").Text);
        }

        [TestMethod]
        public void PoznamkaSeZkratiNa2000ZnakuAUpozorneniKontrolyZustanou()
        {
            Beh.Zpracovani z = Vrat("{ note: 'x'.repeat(5000), audio: { acAvg: { R: 33.333 } } }");
            Assert.AreEqual(Odpoved.MaxZnakuPoznamky, z.Poznamka.Length);
            Obsahuje(z.Poznamka, "audio.acAvg.R vynecháno: 33.333");
            Assert.IsTrue(z.Poznamka.StartsWith("xxxx"));
        }

        [TestMethod]
        public void OdpovedMaHlavickuAIdentifikaci()
        {
            Beh.Zpracovani z = Audio("{ acAvg: { R: 20 } }");
            JsonHodnota o = JsonCteni.Precti(z.TextOdpovedi);
            CollectionAssert.AreEqual(new[] { "fmt", "v", "app", "created", "patient", "exam", "audio" }, o.Polozky.Select(p => p.Key).ToArray());
            Assert.AreEqual("fowler-eval", o.Polozka("fmt").Text);
            Assert.AreEqual("1", o.Polozka("v").Cislo);
            Assert.AreEqual("4F0C1E2A-7B3D-4C55-9E21-0A6B3D9F1C77", Pole(o, "patient.pid").Text);
            Assert.AreEqual("5704150000", Pole(o, "patient.rc").Text);
            Assert.AreEqual("2026-10-28T09:42:00", Pole(o, "exam.time").Text);
        }
    }
}
