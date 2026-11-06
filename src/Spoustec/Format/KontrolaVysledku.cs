using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Spoustec.Json;
using Spoustec.Skript;

namespace Spoustec.Format
{
    /// <summary>vysledek skriptu po formalni kontrole: jen platna pole a poznamky</summary>
    public sealed class KontrolovanyVysledek
    {
        public JsonHodnota Casti { get; private set; }                  //objekt s platnymi castmi (audio, tymp, speech)
        public List<string> PoznamkySkriptu { get; private set; }       //note ze skriptu
        public List<string> PoznamkySpoustece { get; private set; }     //co kontrola vynechala a proc

        public KontrolovanyVysledek()
        {
            Casti = JsonHodnota.NovyObjekt();
            PoznamkySkriptu = new List<string>();
            PoznamkySpoustece = new List<string>();
        }
    }

    /// <summary>
    /// Formalni kontrola vysledku skriptu podle casti "Odpoved programu" v docs/format.md: zname klice, typy, rozsahy,
    /// pocet desetinnych mist a znaku cisel, delky textu, seznamy reflexu a decay. Hodnoty se NEMENI ani nezaokrouhluji:
    /// pole, ktere neodpovida, se vynecha a do poznamky se napise proc (Fowler by jinak odmitl celou odpoved).
    /// Kontroluje se jen tvar, ne vecny obsah - ten je vec skriptu a uzivatele, ktery hodnoceni potvrzuje.
    /// </summary>
    public sealed class KontrolaVysledku
    {
        //pole oken Fowleru (docs/format.md, Odpoved programu)
        const int MaxZnakuTextu = 100;          //who, group, sisi
        const int MaxZnakuTypu = 10;            //typ tympanogramu
        const int MaxZnakuCislaAudia = 5;       //ciselna pole okna Hodnotit audiogramu, napr. 12.25, -10.5, 100
        //technicke meze cteni odpovedi ve Fowleru (cele cislo jako frekvence a procenta)
        const int MaxPodnetHz = 99999;
        const int MaxCeleCislo = 100000;
        const int MaxPoznamek = 50;

        static readonly string[] KlicePolozkyReflexu = { "stim", "res" };
        static readonly string[] KlicePolozkyDecay = { "stim", "pct" };
        static readonly string[] Sumy = { "LP", "HP", "WB" };

        readonly KontrolovanyVysledek vysledek = new KontrolovanyVysledek();
        int vynechanoPoznamek;

        public static KontrolovanyVysledek Zkontroluj(Hodnota hodnota, IList<string> predaneCasti)
        {
            KontrolaVysledku kontrola = new KontrolaVysledku();
            kontrola.Koren(hodnota, predaneCasti);
            if (kontrola.vynechanoPoznamek > 0)
                kontrola.vysledek.PoznamkySpoustece.Add($"… a další upozornění ({kontrola.vynechanoPoznamek}).");
            return kontrola.vysledek;
        }

        void Poznamka(string text)
        {
            if (vysledek.PoznamkySpoustece.Count < MaxPoznamek)
                vysledek.PoznamkySpoustece.Add(text);
            else
                vynechanoPoznamek++;
        }

        void Vynechano(string cesta, string duvod)
        {
            Poznamka($"{cesta} vynecháno: {duvod}.");
        }

        void Koren(Hodnota h, IList<string> predaneCasti)
        {
            if (h.Typ == TypHodnoty.Undefined || h.Typ == TypHodnoty.Null)
                return; //skript nic neurcil
            if (h.Typ != TypHodnoty.Objekt)
            {
                Poznamka($"Výsledek funkce hodnot vynechán: očekáván objekt {{ audio: {{...}}, note: \"...\" }}, skript vrátil {h.PopisDoPoznamky()}.");
                return;
            }
            Dictionary<string, Hodnota> casti = new Dictionary<string, Hodnota>();
            foreach (KeyValuePair<string, Hodnota> p in h.Polozky)
            {
                if (p.Key == "note")
                    Poznamky(p.Value);
                else if (PredanaData.ZnameCasti.Contains(p.Key))
                {
                    if (p.Value.Typ == TypHodnoty.Null)
                        continue;
                    if (!predaneCasti.Contains(p.Key))
                        Vynechano(p.Key, $"Fowler předal část {string.Join(", ", predaneCasti)}, jiné části se do okna nevkládají");
                    else
                        casti[p.Key] = p.Value;
                }
                else
                    NeznamyKlic(p.Key, p.Key, PredanaData.ZnameCasti.Concat(new[] { "note" }));
            }
            foreach (string nazev in PredanaData.ZnameCasti)
            {
                Hodnota cast;
                if (!casti.TryGetValue(nazev, out cast))
                    continue;
                JsonHodnota zapis = null;
                if (nazev == "audio")
                    zapis = Audio(cast);
                else if (nazev == "tymp")
                    zapis = Tympanometrie(cast);
                else if (nazev == "speech")
                    zapis = Slovni(cast);
                PridejNeprazdne(vysledek.Casti, nazev, zapis);
            }
        }

