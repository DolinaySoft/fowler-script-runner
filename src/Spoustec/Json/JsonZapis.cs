using System.Globalization;
using System.Linq;
using System.Text;

namespace Spoustec.Json
{
    /// <summary>zapis JsonHodnota do textu JSON; cisla se zapisou presne tak, jak jsou ulozena</summary>
    public static class JsonZapis
    {
        /// <summary>odsadit = 2 mezery na uroven, seznamy jednoduchych hodnot na jednom radku</summary>
        public static string Zapis(JsonHodnota hodnota, bool odsadit)
        {
            StringBuilder sb = new StringBuilder();
            Zapis(sb, hodnota, odsadit, 0);
            return sb.ToString();
        }

        static void Zapis(StringBuilder sb, JsonHodnota h, bool odsadit, int uroven)
        {
            switch (h.Typ)
            {
                case TypJson.Null:
                    sb.Append("null");
                    break;
                case TypJson.Logicka:
                    sb.Append(h.Logicka ? "true" : "false");
                    break;
                case TypJson.Cislo:
                    sb.Append(h.Cislo);
                    break;
                case TypJson.Text:
                    Text(sb, h.Text);
                    break;
                case TypJson.Pole:
                    bool jednoduche = h.Prvky.All(p => p.Typ != TypJson.Objekt && p.Typ != TypJson.Pole);
                    sb.Append('[');
                    for (int i = 0; i < h.Prvky.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(odsadit && jednoduche ? ", " : ",");
                        if (odsadit && !jednoduche)
                            NovyRadek(sb, uroven + 1);
                        Zapis(sb, h.Prvky[i], odsadit, uroven + 1);
                    }
                    if (odsadit && !jednoduche && h.Prvky.Count > 0)
                        NovyRadek(sb, uroven);
                    sb.Append(']');
                    break;
                case TypJson.Objekt:
                    sb.Append('{');
                    for (int i = 0; i < h.Polozky.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(',');
                        if (odsadit)
                            NovyRadek(sb, uroven + 1);
                        Text(sb, h.Polozky[i].Key);
                        sb.Append(odsadit ? ": " : ":");
                        Zapis(sb, h.Polozky[i].Value, odsadit, uroven + 1);
                    }
                    if (odsadit && h.Polozky.Count > 0)
                        NovyRadek(sb, uroven);
                    sb.Append('}');
                    break;
            }
        }

        static void NovyRadek(StringBuilder sb, int uroven)
        {
            sb.Append('\n').Append(' ', 2 * uroven);
        }

        /// <summary>text v uvozovkach; ridici znaky a osamocene casti dvojic UTF-16 jako \uXXXX</summary>
        public static void Text(StringBuilder sb, string text)
        {
            sb.Append('"');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        bool osamocena = (char.IsHighSurrogate(c) && (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))) ||
                            (char.IsLowSurrogate(c) && (i == 0 || !char.IsHighSurrogate(text[i - 1])));
                        if (c < 0x20 || osamocena)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
