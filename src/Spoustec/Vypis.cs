using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Spoustec
{
    /// <summary>
    /// Vypis rezimu --run a --test: text se pise do nadrazene konzole (program je WinExe bez vlastni konzole,
    /// aby pri spusteni z Fowleru neblikla) a zaroven se sbira pro ulozeni do souboru.
    /// </summary>
    public sealed class Vypis
    {
        readonly StringBuilder text = new StringBuilder();
        readonly Action<string> konzole;

        Vypis(Action<string> konzole)
        {
            this.konzole = konzole;
        }

        /// <summary>jen do textu (testy)</summary>
        public static Vypis BezKonzole()
        {
            return new Vypis(null);
        }

        /// <summary>do presmerovaneho vystupu, jinak do konzole procesu, ktery program spustil (cmd, PowerShell)</summary>
        public static Vypis ProKonzoli()
        {
            return new Vypis(Konzole.Otevri());
        }

        public void Radek(string radek = "")
        {
            radek = radek.Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n";
            text.Append(radek);
            if (konzole != null)
            {
                try
                {
                    konzole(radek);
                }
                catch (Exception)
                {
                    //konzole neni nutna, text se ulozi do souboru
                }
            }
        }

        public string Text { get { return text.ToString(); } }

        /// <summary>ulozi cely vypis v UTF-8 s BOM (Poznamkovy blok ve starsich Windows)</summary>
        public void Uloz(string soubor)
        {
            File.WriteAllText(soubor, text.ToString(), new UTF8Encoding(true));
        }

        static class Konzole
        {
            const int StdOutput = -11;
            const int RodicovskyProces = -1;   //ATTACH_PARENT_PROCESS
            const int TypDisk = 1, TypPipe = 3;
            const uint GenericWrite = 0x40000000, FileShareWrite = 2, OpenExisting = 3;

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool AttachConsole(int idProcesu);
            [DllImport("kernel32.dll")]
            static extern IntPtr GetStdHandle(int typ);
            [DllImport("kernel32.dll")]
            static extern int GetFileType(IntPtr handle);
            [DllImport("kernel32.dll")]
            static extern uint GetConsoleOutputCP();
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            static extern IntPtr CreateFileW(string nazev, uint pristup, uint sdileni, IntPtr zabezpeceni, uint vytvoreni, uint atributy, IntPtr sablona);
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            static extern bool WriteConsoleW(IntPtr handle, string text, int znaku, out int zapsano, IntPtr rezerva);

            public static Action<string> Otevri()
            {
                IntPtr std = GetStdHandle(StdOutput);
                int typ = (std != IntPtr.Zero && std != new IntPtr(-1)) ? GetFileType(std) : 0;
                bool konzoleRodice = AttachConsole(RodicovskyProces);
                if (typ == TypDisk || typ == TypPipe)
                {
                    //presmerovany vystup (> soubor, | Out-Host): v kodove strance konzole, bez ni v UTF-8
                    uint kodovaStranka = konzoleRodice ? GetConsoleOutputCP() : 0;
                    Encoding kodovani;
                    try
                    {
                        kodovani = (kodovaStranka != 0) ? Encoding.GetEncoding((int)kodovaStranka) : new UTF8Encoding(false);
                    }
                    catch (Exception)
                    {
                        kodovani = new UTF8Encoding(false);
                    }
                    FileStream proud = new FileStream(new SafeFileHandle(std, false), FileAccess.Write);
                    StreamWriter zapis = new StreamWriter(proud, kodovani) { AutoFlush = true };
                    return zapis.Write;
                }
                if (!konzoleRodice)
                    return null;
                IntPtr vystup = CreateFileW("CONOUT$", GenericWrite, FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                if (vystup == new IntPtr(-1))
                    return null;
                //prompt uz je vypsany (cmd na program bez konzole neceka), vystup zacne na novem radku
                WriteConsoleW(vystup, "\r\n", 2, out _, IntPtr.Zero);
                return radek => WriteConsoleW(vystup, radek, radek.Length, out _, IntPtr.Zero);
            }
        }
    }
}
