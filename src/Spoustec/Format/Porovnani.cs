using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Spoustec.Json;

namespace Spoustec.Format
{
    /// <summary>
    /// Porovnani vysledku skriptu (po formalni kontrole) s ocekavanym vysledkem testovaciho pripadu. Porovnavaji se
    /// jednotliva pole: cisla podle hodnoty v JSON (25 = 25.0, spoustec nezaokrouhluje, proto bez tolerance), texty
    /// presne. null a chybejici pole znamena "neurceno". Seznamy reflexu a decay se porovnavaji podle podnetu
    /// (poradi polozek nerozhoduje). Poznamka (note) se neporovnava.
    /// </summary>
    public static class Porovnani
    {
        public sealed class Rozdil
        {
            public string Cesta { get; set; }
            public string Ocekavano { get; set; }
            public string Vysledek { get; set; }
        }

        public const string Neurceno = "neurčeno";

        public static List<Rozdil> Porovnej(JsonHodnota ocekavano, JsonHodnota vysledek)
        {
            List<KeyValuePair<string, JsonHodnota>> o = new List<KeyValuePair<string, JsonHodnota>>();
            List<KeyValuePair<string, JsonHodnota>> v = new List<KeyValuePair<string, JsonHodnota>>();
            foreach (KeyValuePair<string, JsonHodnota> p in ocekavano.Polozky)
            {
                if (p.Key != "note")
                    Rozloz(p.Value, p.Key, o);
            }
            Rozloz(vysledek, "", v);
            Dictionary<string, JsonHodnota> vysledkem = v.ToDictionary(p => p.Key, p => p.Value);
            Dictionary<string, JsonHodnota> ocekavanim = o.ToDictionary(p => p.Key, p => p.Value);
            List<Rozdil> rozdily = new List<Rozdil>();
            foreach (KeyValuePair<string, JsonHodnota> p in o)
            {
                JsonHodnota h;
                if (!vysledkem.TryGetValue(p.Key, out h))
                    rozdily.Add(new Rozdil { Cesta = p.Key, Ocekavano = Popis(p.Value), Vysledek = Neurceno });
                else if (!Shodne(p.Value, h))
                    rozdily.Add(new Rozdil { Cesta = p.Key, Ocekavano = Popis(p.Value), Vysledek = Popis(h) });
            }
            foreach (KeyValuePair<string, JsonHodnota> p in v)
            {
                if (!ocekavanim.ContainsKey(p.Key))
                    rozdily.Add(new Rozdil { Cesta = p.Key, Ocekavano = Neurceno, Vysledek = Popis(p.Value) });
            }
            return rozdily;
        }

        //jednotlive hodnoty s cestou, napr. audio.acAvg.R, tymp.R.ipsi[1000].res; null a prazdne objekty se vynechaji
        static void Rozloz(JsonHodnota h, string cesta, List<KeyValuePair<string, JsonHodnota>> hodnoty)
        {
            switch (h.Typ)
            {
                case TypJson.Null:
                    return;
                case TypJson.Objekt:
                    foreach (KeyValuePair<string, JsonHodnota> p in h.Polozky)
                        Rozloz(p.Value, (cesta.Length == 0) ? p.Key : cesta + "." + p.Key, hodnoty);
                    return;
                case TypJson.Pole:
                    bool podlePodnetu = h.Prvky.All(p => p.Typ == TypJson.Objekt && p.Polozka("stim") != null &&
                        (p.Polozka("stim").Typ == TypJson.Cislo || p.Polozka("stim").Typ == TypJson.Text));
                    HashSet<string> pouzite = new HashSet<string>();
                    for (int i = 0; i < h.Prvky.Count; i++)
                    {
                        string klic = i.ToString(CultureInfo.InvariantCulture);
                        if (podlePodnetu)
                        {
                            JsonHodnota stim = h.Prvky[i].Polozka("stim");
                            string podnet = (stim.Typ == TypJson.Text) ? stim.Text : Normalizuj(stim.Cislo);
                            if (pouzite.Add(podnet))
                                klic = podnet;
                        }
                        JsonHodnota prvek = h.Prvky[i];
                        if (podlePodnetu && klic != i.ToString(CultureInfo.InvariantCulture))
                        {
                            //stim je soucasti klice, porovnaji se ostatni polozky
                            JsonHodnota bezPodnetu = JsonHodnota.NovyObjekt();
                            foreach (KeyValuePair<string, JsonHodnota> p in prvek.Polozky.Where(p => p.Key != "stim"))
                                bezPodnetu.Nastav(p.Key, p.Value);
                            prvek = bezPodnetu;
                        }
                        Rozloz(prvek, $"{cesta}[{klic}]", hodnoty);
                    }
                    return;
                default:
                    if (!hodnoty.Any(p => p.Key == cesta))
                        hodnoty.Add(new KeyValuePair<string, JsonHodnota>(cesta, h));
                    return;
            }
        }

        static bool Shodne(JsonHodnota a, JsonHodnota b)
        {
            if (a.Typ != b.Typ)
                return false;
            switch (a.Typ)
            {
                case TypJson.Cislo:
                    return Normalizuj(a.Cislo) == Normalizuj(b.Cislo);
                case TypJson.Text:
                    return a.Text == b.Text;
                case TypJson.Logicka:
                    return a.Logicka == b.Logicka;
                default:
                    return true;
            }
        }

        /// <summary>cislo JSON na kanonicky text podle hodnoty ("25.0" = "25", "1e1" = "10"); kdyz se neda prevest, text beze zmeny</summary>
        static string Normalizuj(string cislo)
        {
            decimal d;
            if (!decimal.TryParse(cislo, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return cislo;
            d = d / 1.000000000000000000000000000000000m;   //bez nul na konci
            return d.ToString(CultureInfo.InvariantCulture);
        }

        static string Popis(JsonHodnota h)
        {
            switch (h.Typ)
            {
                case TypJson.Cislo:
                    return h.Cislo;
                case TypJson.Text:
                    StringBuilder sb = new StringBuilder();
                    JsonZapis.Text(sb, h.Text);
                    return sb.ToString();
                case TypJson.Logicka:
                    return h.Logicka ? "true" : "false";
                case TypJson.Objekt:
                    return "{}";
                case TypJson.Pole:
                    return "[]";
                default:
                    return Neurceno;
            }
        }
    }
}
