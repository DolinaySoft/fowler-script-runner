using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using Jint;
using Jint.Native;
using Jint.Native.Json;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Spoustec.Skript
{
    /// <summary>
    /// Skript v JavaScriptu (interpret Jint). Kazdy beh ma vlastni engine:
    /// - hlavni skript i pod-skripty jsou moduly JavaScriptu: hlavni skript exportuje funkci hodnot(data), pod-skripty
    ///   nacita prikazem import, jen soubory .js ze slozky skriptu (NacitaniSkriptu); require neni;
    /// - strict mode, bez pristupu k CLR (bez AllowClr), k ostatnim souborum a siti;
    /// - k dispozici je jen standardni JavaScript a console.log/info/warn/error (vystup se zachyti, zkraceny);
    /// - data se predaji jako text JSON a prevedou parserem JSON enginu na obycejne objekty JavaScriptu;
    /// - limity: cas a pamet hlida HlidacLimitu pri kazdem prikazu, hloubku volani Jint (LimitRecursion);
    ///   dlouhou operaci uvnitr enginu (napr. velke pole, regularni vyraz), kterou prikazy neprerusi, ukonci
    ///   hlidaci vlakno po limitu casu + 1 s.
    /// </summary>
    public sealed class JazykJavaScript : IJazykSkriptu
    {
        const string Funkce = "hodnot";
        const uint MaxPrvkuPole = 10000000;     //vetsi pole by jen zabiralo pamet; limit pameti hlida HlidacLimitu
        const int MaxRadkuKonzole = 200;
        const int MaxZnakuRadkuKonzole = 1000;

        public static readonly string VerzeJint = Verze.Informacni(typeof(Engine).Assembly);

        public string Popis { get { return "JavaScript (Jint " + VerzeJint + ")"; } }

        public string[] Pripony { get { return new[] { ".js" }; } }

        public VysledekSkriptu Spust(SlozkaSkriptu slozka, string hlavniSkript, string dataJson, Limity limity)
        {
            Stopwatch stopky = Stopwatch.StartNew();
            VysledekSkriptu vysledek = new VysledekSkriptu();
            //zasobnik vlakna podle hloubky rekurze, aby vzdy nastal limit Jintu driv nez preteceni zasobniku
            int zasobnik = Math.Min(256, Math.Max(16, limity.HloubkaRekurze / 16)) * 1024 * 1024;
            Thread vlakno = new Thread(() => Beh(slozka, hlavniSkript, dataJson, limity, vysledek), zasobnik);
            vlakno.IsBackground = true;
            vlakno.Start();
            if (!vlakno.Join(TimeSpan.FromSeconds(limity.CasSekund + 1)))
            {
                vlakno.Abort();
                vlakno.Join(1000);
                VysledekSkriptu prerusen = new VysledekSkriptu();
                prerusen.Chyba = $"překročen časový limit {limity.CasSekund} s (operace uvnitř JavaScriptu se nedokončila)";
                lock (vysledek.Konzole)
                    prerusen.Konzole.AddRange(vysledek.Konzole);
                lock (vysledek.Soubory)
                    prerusen.Soubory.AddRange(vysledek.Soubory);
                vysledek = prerusen;
            }
            vysledek.Doba = stopky.Elapsed;
            return vysledek;
        }

        void Beh(SlozkaSkriptu slozka, string hlavniSkript, string dataJson, Limity limity, VysledekSkriptu vysledek)
        {
            HlidacLimitu hlidac = new HlidacLimitu(limity, slozka);
            NacitaniSkriptu nacitani = new NacitaniSkriptu(slozka, vysledek.Soubory);
            Engine engine = null;
            try
            {
                engine = new Engine(o =>
                {
                    o.Strict();
                    o.LimitRecursion(limity.HloubkaRekurze);
                    o.MaxArraySize(MaxPrvkuPole);
                    o.RegexTimeoutInterval(TimeSpan.FromSeconds(limity.CasSekund));
                    o.Culture(CultureInfo.InvariantCulture);    //vysledek nezavisi na jazyku Windows
                    o.Constraint(hlidac);
                    o.EnableModules(nacitani);
                });
                hlidac.Engine = engine;
                PripojKonzoli(engine, vysledek.Konzole);
                hlidac.Start();

                ObjectInstance modul = engine.Modules.Import("./" + slozka.Relativni(hlavniSkript));
                JsValue funkce = modul.Get(Funkce);
                if (funkce.IsUndefined())
                {
                    vysledek.Chyba = $"hlavní skript neexportuje funkci {Funkce} (musí obsahovat export function {Funkce}(data) {{ ... }})";
                    return;
                }
                if (!funkce.IsCallable())
                {
                    vysledek.Chyba = $"{Funkce} není funkce (hlavní skript musí obsahovat export function {Funkce}(data) {{ ... }})";
                    return;
                }
                JsValue data = new JsonParser(engine).Parse(dataJson);
                JsValue navrat = engine.Call(funkce, new[] { data });
                if (navrat.IsPromise())
                {
                    vysledek.Chyba = $"funkce {Funkce} vrátila Promise (async funkce nejsou podporované)";
                    return;
                }
                vysledek.Vysledek = new PrevodVysledku().Prevod(navrat, 0);
            }
            catch (ThreadAbortException)
            {
                //hlidaci vlakno ukoncilo beh, chybu zapise samo
            }
            catch (ChybaNacteniException ex)
            {
                vysledek.Chyba = ex.Message;
                vysledek.Misto(ex.Soubor, ex.Radek);
            }
            catch (PrekrocenLimitException ex)
            {
                vysledek.Chyba = ex.Message;
                vysledek.Misto(ex.Soubor, ex.Radek);
                vysledek.Podrobnosti = ex.Zasobnik;
            }
            catch (JavaScriptException ex)
            {
                vysledek.Chyba = PopisChybyJs(ex, slozka);
                string soubor = slozka.Relativni(ex.Location.SourceFile);
                if (soubor != null)
                    vysledek.Misto(soubor, ex.Location.Start.Line);
                else
                    MistoZeZasobniku(vysledek, ex.JavaScriptStackTrace, slozka);
                vysledek.Podrobnosti = ex.JavaScriptStackTrace;
            }
            catch (RecursionDepthOverflowException ex)
            {
                vysledek.Chyba = $"překročena hloubka vnoření volání funkcí {limity.HloubkaRekurze} (nekonečná rekurze?" +
                    (string.IsNullOrEmpty(ex.CallExpressionReference) ? ")" : $" funkce {ex.CallExpressionReference})");
                vysledek.Podrobnosti = ex.CallChain;
            }
            catch (RegexMatchTimeoutException)
            {
                vysledek.Chyba = $"regulární výraz se nevyhodnotil v časovém limitu {limity.CasSekund} s";
                MistoZeZasobniku(vysledek, Zasobnik(engine), slozka);
            }
            catch (Exception ex) when (ex is OutOfMemoryException || ex is InsufficientMemoryException)
            {
                vysledek.Chyba = "nedostatek paměti (skript vytváří příliš velká data)";
                MistoZeZasobniku(vysledek, Zasobnik(engine), slozka);
            }
            catch (PrevodException ex)
            {
                vysledek.Chyba = ex.Message;
            }
            catch (Exception ex)
            {
                vysledek.Chyba = "chyba při běhu skriptu: " + BezSlozky(ex.Message, slozka);
                MistoZeZasobniku(vysledek, Zasobnik(engine), slozka);
                vysledek.Podrobnosti = ex.ToString();
            }
            finally
            {
                if (engine != null)
                    engine.Dispose();
            }
        }

        //"TypeError: Cannot read properties of undefined (reading 'R')"; misto na konci zpravy se vynecha (je zvlast),
        //u syntakticke chyby v nacitanem skriptu i uvod Jintu "Error while loading module: error in module '...': "
        static string PopisChybyJs(JavaScriptException ex, SlozkaSkriptu slozka)
        {
            string zprava = Regex.Replace(ex.Message ?? "", @"\s*\([^()]*:\d+:\d+\)\s*$", "");
            Match nacteni = Regex.Match(zprava, @"^Error while loading module: error in module '[^']*': (?<zprava>.*)$", RegexOptions.Singleline);
            if (nacteni.Success)
                return "SyntaxError: " + BezSlozky(nacteni.Groups["zprava"].Value, slozka);
            zprava = BezSlozky(zprava, slozka);
            string nazev = null;
            try
            {
                if (ex.Error.IsObject())
                {
                    JsValue n = ex.Error.AsObject().Get("name");
                    nazev = n.IsString() ? n.AsString() : null;
                }
            }
            catch (Exception)
            {
                //nazev chyby neni nutny
            }
            if (!ex.Error.IsObject())
                return "vyvolána výjimka " + zprava;
            return (string.IsNullOrEmpty(nazev) || zprava.StartsWith(nazev)) ? zprava : nazev + ": " + zprava;
        }

        //plne cesty ke skriptum ve zprave jako cesty vuci slozce skriptu (zprava jde do poznamky ve Fowleru)
        static string BezSlozky(string zprava, SlozkaSkriptu slozka)
        {
            string zaklad = slozka.Cesta.EndsWith("\\") ? slozka.Cesta : slozka.Cesta + "\\";
            return Regex.Replace(zprava, Regex.Escape(zaklad) + @"[^'"":()]*", m => slozka.Relativni(m.Value) ?? m.Value, RegexOptions.IgnoreCase);
        }

        static string Zasobnik(Engine engine)
        {
            try
            {
                return (engine != null) ? engine.Advanced.StackTrace : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// misto chyby z prvniho mista zasobniku Jintu, ktere je ve slozce skriptu
        /// ("   at ztrataFowler (C:\Fowler\skripty\ztrata-fowler.js:14:5)" = ztrata-fowler.js, radek 14)
        /// </summary>
        internal static void MistoZeZasobniku(VysledekSkriptu vysledek, string zasobnik, SlozkaSkriptu slozka)
        {
            string soubor;
            int? radek = MistoZeZasobniku(zasobnik, slozka, out soubor);
            if (soubor != null)
                vysledek.Misto(soubor, radek);
        }

        internal static int? MistoZeZasobniku(string zasobnik, SlozkaSkriptu slozka, out string soubor)
        {
            soubor = null;
            if (string.IsNullOrEmpty(zasobnik))
                return null;
            foreach (string radek in zasobnik.Split('\n'))
            {
                Match m = Regex.Match(radek, @"(?:\(|at\s)(?<src>[^()]*?):(?<radek>\d+):\d+\)?\s*$");
                int cislo;
                string nazev = m.Success ? slozka.Relativni(m.Groups["src"].Value.Trim()) : null;
                if (nazev != null && int.TryParse(m.Groups["radek"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out cislo) && cislo > 0)
                {
                    soubor = nazev;
                    return cislo;
                }
            }
            return null;
        }

        #region Nacitani skriptu

        /// <summary>skript nelze nacist (soubor neni, je mimo slozku skriptu, neni v UTF-8); Soubor a Radek = misto importu</summary>
        sealed class ChybaNacteniException : Exception
        {
            public string Soubor { get; private set; }
            public int? Radek { get; private set; }

            public ChybaNacteniException(string zprava, string soubor, int? radek) : base(zprava)
            {
                Soubor = soubor;
                Radek = radek;
            }
        }

        /// <summary>
        /// Nacitani hlavniho skriptu a pod-skriptu (import): jen soubory .js ve slozce skriptu nebo jejich podslozkach,
        /// zadane relativni cestou ("./ztrata-fowler.js", "./spolecne/cteni-dat.js"). Kazdy nacteny soubor se zaznamena
        /// (SHA-256 do app, logu a protokolu testu). Soubor se cte uz pri vyhledani, aby slo chybu ohlasit s mistem
        /// importu (Jint chybu pri nacteni obsahu nahradi obecnou zpravou).
        /// </summary>
        sealed class NacitaniSkriptu : DefaultModuleLoader
        {
            readonly SlozkaSkriptu slozka;
            readonly List<SouborSkriptu> nactene;
            readonly Dictionary<string, SouborSkriptu> podleCesty = new Dictionary<string, SouborSkriptu>(StringComparer.OrdinalIgnoreCase);

            public NacitaniSkriptu(SlozkaSkriptu slozka, List<SouborSkriptu> nactene) : base(slozka.Cesta, true)
            {
                this.slozka = slozka;
                this.nactene = nactene;
            }

            public override ResolvedSpecifier Resolve(string odkud, ModuleRequest pozadavek)
            {
                string cesta = pozadavek.Specifier ?? "";
                string kdo = slozka.Relativni(odkud);
                int? radek = RadekImportu(odkud, cesta);
                string import = $"import \"{cesta}\"";
                if (!cesta.StartsWith("./") && !cesta.StartsWith("../"))
                    throw new ChybaNacteniException($"{import}: pod-skript se načítá cestou ve složce skriptů, která začíná ./ " +
                        "(např. import { ztrataFowler } from \"./ztrata-fowler.js\")", kdo, radek);
                ResolvedSpecifier vysledek;
                try
                {
                    vysledek = base.Resolve(odkud, pozadavek);
                }
                catch (ModuleResolutionException)
                {
                    throw new ChybaNacteniException($"{import}: skript mimo složku skriptů nelze načíst", kdo, radek);
                }
                string soubor = (vysledek.Type == SpecifierType.RelativeOrAbsolute && vysledek.Uri != null) ? vysledek.Uri.LocalPath : null;
                string nazev = slozka.Relativni(soubor);
                if (nazev == null)
                    throw new ChybaNacteniException($"{import}: skript mimo složku skriptů nelze načíst", kdo, radek);
                if (!string.Equals(Path.GetExtension(soubor), ".js", StringComparison.OrdinalIgnoreCase))
                    throw new ChybaNacteniException($"{import}: načíst lze jen skript s příponou .js", kdo, radek);
                if (!podleCesty.ContainsKey(soubor))
                {
                    if (!File.Exists(soubor))
                        throw new ChybaNacteniException($"{import}: skript {nazev} ve složce skriptů není", kdo, radek);
                    SouborSkriptu skript = SouborSkriptu.Nacti(soubor, nazev);
                    podleCesty[soubor] = skript;
                    lock (nactene)
                        nactene.Add(skript);
                    if (skript.Chyba != null)
                        throw new ChybaNacteniException(skript.Chyba, kdo, radek);
                }
                return vysledek;
            }

            protected override string LoadModuleContents(Engine engine, ResolvedSpecifier vyhledany)
            {
                SouborSkriptu skript;
                if (vyhledany.Uri == null || !podleCesty.TryGetValue(vyhledany.Uri.LocalPath, out skript) || skript.Text == null)
                    throw new ChybaNacteniException($"skript {vyhledany.Key} nelze načíst", null, null);
                return skript.Text;
            }

            //radek importu v nacitajicim skriptu (prvni vyskyt cesty v uvozovkach), null = hlavni skript nebo nenalezeno
            int? RadekImportu(string odkud, string cesta)
            {
                SouborSkriptu skript;
                if (odkud == null || !podleCesty.TryGetValue(odkud, out skript) || skript.Text == null)
                    return null;
                string[] radky = skript.Text.Split('\n');
                for (int i = 0; i < radky.Length; i++)
                {
                    if (radky[i].Contains("\"" + cesta + "\"") || radky[i].Contains("'" + cesta + "'") || radky[i].Contains("`" + cesta + "`"))
                        return i + 1;
                }
                return null;
            }
        }

        #endregion

        #region console

        //console.log/info/warn/error: radek textu do vystupu (zkraceny), nic jineho
        static void PripojKonzoli(Engine engine, List<string> vystup)
        {
            JsObject konzole = new JsObject(engine);
            string[,] funkce = { { "log", "" }, { "info", "" }, { "warn", "varování: " }, { "error", "chyba: " } };
            for (int i = 0; i < funkce.GetLength(0); i++)
            {
                string predpona = funkce[i, 1];
                konzole.FastSetDataProperty(funkce[i, 0], new ClrFunction(engine, funkce[i, 0], (ten, argumenty) =>
                {
                    ZapisDoKonzole(vystup, predpona + string.Join(" ", argumenty.Select(a => TextKonzole(engine, a))));
                    return JsValue.Undefined;
                }, 0, PropertyFlag.Configurable));
            }
            engine.SetValue("console", konzole);
        }

        static string TextKonzole(Engine engine, JsValue hodnota)
        {
            if (hodnota.IsString())
                return hodnota.AsString();
            if (hodnota.IsCallable())
                return "[funkce]";
            if (hodnota.IsObject())
            {
                try
                {
                    JsValue json = new JsonSerializer(engine).Serialize(hodnota);
                    if (json.IsString())
                        return json.AsString();
                }
                catch (JavaScriptException)
                {
                    //napr. cyklicky odkaz
                }
            }
            return TypeConverter.ToString(hodnota);
        }

        static void ZapisDoKonzole(List<string> vystup, string text)
        {
            lock (vystup)
            {
                if (vystup.Count > MaxRadkuKonzole)
                    return;
                if (vystup.Count == MaxRadkuKonzole)
                {
                    vystup.Add($"… další výstup console vynechán (nejvýše {MaxRadkuKonzole} řádků)");
                    return;
                }
                vystup.Add((text.Length > MaxZnakuRadkuKonzole) ? text.Substring(0, MaxZnakuRadkuKonzole) + "…" : text);
            }
        }

        #endregion

        #region Limity

        /// <summary>prekroceny cas nebo pamet; Soubor a Radek = misto ve skriptech, kde se beh zastavil</summary>
        sealed class PrekrocenLimitException : Exception
        {
            public string Soubor { get; private set; }
            public int? Radek { get; private set; }
            public string Zasobnik { get; private set; }

            public PrekrocenLimitException(string zprava, string soubor, int? radek, string zasobnik) : base(zprava)
            {
                Soubor = soubor;
                Radek = radek;
                Zasobnik = zasobnik;
            }
        }

        /// <summary>
        /// Cas a pamet celeho behu skriptu (nacteni skriptu a pod-skriptu, prevod dat, volani hodnot), kontrola pri kazdem prikazu.
        /// Pamet = o kolik se od startu zvetsila spravovana pamet programu; kdyz to vypada na prekroceni, uklidi se
        /// nepotrebna pamet a teprve kdyz ani pak nestaci, beh se zastavi (bezny skript, ktery jen vytvari docasne
        /// hodnoty, tak limit nepresahne).
        /// </summary>
        sealed class HlidacLimitu : Constraint
        {
            readonly Limity limity;
            readonly SlozkaSkriptu slozka;
            readonly long limitPameti;
            Stopwatch cas;
            long zakladPameti;

            public Engine Engine { get; set; }

            public HlidacLimitu(Limity limity, SlozkaSkriptu slozka)
            {
                this.limity = limity;
                this.slozka = slozka;
                limitPameti = (long)limity.PametMB * 1024 * 1024;
            }

            public void Start()
            {
                zakladPameti = GC.GetTotalMemory(true);
                cas = Stopwatch.StartNew();
            }

            public override void Check()
            {
                if (cas == null)
                    return;
                if (cas.Elapsed.TotalSeconds > limity.CasSekund)
                    throw Prekroceno($"překročen časový limit {limity.CasSekund} s (nekonečná smyčka?)");
                if (GC.GetTotalMemory(false) - zakladPameti > limitPameti && GC.GetTotalMemory(true) - zakladPameti > limitPameti)
                    throw Prekroceno($"překročen limit paměti {limity.PametMB} MB");
            }

            public override void Reset()
            {
                //limity plati pro cely beh, Jint vola Reset pri kazdem Execute/Evaluate
            }

            Exception Prekroceno(string zprava)
            {
                string zasobnik = Zasobnik(Engine);
                string soubor;
                int? radek = MistoZeZasobniku(zasobnik, slozka, out soubor);
                return new PrekrocenLimitException(zprava, soubor, radek, zasobnik);
            }
        }

        #endregion

        #region Prevod vysledku

        sealed class PrevodException : Exception
        {
            public PrevodException(string zprava) : base(zprava) { }
        }

        /// <summary>
        /// JsValue na Hodnota bez volani kodu skriptu: jen vlastni vyctove vlastnosti obycejnych objektu a prvky
        /// poli; getter, Proxy, Date, Map apod. se nevyhodnocuji, jen se oznaci (kontrola je ohlasi)
        /// </summary>
        sealed class PrevodVysledku
        {
            const int MaxHloubka = 32;
            const int MaxHodnot = 100000;
            int hodnot;
            readonly HashSet<ObjectInstance> cesta = new HashSet<ObjectInstance>(new PodleOdkazu());

            public Hodnota Prevod(JsValue v, int hloubka)
            {
                if (++hodnot > MaxHodnot)
                    throw new PrevodException($"výsledek funkce {Funkce} je příliš velký (více než {MaxHodnot} hodnot)");
                if (v.IsUndefined())
                    return Hodnota.Undefined;
                if (v.IsNull())
                    return Hodnota.Null;
                if (v.IsBoolean())
                    return Hodnota.Bool(v.AsBoolean());
                if (v.IsNumber())
                    return Hodnota.NoveCislo(v.AsNumber(), TypeConverter.ToString(v));
                if (v.IsString())
                    return Hodnota.Retezec(v.AsString());
                if (v.IsBigInt())
                    return Hodnota.NoveJine("BigInt " + TypeConverter.ToString(v) + "n");
                if (v.IsSymbol())
                    return Hodnota.NoveJine("Symbol");
                if (v.IsCallable())
                    return Hodnota.NoveJine("funkce");
                if (!v.IsObject())
                    return Hodnota.NoveJine(v.Type.ToString());
                ObjectInstance o = v.AsObject();
                if (hloubka >= MaxHloubka)
                    return Hodnota.NoveJine("příliš hluboké vnoření");
                if (!cesta.Add(o))
                    return Hodnota.NoveJine("cyklický odkaz (objekt obsahuje sám sebe)");
                try
                {
                    if (o is JsArray)
                        return Pole((JsArray)o, hloubka);
                    if (o.GetType() == typeof(JsObject))
                        return Objekt(o, hloubka);
                    return Hodnota.NoveJine(PopisObjektu(o));
                }
                finally
                {
                    cesta.Remove(o);
                }
            }

            Hodnota Pole(JsArray pole, int hloubka)
            {
                ulong delka = TypeConverter.ToLength(pole.GetOwnProperty("length").Value);
                if (delka > MaxHodnot)
                    throw new PrevodException($"výsledek funkce {Funkce} obsahuje seznam s {delka} prvky");
                Hodnota h = Hodnota.NovePole();
                for (int i = 0; i < (int)delka; i++)
                {
                    PropertyDescriptor d = pole.GetOwnProperty(i.ToString(CultureInfo.InvariantCulture));
                    if (d == PropertyDescriptor.Undefined)
                        h.Prvky.Add(Hodnota.NoveJine("prázdné místo v seznamu"));
                    else if (d.IsAccessorDescriptor())
                        h.Prvky.Add(Hodnota.NoveJine("vlastnost s getterem (get)"));
                    else
                        h.Prvky.Add(Prevod(d.Value, hloubka + 1));
                }
                return h;
            }

            Hodnota Objekt(ObjectInstance o, int hloubka)
            {
                Hodnota h = Hodnota.NovyObjekt();
                foreach (JsValue klic in o.GetOwnPropertyKeys(Types.String))
                {
                    PropertyDescriptor d = o.GetOwnProperty(klic);
                    if (d == PropertyDescriptor.Undefined || !d.Enumerable)
                        continue;
                    Hodnota hodnota = d.IsAccessorDescriptor() ? Hodnota.NoveJine("vlastnost s getterem (get)") : Prevod(d.Value, hloubka + 1);
                    h.Polozky.Add(new KeyValuePair<string, Hodnota>(klic.AsString(), hodnota));
                }
                return h;
            }

            static string PopisObjektu(ObjectInstance o)
            {
                if (o.IsDate())
                    return "datum (Date)";
                if (o.IsRegExp())
                    return "regulární výraz (RegExp)";
                if (o.IsPromise())
                    return "Promise";
                string nazev = o.GetType().Name;
                if (nazev.StartsWith("Js"))
                    nazev = nazev.Substring(2);
                if (nazev.EndsWith("Instance"))
                    nazev = nazev.Substring(0, nazev.Length - "Instance".Length);
                return "objekt typu " + nazev;
            }
        }

        sealed class PodleOdkazu : IEqualityComparer<ObjectInstance>
        {
            public bool Equals(ObjectInstance a, ObjectInstance b) { return ReferenceEquals(a, b); }
            public int GetHashCode(ObjectInstance o) { return RuntimeHelpers.GetHashCode(o); }
        }

        #endregion
    }
}
