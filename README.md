# Spouštěč skriptů pracoviště pro Fowler

Program **Fowler** (přenos dat z audiometrů a tympanometrů) umí předat uložené vyšetření externímu programu pracoviště
a jeho odpověď vložit do okna Hodnotit, kde ji uživatel zkontroluje.

**Spouštěč** (`spoustec.exe`) je obecné rozhraní pro spuštění skriptů pracoviště nad daty z Fowleru. Je to hotový
externí program podle šablony [fowler-external-program](https://github.com/DolinaySoft/fowler-external-program):
převezme data vyšetření od Fowleru, spustí skript v JavaScriptu, který si pracoviště samo napíše, výstup skriptu
formálně zkontroluje a předá Fowleru. **Spouštěč sám nic nepočítá ani nevyhodnocuje.** Co skripty vrátí, určuje
výhradně pracoviště, které je vytvoří a používá.

Pracoviště tak nemusí překládat vlastní program, dodá jen skripty, např. s pomocí AI asistenta (pokyny pro něj jsou
v [AGENTS.md](AGENTS.md)). Skripty jsou v jedné složce: pro každou část vyšetření jeden hlavní skript (`audio.js`,
`tymp.js`, `speech.js`) a k nim pod-skripty, z nichž každý určí jedno pole hodnocení (např. ztrátu dle Fowlera).
Pracoviště tak může mít naprogramovanou jen část hodnocení, Fowler vloží jen to, co skripty určí.

## Dvě cesty: spouštěč skriptů, nebo vlastní program

Spouštěč je nejjednodušší cesta pro pracoviště: program je hotový, pracoviště píše jen skripty. Vývojář nebo
dodavatel softwaru, který potřebuje vlastní program, nemusí začínat od nuly: šablona
[**fowler-external-program**](https://github.com/DolinaySoft/fowler-external-program) obsahuje kostru programu
v C# i v Pythonu a popis formátu dat.

| | spouštěč skriptů (tento repozitář) | [šablona vlastního programu](https://github.com/DolinaySoft/fowler-external-program) |
|---|---|---|
| pro koho | pracoviště, lékař (i s pomocí AI asistenta) | vývojář pracoviště nebo dodavatel softwaru |
| co pracoviště dodá | jen skripty v JavaScriptu, bez překladu a vývojových nástrojů | vlastní program v C# nebo Pythonu |
| co je hotové | celý program: převzetí dat, kontrola výsledku, testovací režim s protokolem, otisk skriptů v odpovědi | kostra programu: čtení dat a zápis odpovědi |
| volnost | jen data předaná Fowlerem, standardní JavaScript, limity času a paměti | libovolný jazyk a knihovny, napojení na vlastní systémy |
| kdy zvolit | hodnocení chcete naprogramovat co nejjednodušeji | potřebujete víc, než skripty umožní, nebo píšete vlastní implementaci |

Formát předaných dat a odpovědi je pro obě cesty stejný ([docs/format.md](docs/format.md), kopie ze šablony) a Fowler
mezi nimi nerozlišuje. Žádná z nich neobsahuje hodnocení, to vždy dodá pracoviště. Kdo píše vlastní program, může se
spouštěčem inspirovat: je to jedna hotová implementace šablony (zdrojový kód v [src](src), C#).

## Odpovědnost

- **Spouštěč nic nevyhodnocuje.** Jen spustí skript uživatele a jeho výstup formálně zkontroluje (formát, rozsahy
  polí). Neobsahuje žádný postup hodnocení, žádné odborné konstanty ani pomocné výpočty a bez skriptu pracoviště nic
  neurčí.
- **Za skript a jeho výsledky odpovídá pracoviště**, které ho vytvoří a používá. Skript, který z naměřených dat
  určuje hodnocení, může být zdravotnickým prostředkem podle nařízení (EU) 2017/745 (MDR). Pracoviště, které ho
  používá bez označení CE, musí splnit podmínky čl. 5 odst. 5 MDR (mimo jiné ho nepředávat jiné právnické osobě).
- **Výsledky skriptu Fowler vkládá jako návrh.** Uživatel je musí zkontrolovat, případně opravit, a potvrdit.
  Potvrzené hodnocení se ve Fowleru uloží s označením „převzato z externího programu“ a se jménem uživatele.
- **Spouštěč skriptu nepředává osobní údaje pacienta** (jméno, rodné číslo, datum narození, pojišťovnu ani
  identifikátor pacienta). Do logu je nezapisuje.
- Fowler odpověď kontroluje také jen formálně, ne věcně. Popis formátu dat a odpovědi je
  v [docs/format.md](docs/format.md).

## Co spouštěč s daty dělá a nedělá

Popis je určený i pro dokumentaci pracoviště, které rozhraní posuzuje.

**Dělá:**

1. Přečte data předaná Fowlerem (`fowler-exam`, UTF-8). Neplatný JSON nebo jiný formát ohlásí návratovým kódem
   a nic dalšího nedělá.
2. Zapamatuje si identifikaci vyšetření přesně tak, jak byla předána: `patient.pid`, `patient.rc` a `exam.time`.
3. **Odebere celý blok `patient`.** Zbytek dat (hlavička, `exam` s věkem a pohlavím, předaná část vyšetření, včetně
   klíčů, které spouštěč nezná) předá skriptu beze změny jako obyčejné objekty JavaScriptu.
4. Ve složce skriptů najde **hlavní skript předané části** (`audio.js`, `tymp.js` nebo `speech.js`). Když tam není,
   nic neurčí a do poznámky napíše, že pro tuto část skript chybí.
5. Spustí hlavní skript v odděleném prostředí: přísný režim (strict mode), bez přístupu k systému, síti a souborům
   s jedinou výjimkou: hlavní skript si smí příkazem `import` načíst pod-skripty (soubory `.js`) ze složky skriptů
   a jejích podsložek. K dispozici je jen standardní JavaScript a `console.log`. Běh hlídají limity (viz níže).
6. Zavolá funkci `hodnot(data)` hlavního skriptu a výsledek **formálně zkontroluje** podle části „Odpověď programu“
   v [docs/format.md](docs/format.md): jen předaná část, známé klíče, typy, rozsahy, počet desetinných míst a znaků
   čísel, délky textů, seznamy reflexů a decay. Pole, které neodpovídá, **vynechá** a do poznámky napíše proč, např.
   „audio.acAvg.R vynecháno: 0.30000000000000004 má víc desetinných míst, než pole dovoluje (nejvýše 2) – zaokrouhlete
   ve skriptu.“ Ostatní platná pole zapíše. Pole, které skript nevrátí (nebo vrátí `null`), Fowler nechá, jak je.
7. Zapíše odpověď (`fowler-eval`) s identifikací ze vstupu, platnými poli a poznámkou (poznámky skriptu
   a upozornění kontroly, nejvýše 2000 znaků). V položce `app` je verze spouštěče, hlavní skript a začátek otisku
   SHA-256 všech načtených skriptů (hlavního i pod-skriptů), např. `spoustec 1.0.0; audio.js #1a2b3c4d`. Podle toho
   je dohledatelné, kterou verzí skriptů hodnocení vzniklo: změna kteréhokoli z nich otisk změní.
8. Zapíše záznam do logu (bez osobních údajů) se seznamem načtených skriptů a jejich SHA-256.

**Nedělá:**

- nic nepočítá, nedoplňuje, neprůměruje ani nehodnotí,
- **nemění ani nezaokrouhluje** hodnoty, které skript vrátí; neplatnou hodnotu neopraví, ale vynechá,
- nekontroluje věcnou správnost výsledku (jen tvar, který přijmou pole Fowleru),
- nepředává skriptu osobní údaje pacienta.

**Chování při chybě skriptu:** když skript nejde spustit nebo selže (složka nenalezena, pod-skript nenalezen,
syntaktická chyba, výjimka v hlavním skriptu nebo v kterémkoli pod-skriptu, chybí funkce `hodnot`, překročený čas
nebo paměť), spouštěč zapíše odpověď **bez hodnocení** s poznámkou, která uvede soubor a řádek, např.
„Skript selhal: TypeError: Cannot read properties of undefined (reading 'R') (ztrata-fowler.js, řádek 14)“, a skončí
s kódem 0. Uživatel tak chybu uvidí přímo ve Fowleru. Podrobnosti jsou v logu. Ani pole, která jiné pod-skripty
určily, se v takovém případě nevloží.

**Limity běhu skriptu:**

| limit | výchozí | |
|---|---|---|
| čas | 5 s | celý běh skriptu (načtení hlavního skriptu a pod-skriptů i volání `hodnot`) |
| paměť | 64 MB | o kolik může skript zvětšit paměť programu |
| hloubka rekurze | 256 | největší hloubka vnoření volání funkcí |

Limity lze změnit v souboru `spoustec.ini` ve složce spouštěče ([docs/skripty.md](docs/skripty.md#limity)).
Bez něj platí výchozí hodnoty.

## Stažení a instalace

1. Stáhněte `fowler-script-runner-<verze>.zip` z [Releases](https://github.com/DolinaySoft/fowler-script-runner/releases).
   Otisk SHA-256 ZIPu je v popisu releasu, ověřit ho můžete v PowerShellu: `Get-FileHash fowler-script-runner-<verze>.zip`.
2. Program není digitálně podepsaný. Před rozbalením otevřete Vlastnosti staženého ZIPu a zaškrtněte
   **Odblokovat**, jinak může Windows nebo antivir spuštění zastavit.
3. Rozbalte ZIP do složky Fowleru. Vznikne složka `spoustec` s programem, dokumentací a příklady. Krátký návod
   k nastavení ve Fowleru je v `readme.txt`. Složka `skripty` obsahuje bezpečnou ukázku (kopie
   [priklady/ukazka](priklady/ukazka)): nic nehodnotí a do polí okna nic nevloží, jen do zprávy popíše předaná data.
   Spouštěč ji použije, když ve Fowleru není zadaná složka skriptů, takže lze hned vyzkoušet, že předání funguje.

Program běží bez instalace a bez práv správce z libovolné složky. Potřebuje .NET Framework 4.8, který je součástí
Windows 10 a 11.

## Nastavení ve Fowleru

**Nastavení programu → Extra... → Externí program pro hodnocení měření.** V INI Fowleru (`fowler.ini`):

```ini
[HODNOCENI]
program=spoustec\spoustec.exe
parametry="%VSTUP%" "%VYSTUP%" "C:\Fowler\skripty"
```

- `program`: cesta ke `spoustec.exe` (relativní cesta je vůči složce Fowleru).
- `parametry`: vstupní a výstupní soubor (doplní Fowler) a **složka skriptů** pracoviště. Relativní cesta je vůči
  složce spouštěče. Bez třetího parametru se použije složka `skripty` ve složce spouštěče.
  Některá zkušební sestavení Fowleru při čtení `fowler.ini` odebírala uvozovky na začátku a konci hodnoty (Windows
  to dělá u každé hodnoty, která uvozovkou začíná i končí), takže spouštěč dostal `vstup" "výstup" "složka`.
  Spouštěč takový tvar pozná a uvozovky si doplní, v nastavení je proto pište tak, jak je uvedeno výše.
- Ostatní nastavení (`vstup`, `vystup`, `automaticky`) popisuje [docs/format.md](docs/format.md#spuštění-programu).

Složku skriptů doporučujeme mít mimo složku spouštěče, aby ji nová verze spouštěče nepřepsala. Spouštěč čte skripty
při každém spuštění, změna skriptů platí hned pro další hodnocení (bez restartu Fowleru). Pracoviště s více
počítači může mít složku skriptů na sdíleném disku, nebo ji na počítače kopírovat. Podle otisku v `app` je vidět,
kterou verzí skriptů které hodnocení vzniklo.

## Skripty pracoviště

```
C:\Fowler\skripty\        složka skriptů (ve Fowleru 3. parametr)
  audio.js                hlavní skript pro tónovou audiometrii (okno Hodnotit audiogramu)
  tymp.js                 hlavní skript pro tympanometrii (okno Hodnotit tympanometrie)
  speech.js               hlavní skript pro slovní audiometrii (pole SRT)
  ztrata-fowler.js        pod-skripty pracoviště, každý určí jedno pole (název podle pracoviště)
  who.js
  cteni-dat.js            společné funkce pro čtení dat (ze šablony)
  testy\                  testovací případy pro --test
```

Fowler předává vždy jen jednu část vyšetření. Spouštěč spustí její hlavní skript a zavolá jeho funkci `hodnot(data)`.
Hlavní skript sám nic nepočítá, jen zavolá pod-skripty a předá jejich výsledky:

```js
// audio.js – hlavní skript pro tónovou audiometrii
import { ztrataFowler } from "./ztrata-fowler.js";
import { ztrataWho } from "./who.js";

export function hodnot(data) {
  // data = předané vyšetření bez údajů o pacientovi (docs/format.md)
  const poznamky = [];
  const audio = {};
  audio.fowler = ztrataFowler(data.audio, poznamky, data);   // pole podle docs/format.md, Odpověď programu
  audio.who = ztrataWho(data.audio, poznamky, data);
  return { audio: audio, note: poznamky };
}
```

Pole, pro které pracoviště pod-skript nemá, hlavní skript nevrací a Fowler ho nechá, jak je. Hlavní skript pro
část, kterou pracoviště nehodnotí, ve složce být nemusí.

- **Jak skripty napsat** (postup, dotazník metodiky, práce s daty, kontrolní seznam): [docs/navod.md](docs/navod.md).
- Rozhraní, pod-skripty, běh, limity, testovací režim a úskalí JavaScriptu: [docs/skripty.md](docs/skripty.md).
- Data a pole odpovědi: [docs/format.md](docs/format.md).
- Ukázky (žádná nic nehodnotí):
  - [priklady/sablona](priklady/sablona): šablona složky skriptů, ze které se začíná (hlavní skripty bez
    zapojených pod-skriptů, kostra pod-skriptu `sablona-podskriptu.js`, společné funkce `cteni-dat.js`),
  - [priklady/ukazka](priklady/ukazka): jak číst audiometrii, tympanometrii a slovní audiometrii
    (pod-skript `prace-s-daty.js` jen popíše předaná data) a jak v pod-skriptu počítat (`ukazka-vypoctu.js`
    počítá veličinu bez audiologického významu, průměrnou frekvenci naměřených prahů; výsledek jde jen do poznámky,
    do žádného pole okna).
- S pomocí AI: dejte asistentovi přečíst [AGENTS.md](AGENTS.md) a popište mu postup hodnocení svého pracoviště
  (odpovědi na dotazník metodiky). Spouštěč žádný postup neobsahuje a asistent ho nemá vymýšlet.

**Než skripty začnete používat,** ověřte jejich výsledky na vlastních vyšetřeních. Testovací režim (`--test`)
porovná výsledky skriptů s hodnocením, které určil lékař, a uloží protokol do validační dokumentace pracoviště.

## Příkazová řádka

| volání | význam |
|---|---|
| `spoustec.exe "<vstup>" "<výstup>" ["<složka skriptů>"]` | běh z Fowleru (bez okna) |
| `spoustec.exe --run "<složka skriptů>" "<vstup.json>" ["<výstup.json>"]` | ladění bez Fowleru: vypíše načtené skripty, výstup `console.log`, poznámky a odpověď |
| `spoustec.exe --test "<složka skriptů>" "<složka s testy>"` | testovací režim s protokolem ([docs/skripty.md](docs/skripty.md#testovací-režim)) |
| `spoustec.exe` | jen informace o programu |

Spouštěč je program bez vlastní konzole (aby při spuštění z Fowleru nic neblikalo). Výstup `--run` a `--test` píše
do konzole, ze které byl spuštěn, a zároveň do souboru (`--run` do `spoustec-run.txt` vedle logu, `--test` do
protokolu). Příkazový řádek na jeho konec nečeká, proto spouštějte takto:

```bat
rem příkazový řádek (cmd)
start /wait spoustec.exe --run priklady\ukazka priklady\audio.json
echo %ERRORLEVEL%
```

```powershell
# PowerShell
.\spoustec.exe --run priklady\ukazka priklady\audio.json | Out-Host
$LASTEXITCODE
```

## Návratové kódy

| kód | význam |
|---|---|
| 0 | odpověď zapsána (i když skript selhal, chybí nebo nic neurčil, pak je v ní jen poznámka); `--test`: všechny případy OK |
| 1 | chybí parametry (a spuštění bez parametrů) |
| 2 | vstupní soubor nelze přečíst nebo neobsahuje platná data; `--test`: složka skriptů nebo složka s testy neexistuje nebo je prázdná |
| 3 | data v jiném formátu nebo novější verzi, než spouštěč zná |
| 4 | odpověď nebo protokol nelze zapsat |
| 5 | `--test`: některý případ skončil ROZDÍL nebo CHYBA |

Fowler při jiném kódu než 0 ohlásí chybu a nic nevloží.

## Log

`spoustec.log` ve složce spouštěče. Když do ní nelze zapisovat (např. Program Files), použije se
`%LOCALAPPDATA%\fowler-script-runner\`. Obsahuje čas, verzi, složku skriptů, předanou část, načtené skripty s plným
SHA-256 a otisk, dobu běhu, výstup `console.log` (zkrácený), poznámky a chyby. Osobní údaje pacienta neobsahuje (skript je
nedostává). Nad 1 MB se log přejmenuje na `spoustec.log.old`.

Výstup `console.log` se do logu zapisuje, proto do něj skript nemá vypisovat celá data vyšetření ani poznámku
k vyšetření (`note`), do které uživatel mohl napsat cokoli.

## Obsah repozitáře

```
README.md                 tento popis
AGENTS.md                 pokyny pro AI asistenta, který pomáhá napsat skript
LICENSE                   licence MIT
THIRD-PARTY-NOTICES.md    licence použitých knihoven (Jint a jeho závislosti)
docs/format.md            formát předaných dat a odpovědi (kopie z fowler-external-program)
docs/navod.md             jak napsat skripty: postup, dotazník metodiky, práce s daty, kontrolní seznam
docs/skripty.md           rozhraní skriptů, pod-skripty, běh, limity, testovací režim, úskalí JavaScriptu
priklady/                 vzorová data (smyšlený pacient) a testy k ukázce
priklady/sablona/         šablona složky skriptů pracoviště (nic neurčí, dokud pracoviště nedoplní pod-skripty)
priklady/ukazka/          ukázka čtení dat a techniky výpočtu (výpočet bez audiologického významu, nic nehodnotí)
release/readme.txt        krátký návod k nastavení ve Fowleru do ZIPu releasu
src/Spoustec/             zdrojový kód spouštěče (C#, .NET Framework 4.8)
tests/                    automatické testy spouštěče (syntetické skripty, nejsou součástí releasu)
.github/workflows/        překlad a testy (build.yml), release při tagu v* (release.yml)
```

## Překlad

Potřeba je .NET SDK (8 nebo novější) a Windows. Visual Studio 2022 otevře `fowler-script-runner.sln`.

```
dotnet build -c Release
dotnet test -c Release
```

Program je v `src\Spoustec\bin\Release\net48\` (`spoustec.exe`, `spoustec.exe.config` a knihovny `*.dll`).
JavaScript interpretuje knihovna [Jint](https://github.com/sebastienros/jint) (licence BSD-2-Clause).

## Licence

[MIT](LICENSE), © 2026 Viliam Dolinay. Program se poskytuje bez záruky. Za skripty, které s ním pracoviště používá,
a jejich výsledky odpovídá pracoviště (viz [Odpovědnost](#odpovědnost)). Licence použitých knihoven jsou
v [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
