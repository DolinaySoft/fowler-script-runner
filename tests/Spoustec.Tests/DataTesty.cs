using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spoustec.Format;
using Spoustec.Json;
using static Spoustec.Tests.Pomocnik;

namespace Spoustec.Tests
{
    /// <summary>cteni a zapis JSON, nacteni predanych dat a odebrani pacienta</summary>
    [TestClass]
    public sealed class DataTesty
    {
        [TestMethod]
        public void JsonZachovaZapisCiselAPoradiKlicu()
        {
            string json = "{\"b\":1.50,\"a\":[1e2,-0.0,12],\"c\":\"xé\\n\",\"d\":null,\"e\":true}";
            Assert.AreEqual(json, JsonZapis.Zapis(JsonCteni.Precti(json), false));
        }

        [TestMethod]
        public void JsonOdmitneNeplatnyZapis()
        {
            foreach (string json in new[] { "{\"a\":1,}", "{'a':1}", "{\"a\":NaN}", "{\"a\":01}", "{\"a\":\"x\ny\"}", "[1 2]", "{\"a\":1} x", "" })
            {
                try
                {
                    JsonCteni.Precti(json);
                    Assert.Fail("Neodmítnuto: " + json);
                }
                catch (ChybaJsonException)
                {
                }
            }
        }

        [TestMethod]
        public void JsonOdmitneOpakovanyKlic()
        {
            try
            {
                JsonCteni.Precti("{\"patient\":{},\"patient\":{}}");
                Assert.Fail("opakovaný klíč nebyl odmítnut");
            }
            catch (ChybaJsonException ex)
            {
                Obsahuje(ex.Message, "vícekrát");
            }
        }

        [TestMethod]
        public void JsonZapiseOsamocenouPolovinuZnakuJakoEscape()
        {
            Assert.AreEqual("\"a\\ud800b\"", JsonZapis.Zapis(JsonHodnota.Retezec("a\ud800b"), false));
        }

        [TestMethod]
        public void IdentifikaceZustanePresneJakoVeVstupu()
        {
            PredanaData data = PredanaData.PrectiText(Priklad("audio.json"));
            Assert.AreEqual("4F0C1E2A-7B3D-4C55-9E21-0A6B3D9F1C77", data.Pid);
            Assert.AreEqual("5704150000", data.Rc);
            Assert.AreEqual("2026-10-28T09:42:00", data.CasVysetreni);
            CollectionAssert.AreEqual(new[] { "audio" }, data.Casti);
        }

        [TestMethod]
        public void SkriptNedostanePacienta()
        {
            foreach (string priklad in new[] { "audio.json", "tympanometrie.json", "slovni.json" })
            {
                PredanaData data = PredanaData.PrectiText(Priklad(priklad));
                Neobsahuje(data.DataProSkript, "patient");
                Neobsahuje(data.DataProSkript, "5704150000");
                Neobsahuje(data.DataProSkript, "Novák");
                Neobsahuje(data.DataProSkript, "1957-04-15");
                Obsahuje(data.DataProSkript, "\"exam\"");
            }
        }

        [TestMethod]
        public void DataProSkriptJsouBezeZmeny()
        {
            string vstup = "{\"fmt\":\"fowler-exam\",\"v\":1,\"app\":\"Fowler\",\"created\":\"2026-10-29T10:15:31\"," +
                "\"patient\":{\"rc\":\"5704150000\"},\"exam\":{\"time\":\"2026-10-28T09:42:00\"}," +
                "\"tymp\":{\"legacy\":false,\"R\":{\"tg\":[{\"probeHz\":226,\"peakY\":0.650,\"y\":[1.10,1.2]}]}},\"novyKlic\":{\"x\":1.0}}";
            PredanaData data = PredanaData.PrectiText(vstup);
            Assert.AreEqual("{\"fmt\":\"fowler-exam\",\"v\":1,\"app\":\"Fowler\",\"created\":\"2026-10-29T10:15:31\"," +
                "\"exam\":{\"time\":\"2026-10-28T09:42:00\"}," +
                "\"tymp\":{\"legacy\":false,\"R\":{\"tg\":[{\"probeHz\":226,\"peakY\":0.650,\"y\":[1.10,1.2]}]}},\"novyKlic\":{\"x\":1.0}}",
                data.DataProSkript);
        }

        [TestMethod]
        public void KodyChybVstupu()
        {
            string platny = Priklad("audio.json");
            Kod(Beh.KodVstup, "{neplatný");
            Kod(Beh.KodVstup, "[1,2]");
            Kod(Beh.KodFormat, platny.Replace("\"fowler-exam\"", "\"jiny-format\""));
            Kod(Beh.KodFormat, platny.Replace("\"v\": 1", "\"v\": 2"));
            Kod(Beh.KodVstup, platny.Replace("\"v\": 1,", ""));
            Kod(Beh.KodVstup, platny.Replace("\"time\"", "\"cas\""));
            Kod(Beh.KodVstup, platny.Replace("\"audio\"", "\"neznama\""));
        }

        static void Kod(int kod, string vstup)
        {
            try
            {
                PredanaData.PrectiText(vstup);
                Assert.Fail("Vstup nebyl odmítnut:\n" + vstup);
            }
            catch (ChybaVstupuException ex)
            {
                Assert.AreEqual(kod, ex.Kod, ex.Message);
            }
        }

        [TestMethod]
        public void SouborVstupuSBomANeplatneUtf8()
        {
            string slozka = NovaSlozka();
            string sBom = Path.Combine(slozka, "bom.json");
            File.WriteAllText(sBom, Priklad("audio.json"), new UTF8Encoding(true));
            Assert.AreEqual("5704150000", PredanaData.PrectiSoubor(sBom).Rc);

            string cp1250 = Path.Combine(slozka, "cp1250.json");
            File.WriteAllText(cp1250, Priklad("audio.json"), Encoding.GetEncoding(1250));
            try
            {
                PredanaData.PrectiSoubor(cp1250);
                Assert.Fail("soubor v cp1250 nebyl odmítnut");
            }
            catch (ChybaVstupuException ex)
            {
                Assert.AreEqual(Beh.KodVstup, ex.Kod);
                Obsahuje(ex.Message, "UTF-8");
            }
        }
    }
}
