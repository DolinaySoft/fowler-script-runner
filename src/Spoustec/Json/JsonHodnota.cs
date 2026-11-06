using System;
using System.Collections.Generic;
using System.Globalization;

namespace Spoustec.Json
{
    public enum TypJson { Null, Logicka, Cislo, Text, Pole, Objekt }

    /// <summary>
    /// hodnota JSON: cislo si pamatuje text tak, jak byl zapsan (zapise se zpet beze zmeny), objekt poradi klicu
    /// </summary>
    public sealed class JsonHodnota
    {
        public TypJson Typ { get; private set; }
        public bool Logicka { get; private set; }
        public string Cislo { get; private set; }      //text cisla podle gramatiky JSON, napr. "12.5", "-10", "1e-7"
        public string Text { get; private set; }
        public List<JsonHodnota> Prvky { get; private set; }
        public List<KeyValuePair<string, JsonHodnota>> Polozky { get; private set; }

        JsonHodnota(TypJson typ)
        {
            Typ = typ;
        }

        public static JsonHodnota Null() { return new JsonHodnota(TypJson.Null); }
        public static JsonHodnota Bool(bool hodnota) { return new JsonHodnota(TypJson.Logicka) { Logicka = hodnota }; }
        public static JsonHodnota CisloZTextu(string cislo) { return new JsonHodnota(TypJson.Cislo) { Cislo = cislo }; }
        public static JsonHodnota CisloInt(int cislo) { return CisloZTextu(cislo.ToString(CultureInfo.InvariantCulture)); }
        public static JsonHodnota Retezec(string text) { return new JsonHodnota(TypJson.Text) { Text = text }; }
        public static JsonHodnota NovePole() { return new JsonHodnota(TypJson.Pole) { Prvky = new List<JsonHodnota>() }; }
        public static JsonHodnota NovyObjekt() { return new JsonHodnota(TypJson.Objekt) { Polozky = new List<KeyValuePair<string, JsonHodnota>>() }; }

        /// <summary>polozka objektu, null kdyz neni (JSON null se vrati jako hodnota typu Null)</summary>
        public JsonHodnota Polozka(string klic)
        {
            if (Typ != TypJson.Objekt)
                return null;
            foreach (KeyValuePair<string, JsonHodnota> p in Polozky)
            {
                if (p.Key == klic)
                    return p.Value;
            }
            return null;
        }

        public bool Obsahuje(string klic)
        {
            return Polozka(klic) != null;
        }

        /// <summary>prida nebo nahradi polozku objektu (poradi prvniho vlozeni zustane)</summary>
        public void Nastav(string klic, JsonHodnota hodnota)
        {
            if (Typ != TypJson.Objekt)
                throw new InvalidOperationException("hodnota není objekt");
            for (int i = 0; i < Polozky.Count; i++)
            {
                if (Polozky[i].Key == klic)
                {
                    Polozky[i] = new KeyValuePair<string, JsonHodnota>(klic, hodnota);
                    return;
                }
            }
            Polozky.Add(new KeyValuePair<string, JsonHodnota>(klic, hodnota));
        }

        public void Odeber(string klic)
        {
            if (Typ == TypJson.Objekt)
                Polozky.RemoveAll(p => p.Key == klic);
        }

        /// <summary>objekt nebo pole bez polozek</summary>
        public bool JePrazdny
        {
            get
            {
                return (Typ == TypJson.Objekt && Polozky.Count == 0) || (Typ == TypJson.Pole && Prvky.Count == 0);
            }
        }
    }
}