        //note: text nebo seznam textu
        void Poznamky(Hodnota h)
        {
            if (h.Typ == TypHodnoty.Undefined || h.Typ == TypHodnoty.Null)
                return;
            if (h.Typ == TypHodnoty.Text)
            {
                if (h.Text.Trim().Length > 0)
                    vysledek.PoznamkySkriptu.Add(h.Text);
                return;
            }
            if (h.Typ != TypHodnoty.Pole)
            {
                Vynechano("note", "očekáván text nebo seznam textů, skript vrátil " + h.PopisDoPoznamky());
                return;
            }
            for (int i = 0; i < h.Prvky.Count; i++)
            {
                Hodnota p = h.Prvky[i];
                if (p.Typ == TypHodnoty.Text)
                {
                    if (p.Text.Trim().Length > 0)
                        vysledek.PoznamkySkriptu.Add(p.Text);
                }
                else if (p.Typ != TypHodnoty.Null)
                    Vynechano($"note[{i}]", "očekáván text, skript vrátil " + p.PopisDoPoznamky());
            }
        }

        #region Spolecne

        void NeznamyKlic(string cesta, string klic, IEnumerable<string> zname)
        {
            string podobny = zname.FirstOrDefault(k => string.Equals(k, klic, StringComparison.OrdinalIgnoreCase));
            Vynechano(cesta, "neznámý klíč " + ((podobny != null) ? $"(myslíte {podobny}?)" : $"(povolené: {string.Join(", ", zname)})"));
        }

        /// <summary>
        /// polozky objektu na ceste, null = neni co kontrolovat (null tise, undefined a jiny typ s poznamkou);
        /// nezname klice se ohlasi a vynechaji
        /// </summary>
        Dictionary<string, Hodnota> Objekt(Hodnota h, string cesta, string[] zname)
        {
            if (h.Typ == TypHodnoty.Null)
                return null;
            if (h.Typ == TypHodnoty.Undefined)
            {
                Vynechano(cesta, "hodnota je undefined – údaj, který skript neurčuje, vynechte nebo vraťte null");
                return null;
            }
            if (h.Typ != TypHodnoty.Objekt)
            {
                Vynechano(cesta, "očekáván objekt, skript vrátil " + h.PopisDoPoznamky());
                return null;
            }
            Dictionary<string, Hodnota> polozky = new Dictionary<string, Hodnota>();
            foreach (KeyValuePair<string, Hodnota> p in h.Polozky)
            {
                if (zname.Contains(p.Key))
                    polozky[p.Key] = p.Value;
                else
                    NeznamyKlic(cesta + "." + p.Key, p.Key, zname);
            }
            return polozky;
        }

        static void PridejNeprazdne(JsonHodnota objekt, string klic, JsonHodnota hodnota)
        {
            if (hodnota != null && !hodnota.JePrazdny)
                objekt.Nastav(klic, hodnota);
        }

        /// <summary>
        /// cislo do pole: typ, rozsah, nejvyse maxDesetin desetinnych mist a maxZnaku znaku zapisu (jak ho zapise
        /// skript, napr. 0.30000000000000004); null = vynechano (null ve vysledku tise, jinak s poznamkou)
        /// </summary>
        JsonHodnota Cislo(Hodnota h, string cesta, double min, double max, int maxDesetin, int maxZnaku)
        {
            if (!JeCislo(h, cesta))
                return null;
            string text = h.CisloText;
            if (h.Cislo < min || h.Cislo > max)
            {
                Vynechano(cesta, $"{text} je mimo rozsah pole {Text(min)} až {Text(max)}");
                return null;
            }
            int desetin = DesetinnychMist(text);
            if (desetin > maxDesetin)
            {
                Vynechano(cesta, $"{text} má víc desetinných míst, než pole dovoluje (nejvýše {maxDesetin}) – zaokrouhlete ve skriptu");
                return null;
            }
            if (text.Length > maxZnaku)
            {
                Vynechano(cesta, $"{text} má {text.Length} znaků, pole pojme nejvýše {maxZnaku} – zaokrouhlete ve skriptu na méně desetinných míst");
                return null;
            }
            return JsonHodnota.CisloZTextu(text);
        }

