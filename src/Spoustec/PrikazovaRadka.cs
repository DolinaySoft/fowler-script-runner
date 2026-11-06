using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Spoustec
{
    /// <summary>
    /// Oprava parametru od Fowleru. Fowler cte parametry z fowler.ini funkci GetPrivateProfileString a ta z hodnoty,
    /// ktera zacina i konci uvozovkou, uvozovky na krajich odebere: z "%VSTUP%" "%VYSTUP%" "C:\Fowler\skripty" zbude
    /// %VSTUP%" "%VYSTUP%" "C:\Fowler\skripty a Windows pak parametry rozdeli spatne (jako jeden, nebo podle mezer
    /// v cestach). Takova prikazova radka za nazvem programu nezacina ani nekonci uvozovkou a uvozovky v ni jsou jen
    /// v dvojicich " " mezi parametry; spoustec ji pozna, doplni vnejsi uvozovky a parametry rozdeli znovu (stejne jako
    /// Windows). Radky zadane rucne (napr. a "b c" d) tomuto tvaru neodpovidaji a zustanou, jak jsou.
    /// </summary>
    static class PrikazovaRadka
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CommandLineToArgvW(string radka, out int pocet);

        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr pamet);

        /// <summary>parametry s doplnenymi vnejsimi uvozovkami, null = prikazova radka takovou opravu nepotrebuje</summary>
        public static string[] OpravOdebraneUvozovky(string prikazovaRadka)
        {
            string zbytek = ZaProgramem(prikazovaRadka ?? "");
            if (zbytek.Length == 0 || zbytek[0] == '"' || zbytek[zbytek.Length - 1] == '"' || zbytek.IndexOf('"') < 0)
                return null;
            //uvozovky jen mezi parametry (konec jednoho, mezera, zacatek dalsiho)
            if (Regex.Replace(zbytek, "\"[ \t]+\"", "").IndexOf('"') >= 0)
                return null;
            string[] parametry = Rozdel("spoustec.exe \"" + zbytek + "\"");
            return (parametry != null && parametry.Length >= 3) ? parametry.Skip(1).ToArray() : null;
        }

        //zbytek prikazove radky za nazvem programu (nazev v uvozovkach do dalsi uvozovky, jinak do mezery)
        static string ZaProgramem(string radka)
        {
            int i;
            if (radka.StartsWith("\""))
            {
                int konec = radka.IndexOf('"', 1);
                i = (konec < 0) ? radka.Length : konec + 1;
            }
            else
            {
                i = 0;
                while (i < radka.Length && radka[i] != ' ' && radka[i] != '\t')
                    i++;
            }
            return radka.Substring(i).Trim(' ', '\t');
        }

        //rozdeleni podle pravidel Windows (CommandLineToArgvW), prvni prvek je program
        static string[] Rozdel(string radka)
        {
            int pocet;
            IntPtr pole = CommandLineToArgvW(radka, out pocet);
            if (pole == IntPtr.Zero)
                return null;
            try
            {
                string[] parametry = new string[pocet];
                for (int i = 0; i < pocet; i++)
                    parametry[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(pole, i * IntPtr.Size));
                return parametry;
            }
            finally
            {
                LocalFree(pole);
            }
        }
    }
}
