using System.Reflection;

namespace Spoustec
{
    static class Verze
    {
        /// <summary>verze spoustece, napr. "1.0.0" (release ji bere z tagu), mistni preklad "0.0.0-dev"</summary>
        public static readonly string Spoustec = Informacni(typeof(Verze).Assembly);

        /// <summary>nazev a verze do odpovedi (app), logu a protokolu, napr. "spoustec 1.0.0"</summary>
        public static readonly string Nazev = "spoustec " + Spoustec;

        /// <summary>AssemblyInformationalVersion bez pripojeneho commitu ("4.17.0+f0e4cbb..." -> "4.17.0")</summary>
        public static string Informacni(Assembly assembly)
        {
            AssemblyInformationalVersionAttribute a = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string verze = (a != null) ? a.InformationalVersion : assembly.GetName().Version.ToString();
            int plus = verze.IndexOf('+');
            return (plus >= 0) ? verze.Substring(0, plus) : verze;
        }
    }
}