        bool JeCislo(Hodnota h, string cesta)
        {
            switch (h.Typ)
            {
                case TypHodnoty.Null:
                    return false;
                case TypHodnoty.Undefined:
                    Vynechano(cesta, "hodnota je undefined – údaj, který skript neurčuje, vynechte nebo vraťte null");
                    return false;
                case TypHodnoty.Cislo:
                    if (double.IsNaN(h.Cislo))
                    {
                        Vynechano(cesta, "hodnota je NaN (neplatný výpočet, např. s \"NR\", undefined nebo textem)");
                        return false;
                    }
                    if (double.IsInfinity(h.Cislo))
                    {
                        Vynechano(cesta, $"hodnota je {h.CisloText} (např. dělení nulou)");
                        return false;
                    }
                    return true;
                default:
                    Vynechano(cesta, "očekáváno číslo, skript vrátil " + h.PopisDoPoznamky());
                    return false;
            }
        }

        /// <summary>cele cislo |x| &lt;= max</summary>
        bool JeCeleCislo(Hodnota h, int max)
        {
            return h.Typ == TypHodnoty.Cislo && !double.IsNaN(h.Cislo) && !double.IsInfinity(h.Cislo) &&
                h.Cislo == Math.Floor(h.Cislo) && Math.Abs(h.Cislo) <= max;
        }

