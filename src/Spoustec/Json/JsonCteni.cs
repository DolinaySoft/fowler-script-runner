using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Spoustec.Json
{
    /// <summary>neplatny JSON nebo soubor, ktery neni v UTF-8; zprava je bez obsahu dat (jde i do logu)</summary>
    public class ChybaJsonException : Exception
    {
        public ChybaJsonException(string zprava, Exception vnitrni = null) : base(zprava, vnitrni) { }
    }

    /// <summary>
    /// prisne cteni JSON podle RFC 8259: klic opakovany v jednom objektu je chyba (u predanych dat by nebylo
    /// jasne, kterou hodnotu skript dostane), cisla si pamatuji svuj text
    /// </summary>
    public sealed class JsonCteni
    {
        const int MaxHloubka = 64;

        readonly string text;
        int pozice;
        int hloubka;

        JsonCteni(string text)
        {
            this.text = text;
        }

        /// <summary>text souboru v UTF-8 (BOM se toleruje), neplatne UTF-8 je chyba</summary>
        public static string PrectiUtf8(string soubor)
        {
            byte[] data = File.ReadAllBytes(soubor);
            int od = (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) ? 3 : 0;
            try
            {
                return new UTF8Encoding(false, true).GetString(data, od, data.Length - od);
            }
            catch (DecoderFallbackException ex)
            {
                throw new ChybaJsonException("soubor není v kódování UTF-8", ex);
            }
        }

        public static JsonHodnota PrectiSoubor(string soubor)
        {
            return Precti(PrectiUtf8(soubor));
        }

        public static JsonHodnota Precti(string text)
        {
            JsonCteni cteni = new JsonCteni(text);
            cteni.Mezery();
            JsonHodnota hodnota = cteni.Hodnota();
            cteni.Mezery();
            if (cteni.pozice < text.Length)
                throw cteni.Chyba("za koncem dat následuje další text");
            return hodnota;
        }

        ChybaJsonException Chyba(string zprava)
        {
            int radek = 1, sloupec = 1;
            for (int i = 0; i < pozice && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    radek++;
                    sloupec = 1;
                }
                else
                    sloupec++;
            }
            return new ChybaJsonException($"neplatný JSON: {zprava} (řádek {radek}, sloupec {sloupec})");
        }

        bool Konec { get { return pozice >= text.Length; } }

        void Mezery()
        {
            while (!Konec && (text[pozice] == ' ' || text[pozice] == '\t' || text[pozice] == '\r' || text[pozice] == '\n'))
                pozice++;
        }

        bool Slovo(string slovo)
        {
            if (string.CompareOrdinal(text, pozice, slovo, 0, slovo.Length) != 0)
                return false;
            pozice += slovo.Length;
            return true;
        }

        static bool Cislice(char c)
        {
            return c >= '0' && c <= '9';
        }

        JsonHodnota Hodnota()
        {
            if (Konec)
                throw Chyba("neočekávaný konec dat");
            if (++hloubka > MaxHloubka)
                throw Chyba("příliš hluboké vnoření");
            JsonHodnota hodnota;
            char c = text[pozice];
            if (c == '{')
                hodnota = Objekt();
            else if (c == '[')
                hodnota = Pole();
            else if (c == '"')
                hodnota = JsonHodnota.Retezec(Retezec());
            else if (c == '-' || Cislice(c))
                hodnota = Cislo();
            else if (Slovo("true"))
                hodnota = JsonHodnota.Bool(true);
            else if (Slovo("false"))
                hodnota = JsonHodnota.Bool(false);
            else if (Slovo("null"))
                hodnota = JsonHodnota.Null();
            else
                throw Chyba("neplatná hodnota");
            hloubka--;
            return hodnota;
        }

        JsonHodnota Objekt()
        {
            pozice++; //{
            JsonHodnota objekt = JsonHodnota.NovyObjekt();
            HashSet<string> klice = new HashSet<string>(StringComparer.Ordinal);
            Mezery();
            if (!Konec && text[pozice] == '}')
            {
                pozice++;
                return objekt;
            }
            for (;;)
            {
                Mezery();
                if (Konec || text[pozice] != '"')
                    throw Chyba("očekáván klíč v uvozovkách");
                int zacatekKlice = pozice;
                string klic = Retezec();
                if (!klice.Add(klic))
                {
                    pozice = zacatekKlice;
                    throw Chyba($"klíč \"{klic}\" je v objektu vícekrát");
                }
                Mezery();
                if (Konec || text[pozice] != ':')
                    throw Chyba("za klíčem chybí dvojtečka");
                pozice++;
                Mezery();
                objekt.Polozky.Add(new KeyValuePair<string, JsonHodnota>(klic, Hodnota()));
                Mezery();
                if (!Konec && text[pozice] == ',')
                {
                    pozice++;
                    continue;
                }
                if (!Konec && text[pozice] == '}')
                {
                    pozice++;
                    return objekt;
                }
                throw Chyba("v objektu chybí čárka nebo }");
            }
        }

        JsonHodnota Pole()
        {
            pozice++; //[
            JsonHodnota pole = JsonHodnota.NovePole();
            Mezery();
            if (!Konec && text[pozice] == ']')
            {
                pozice++;
                return pole;
            }
            for (;;)
            {
                Mezery();
                pole.Prvky.Add(Hodnota());
                Mezery();
                if (!Konec && text[pozice] == ',')
                {
                    pozice++;
                    continue;
                }
                if (!Konec && text[pozice] == ']')
                {
                    pozice++;
                    return pole;
                }
                throw Chyba("v poli chybí čárka nebo ]");
            }
        }

        string Retezec()
        {
            pozice++; //"
            StringBuilder sb = new StringBuilder();
            while (!Konec)
            {
                char c = text[pozice++];
                if (c == '"')
                    return sb.ToString();
                if (c < 0x20)
                {
                    pozice--;
                    throw Chyba("v textu je řídicí znak (konec řádku se píše \\n)");
                }
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (Konec)
                    break;
                c = text[pozice++];
                switch (c)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        int kod = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            if (Konec)
                                throw Chyba("neukončený text (chybí uvozovky)");
                            char h = text[pozice++];
                            int cislice = Cislice(h) ? h - '0' : (h >= 'a' && h <= 'f') ? h - 'a' + 10 : (h >= 'A' && h <= 'F') ? h - 'A' + 10 : -1;
                            if (cislice < 0)
                                throw Chyba("neplatná sekvence \\u v textu");
                            kod = kod * 16 + cislice;
                        }
                        sb.Append((char)kod);
                        break;
                    default:
                        pozice--;
                        throw Chyba("neplatná sekvence \\ v textu");
                }
            }
            throw Chyba("neukončený text (chybí uvozovky)");
        }

        //cislo podle gramatiky JSON; hodnota zustane jako text
        JsonHodnota Cislo()
        {
            int zacatek = pozice;
            if (text[pozice] == '-')
                pozice++;
            if (Konec || !Cislice(text[pozice]))
                throw Chyba("neplatné číslo");
            if (text[pozice] == '0')
                pozice++;
            else
            {
                while (!Konec && Cislice(text[pozice]))
                    pozice++;
            }
            if (!Konec && text[pozice] == '.')
            {
                pozice++;
                if (Konec || !Cislice(text[pozice]))
                    throw Chyba("neplatné číslo (za desetinnou tečkou chybí číslice)");
                while (!Konec && Cislice(text[pozice]))
                    pozice++;
            }
            if (!Konec && (text[pozice] == 'e' || text[pozice] == 'E'))
            {
                pozice++;
                if (!Konec && (text[pozice] == '+' || text[pozice] == '-'))
                    pozice++;
                if (Konec || !Cislice(text[pozice]))
                    throw Chyba("neplatné číslo (chybí exponent)");
                while (!Konec && Cislice(text[pozice]))
                    pozice++;
            }
            return JsonHodnota.CisloZTextu(text.Substring(zacatek, pozice - zacatek));
        }
    }
}
