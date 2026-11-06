using System.Collections.Generic;

namespace Spoustec.Skript
{
    public enum TypHodnoty { Undefined, Null, Logicka, Cislo, Text, Pole, Objekt, Jine }

    /// <summary>
    /// Vysledek skriptu nezavisle na jazyku, ve kterem je napsany. Na rozdil od JSON zachova i hodnoty, ktere
    /// JSON nema (undefined, NaN, Infinity, funkce...), aby je kontrola mohla ohlasit: JSON.stringify by je potichu
    /// zmenil na null nebo vynechal.
    /// </summary>
    public sealed class Hodnota
    {
        public TypHodnoty Typ { get; private set; }
        public bool Logicka { get; private set; }
        public double Cislo { get; private set; }
        public string CisloText { get; private set; }   //cislo tak, jak ho zapise jazyk skriptu (JS: String(x)), napr. "0.30000000000000004", "NaN"
        public string Text { get; private set; }
        public List<Hodnota> Prvky { get; private set; }                            //Pole; null prvek = dira v poli
        public List<KeyValuePair<string, Hodnota>> Polozky { get; private set; }    //Objekt
        public string Popis { get; private set; }       //Jine: co to je, napr. "funkce", "Date"

        Hodnota(TypHodnoty typ)
        {
            Typ = typ;
        }

        public static readonly Hodnota Undefined = new Hodnota(TypHodnoty.Undefined);
        public static readonly Hodnota Null = new Hodnota(TypHodnoty.Null);

        public static Hodnota Bool(bool hodnota) { return new Hodnota(TypHodnoty.Logicka) { Logicka = hodnota }; }
        public static Hodnota NoveCislo(double hodnota, string text) { return new Hodnota(TypHodnoty.Cislo) { Cislo = hodnota, CisloText = text }; }
        public static Hodnota Retezec(string text) { return new Hodnota(TypHodnoty.Text) { Text = text }; }
        public static Hodnota NovePole() { return new Hodnota(TypHodnoty.Pole) { Prvky = new List<Hodnota>() }; }
        public static Hodnota NovyObjekt() { return new Hodnota(TypHodnoty.Objekt) { Polozky = new List<KeyValuePair<string, Hodnota>>() }; }
        public static Hodnota NoveJine(string popis) { return new Hodnota(TypHodnoty.Jine) { Popis = popis }; }

        public bool JeCislo { get { return Typ == TypHodnoty.Cislo; } }

        /// <summary>popis hodnoty do poznamky pro uzivatele, napr. text "25", cislo 12.5, undefined</summary>
        public string PopisDoPoznamky()
        {
            switch (Typ)
            {
                case TypHodnoty.Undefined: return "undefined";
                case TypHodnoty.Null: return "null";
                case TypHodnoty.Logicka: return Logicka ? "true" : "false";
                case TypHodnoty.Cislo: return CisloText;
                case TypHodnoty.Text: return "text \"" + Ukazka(Text, 30) + "\"";
                case TypHodnoty.Pole: return "seznam";
                case TypHodnoty.Objekt: return "objekt";
                default: return Popis;
            }
        }

        /// <summary>zacatek textu do poznamky, ridici znaky jako mezery</summary>
        public static string Ukazka(string text, int delka)
        {
            char[] znaky = ((text.Length > delka) ? text.Substring(0, delka) : text).ToCharArray();
            for (int i = 0; i < znaky.Length; i++)
            {
                if (znaky[i] < 0x20)
                    znaky[i] = ' ';
            }
            return new string(znaky) + ((text.Length > delka) ? "…" : "");
        }
    }
}