        /// <summary>pocet desetinnych mist zapisu cisla, i s exponentem ("1e-7" = 7, "1.5e+2" = 0)</summary>
        static int DesetinnychMist(string text)
        {
            int e = text.IndexOfAny(new[] { 'e', 'E' });
            string mantisa = (e < 0) ? text : text.Substring(0, e);
            int exponent = 0;
            if (e >= 0 && !int.TryParse(text.Substring(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
                return int.MaxValue;
            int tecka = mantisa.IndexOf('.');
            int desetin = (tecka < 0) ? 0 : mantisa.Length - tecka - 1;
            return Math.Max(0, desetin - exponent);
        }

        static string Text(double cislo)
        {
            return cislo.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>text do pole: bez ridicich znaku, po oriznuti mezer neprazdny a nejvyse maxZnaku; zapise se beze zmeny</summary>
        JsonHodnota TextPole(Hodnota h, string cesta, int maxZnaku)
        {
            switch (h.Typ)
            {
                case TypHodnoty.Null:
                    return null;
                case TypHodnoty.Undefined:
                    Vynechano(cesta, "hodnota je undefined – údaj, který skript neurčuje, vynechte nebo vraťte null");
                    return null;
                case TypHodnoty.Text:
                    break;
                default:
                    Vynechano(cesta, "očekáván text, skript vrátil " + h.PopisDoPoznamky());
                    return null;
            }
            if (h.Text.Any(c => c < 0x20))
            {
                Vynechano(cesta, "text obsahuje řídicí znak (např. konec řádku)");
                return null;
            }
            string orezany = h.Text.Trim(' ');
            if (orezany.Length == 0)
            {
                Vynechano(cesta, "prázdný text – údaj, který skript neurčuje, vynechte nebo vraťte null");
                return null;
            }
            if (orezany.Length > maxZnaku)
            {
                Vynechano(cesta, $"text má {orezany.Length} znaků, pole pojme nejvýše {maxZnaku}");
                return null;
            }
            return JsonHodnota.Retezec(h.Text);
        }

        #endregion

        #region Tonova audiometrie

        static readonly string[] KliceAudia = { "fowler", "acAvg", "bcAvg", "who", "group", "sisi" };
        static readonly string[] KliceTrojice = { "R", "L", "total" };
        static readonly string[] KliceSisi = { "R", "L" };

        JsonHodnota Audio(Hodnota h)
        {
            Dictionary<string, Hodnota> polozky = Objekt(h, "audio", KliceAudia);
            if (polozky == null)
                return null;
            JsonHodnota o = JsonHodnota.NovyObjekt();
            foreach (string klic in KliceAudia)
            {
                Hodnota p;
                if (!polozky.TryGetValue(klic, out p))
                    continue;
                string cesta = "audio." + klic;
                if (klic == "fowler")
                    PridejNeprazdne(o, klic, Trojice(p, cesta, 0, 100));
                else if (klic == "acAvg" || klic == "bcAvg")
                    PridejNeprazdne(o, klic, Trojice(p, cesta, -10, 130));
                else if (klic == "who" || klic == "group")
                    PridejNeprazdne(o, klic, TextPole(p, cesta, MaxZnakuTextu));
                else if (klic == "sisi")
                {
                    Dictionary<string, Hodnota> strany = Objekt(p, cesta, KliceSisi);
                    if (strany == null)
                        continue;
                    JsonHodnota sisi = JsonHodnota.NovyObjekt();
                    foreach (string strana in KliceSisi)
                    {
                        if (strany.ContainsKey(strana))
                            PridejNeprazdne(sisi, strana, TextPole(strany[strana], cesta + "." + strana, MaxZnakuTextu));
                    }
                    PridejNeprazdne(o, klic, sisi);
                }
            }
            return o;
        }

        //R, L, total: cislo s nejvyse 2 desetinnymi misty a 5 znaky
        JsonHodnota Trojice(Hodnota h, string cesta, double min, double max)
        {
            Dictionary<string, Hodnota> polozky = Objekt(h, cesta, KliceTrojice);
            if (polozky == null)
                return null;
            JsonHodnota o = JsonHodnota.NovyObjekt();
            foreach (string klic in KliceTrojice)
            {
                if (polozky.ContainsKey(klic))
                    PridejNeprazdne(o, klic, Cislo(polozky[klic], cesta + "." + klic, min, max, 2, MaxZnakuCislaAudia));
            }
            return o;
        }

        #endregion

        #region Tympanometrie

        static readonly string[] KliceUcha = { "R", "L" };
        static readonly string[] KliceTympUcha = { "type", "ipsi", "contra", "decayIpsi", "decayContra" };

        JsonHodnota Tympanometrie(Hodnota h)
        {
            Dictionary<string, Hodnota> usi = Objekt(h, "tymp", KliceUcha);
            if (usi == null)
                return null;
            JsonHodnota o = JsonHodnota.NovyObjekt();
            foreach (string ucho in KliceUcha)
            {
                if (!usi.ContainsKey(ucho))
                    continue;
                string cesta = "tymp." + ucho;
                Dictionary<string, Hodnota> polozky = Objekt(usi[ucho], cesta, KliceTympUcha);
                if (polozky == null)
                    continue;
                JsonHodnota u = JsonHodnota.NovyObjekt();
                foreach (string klic in KliceTympUcha)
                {
                    Hodnota p;
                    if (!polozky.TryGetValue(klic, out p))
                        continue;
                    if (klic == "type")
                        PridejNeprazdne(u, klic, TextPole(p, cesta + "." + klic, MaxZnakuTypu));
                    else
                        PridejNeprazdne(u, klic, SeznamPodnetu(p, cesta + "." + klic, klic.StartsWith("decay")));
                }
                PridejNeprazdne(o, ucho, u);
            }
            return o;
        }

        /// <summary>
        /// seznam reflexu { stim, res } nebo decay { stim, pct }; polozka bez vysledku (null) se vynecha tise,
        /// stejny podnet vicekrat = vynechaji se vsechny jeho polozky (Fowler by odmitl celou odpoved)
        /// </summary>
        JsonHodnota SeznamPodnetu(Hodnota h, string cesta, bool decay)
        {
            if (h.Typ == TypHodnoty.Null)
                return null;
            if (h.Typ == TypHodnoty.Undefined)
            {
                Vynechano(cesta, "hodnota je undefined – údaj, který skript neurčuje, vynechte nebo vraťte null");
                return null;
            }
            if (h.Typ != TypHodnoty.Pole)
            {
                Vynechano(cesta, "očekáván seznam [{ stim: ..., " + (decay ? "pct" : "res") + ": ... }], skript vrátil " + h.PopisDoPoznamky());
                return null;
            }
            string klicVysledku = decay ? "pct" : "res";
            List<KeyValuePair<string, JsonHodnota>> platne = new List<KeyValuePair<string, JsonHodnota>>();  //podnet a polozka
            for (int i = 0; i < h.Prvky.Count; i++)
            {
                string cestaPolozky = $"{cesta}[{i}]";
                Dictionary<string, Hodnota> polozka = Objekt(h.Prvky[i], cestaPolozky, decay ? KlicePolozkyDecay : KlicePolozkyReflexu);
                if (polozka == null)
                    continue;
                Hodnota stim, vysledekPodnetu;
                polozka.TryGetValue("stim", out stim);
                polozka.TryGetValue(klicVysledku, out vysledekPodnetu);
                JsonHodnota podnet = Podnet(stim);
                if (podnet == null)
                {
                    Vynechano(cestaPolozky, "stim musí být frekvence v Hz (celé číslo 1 až 99999) nebo \"LP\", \"HP\", \"WB\", skript vrátil " +
                        ((stim == null) ? "položku bez stim" : stim.PopisDoPoznamky()));
                    continue;
                }
                if (vysledekPodnetu == null || vysledekPodnetu.Typ == TypHodnoty.Null)
                    continue; //podnet bez vysledku
                JsonHodnota hodnota = decay ? Decay(vysledekPodnetu) : Reflex(vysledekPodnetu);
                if (hodnota == null)
                {
                    Vynechano(cestaPolozky, decay ?
                        $"pct musí být celé číslo (nejvýše ±{MaxCeleCislo}) nebo \"-\" (neměřitelné), skript vrátil {vysledekPodnetu.PopisDoPoznamky()}" :
                        $"res musí být \"+\" nebo \"-\", skript vrátil {vysledekPodnetu.PopisDoPoznamky()}");
                    continue;
                }
                JsonHodnota zapis = JsonHodnota.NovyObjekt();
                zapis.Nastav("stim", podnet);
                zapis.Nastav(klicVysledku, hodnota);
                platne.Add(new KeyValuePair<string, JsonHodnota>(TextPodnetu(podnet), zapis));
            }
            JsonHodnota seznam = JsonHodnota.NovePole();
            foreach (IGrouping<string, KeyValuePair<string, JsonHodnota>> skupina in platne.GroupBy(p => p.Key))
            {
                if (skupina.Count() > 1)
                    Vynechano(cesta, $"podnět {skupina.Key} je v seznamu {skupina.Count()}×, vynechány všechny jeho položky");
                else
                    seznam.Prvky.Add(skupina.First().Value);
            }
            return seznam;
        }

        JsonHodnota Podnet(Hodnota stim)
        {
            if (stim == null)
                return null;
            if (stim.Typ == TypHodnoty.Text && Sumy.Contains(stim.Text))
                return JsonHodnota.Retezec(stim.Text);
            if (JeCeleCislo(stim, MaxPodnetHz) && stim.Cislo >= 1)
                return JsonHodnota.CisloZTextu(stim.CisloText);
            return null;
        }

        static string TextPodnetu(JsonHodnota podnet)
        {
            return (podnet.Typ == TypJson.Text) ? podnet.Text : podnet.Cislo + " Hz";
        }

        static JsonHodnota Reflex(Hodnota res)
        {
            return (res.Typ == TypHodnoty.Text && (res.Text == "+" || res.Text == "-")) ? JsonHodnota.Retezec(res.Text) : null;
        }

        JsonHodnota Decay(Hodnota pct)
        {
            if (pct.Typ == TypHodnoty.Text && pct.Text == "-")
                return JsonHodnota.Retezec("-");
            return JeCeleCislo(pct, MaxCeleCislo) ? JsonHodnota.CisloZTextu(pct.CisloText) : null;
        }

        #endregion

        #region Slovni audiometrie

        static readonly string[] KliceSlovni = { "srt", "aided" };
        static readonly string[] KliceAided = { "srt" };
        static readonly string[] KliceSrt = { "R", "L", "bin" };

        JsonHodnota Slovni(Hodnota h)
        {
            Dictionary<string, Hodnota> polozky = Objekt(h, "speech", KliceSlovni);
            if (polozky == null)
                return null;
            JsonHodnota o = JsonHodnota.NovyObjekt();
            if (polozky.ContainsKey("srt"))
                PridejNeprazdne(o, "srt", Srt(polozky["srt"], "speech.srt"));
            if (polozky.ContainsKey("aided"))
            {
                Dictionary<string, Hodnota> aided = Objekt(polozky["aided"], "speech.aided", KliceAided);
                if (aided != null && aided.ContainsKey("srt"))
                {
                    JsonHodnota a = JsonHodnota.NovyObjekt();
                    PridejNeprazdne(a, "srt", Srt(aided["srt"], "speech.aided.srt"));
                    PridejNeprazdne(o, "aided", a);
                }
            }
            return o;
        }

        //SRT: cislo -10 az 120 s nejvyse 1 desetinnym mistem
        JsonHodnota Srt(Hodnota h, string cesta)
        {
            Dictionary<string, Hodnota> polozky = Objekt(h, cesta, KliceSrt);
            if (polozky == null)
                return null;
            JsonHodnota o = JsonHodnota.NovyObjekt();
            foreach (string klic in KliceSrt)
            {
                if (polozky.ContainsKey(klic))
                    PridejNeprazdne(o, klic, Cislo(polozky[klic], cesta + "." + klic, -10, 120, 1, int.MaxValue));
            }
            return o;
        }

        #endregion
    }
}
