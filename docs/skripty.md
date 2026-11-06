# Skripty pro spouštěč

Popis pro lékaře, vývojáře pracoviště i jejich AI asistenta. Spouštěč žádné hodnocení neobsahuje. Co skripty určí,
a jakým postupem, rozhoduje pracoviště, které je vytvoří a používá (viz [README](../README.md#odpovědnost)).
Postup, jak skripty napsat (dotazník metodiky, šablona, testy), je v [navod.md](navod.md).

Obsah: [Složka skriptů](#složka-skriptů) · [Hlavní skript](#hlavní-skript) · [Pod-skripty](#pod-skripty) ·
[Data](#data) · [Výsledek](#výsledek) · [Formální kontrola](#formální-kontrola) · [Běh skriptu](#běh-skriptu) ·
[Limity](#limity) · [Otisk skriptů](#otisk-skriptů) · [Ladění](#ladění---run) · [Testovací režim](#testovací-režim) ·
[Úskalí JavaScriptu](#úskalí-javascriptu)

## Složka skriptů

Skripty pracoviště jsou soubory v JavaScriptu s příponou `.js`, uložené v kódování UTF-8 (BOM nevadí), v jedné
složce. Tu Fowler předá spouštěči jako třetí parametr (README, Nastavení ve Fowleru).

```
C:\Fowler\skripty\
  audio.js             hlavní skript pro tónovou audiometrii
  tymp.js              hlavní skript pro tympanometrii
  speech.js            hlavní skript pro slovní audiometrii
  ztrata-fowler.js     pod-skripty: každý určí jedno pole hodnocení (název volí pracoviště)
  who.js
  cteni-dat.js         společné funkce pro čtení dat (ze šablony)
  spolecne\…           pod-skripty mohou být i v podsložkách
  testy\               testovací případy pro --test (nejsou skripty, spouštěč je při běhu z Fowleru nečte)
```

- **Hlavní skript** se jmenuje stejně jako předaná část dat: `audio.js`, `tymp.js`, `speech.js`. Fowler předává vždy
  jen jednu část, spouštěč spustí jen její hlavní skript. Když ve složce není, nic se neurčí a poznámka to uvede
  („Pro část tymp (tympanometrie) není ve složce skriptů hlavní skript tymp.js, nic se neurčuje“). Pracoviště tedy
  nemusí mít skript pro všechny části.
- **Pod-skripty** si hlavní skript načte příkazem `import`. Názvy a rozdělení volí pracoviště, doporučení: jeden
  pod-skript = jedno pole odpovědi (ztráta dle Fowlera, WHO, průměr…).
- Šablona složky je [priklady/sablona](../priklady/sablona), ukázka čtení dat a techniky výpočtu
  [priklady/ukazka](../priklady/ukazka) (výpočet bez audiologického významu v `ukazka-vypoctu.js`, jen do poznámky).

## Hlavní skript

Hlavní skript je modul JavaScriptu, který **exportuje** funkci `hodnot(data)`:

```js
// audio.js
import { ztrataFowler } from "./ztrata-fowler.js";

export function hodnot(data) {
  // data = předané vyšetření BEZ bloku patient, jinak beze změny (fmt, v, app, created, exam, audio|tymp|speech
  //        včetně klíčů, které spouštěč nezná)
  const poznamky = [];
  const audio = {};
  audio.fowler = ztrataFowler(data.audio, poznamky, data);    // pole podle docs/format.md
  return {
    audio: audio,      // jen předaná část (v tymp.js tymp, ve speech.js speech)
    note: poznamky     // nepovinné: text nebo seznam textů
  };
  // return null (nebo nic) = skript nic neurčil
}
```

- Bez `export` funkci spouštěč nenajde („hlavní skript neexportuje funkci hodnot“).
- Hlavní skript sám nepočítá, jen zapojí pod-skripty: na každé pole jeden `import` a jedno přiřazení. Pole, které
  pracoviště neurčuje, v hlavním skriptu není.
- Spouštěč skripty pro každé vyšetření načte znovu v novém prostředí, mezi dvěma běhy si nic nepamatují.

## Pod-skripty

Pod-skript je soubor `.js` ve složce skriptů, který exportuje funkci (nebo konstanty). Hlavní skript nebo jiný
pod-skript ho načte příkazem `import` s **relativní cestou začínající `./`** (nebo `../` z podsložky):

```js
// ztrata-fowler.js – pod-skript pracoviště (výpočet podle metodiky pracoviště)
import { hodnotaNa } from "./cteni-dat.js";

export function ztrataFowler(audio, poznamky, data) {
  // ... výpočet podle metodiky pracoviště ...
  // hodnota pole ve tvaru z docs/format.md, nebo null, když ji nelze určit (důvod do poznamky)
}
```

Doporučené rozhraní pod-skriptu (používá ho šablona, spouštěč ho nevynucuje):

| parametr | obsah |
|---|---|
| 1. | předaná data, se kterými pod-skript počítá: `data.audio`; u tympanometrie data ucha `data.tymp.R`; u slovní audiometrie křivka `data.speech.R` (`undefined`, když nebyla předána) |
| 2. `poznamky` | seznam poznámek pro uživatele, pod-skript do něj přidá důvod, proč pole nelze určit |
| 3. `data` | celá předaná data (např. věk `data.exam.age`) |
| vrací | hodnotu jednoho pole ve tvaru z [format.md](format.md#odpověď-programu-fowler-eval), nebo `null` |

Pravidla načítání:

- Načíst lze jen soubory `.js` ze složky skriptů a jejích podsložek. Cesta mimo složku (`../jinde/x.js`), absolutní
  cesta, knihovna podle jména (`import x from "lodash"`) ani `require` nejdou.
- Pod-skript, který chybí, je chyba: „Skript selhal: import "./who.js": skript who.js ve složce skriptů není
  (audio.js, řádek 2)“.
- Každý soubor se načte jednou za běh, i když ho importuje víc skriptů. Pod-skripty si mezi sebou mohou funkce
  předávat (např. WHO z průměru vzdušného vedení).
- Používejte jen statický `import` na začátku souboru. Dynamický `import()` vrací Promise a jeho výsledek se do
  odpovědi nedostane.

## Data

Funkce dostane data přesně tak, jak je předal Fowler (popis v [format.md](format.md#předávaná-data-fowler-exam)),
jen bez bloku `patient`. Čísla, `null`, texty i seznamy jsou obyčejné hodnoty JavaScriptu.

| | |
|---|---|
| `data.exam.time` | čas vyšetření, text `"2026-10-28T09:42:00"` |
| `data.exam.age.y`, `data.exam.age.m` | věk v den vyšetření (roky, měsíce); chybí, když není znám |
| `data.exam.sex` | `"M"` nebo `"F"`; chybí, když není známo |
| `data.audio`, `data.tymp`, `data.speech` | předaná část vyšetření. Fowler předává vždy jen jednu, podle okna, ze kterého uživatel program spustil |

Na co myslet:

- **Chybějící údaj ve vstupu vůbec není** (`data.exam.age` může být `undefined`). V seznamech hodnot znamená
  `null` „neměřeno“.
- V tónové audiometrii je v `ac` a `bc` místo čísla text **`"NR"`** (neslyší).
- Frekvence nejsou pevné: `data.audio.hz` obsahuje jen frekvence, na kterých je aspoň jedna hodnota. Hodnotu
  hledejte podle frekvence (`hz.indexOf(1000)`), ne podle pořadí.
- `legacy: true` znamená vyšetření uložené starší verzí Fowleru. U audiometrie může kostní vedení obsahovat odhad.
- Ukázková data se smyšleným pacientem jsou v [priklady](../priklady). Čtecí funkce, které vracejí stav hodnoty
  (číslo, `NR`, neměřeno), jsou v [cteni-dat.js](../priklady/sablona/cteni-dat.js), jejich použití ukazuje
  [priklady/ukazka/prace-s-daty.js](../priklady/ukazka/prace-s-daty.js), výpočet s nimi
  [priklady/ukazka/ukazka-vypoctu.js](../priklady/ukazka/ukazka-vypoctu.js).

## Výsledek

Funkce `hodnot` hlavního skriptu vrátí objekt s částí, která byla předána, a případně poznámkou:

| klíč | obsah |
|---|---|
| `audio` | pole okna Hodnotit audiogramu: `fowler`, `acAvg`, `bcAvg` (každé s `R`, `L`, `total`), `who`, `group`, `sisi` (`R`, `L`) |
| `tymp` | pro ucho `R`, `L`: `type`, `ipsi`, `contra`, `decayIpsi`, `decayContra` |
| `speech` | `srt` (`R`, `L`, `bin`), `aided.srt` (`R`, `L`, `bin`) |
| `note` | zpráva pro uživatele: text nebo seznam textů; Fowler ji zobrazí po vložení odpovědi |

Význam, hodnoty a rozsahy polí jsou v [format.md, část Odpověď programu](format.md#odpověď-programu-fowler-eval).

- Údaj, který skript neurčuje, **vynechte nebo vraťte `null`**. Pole v okně Fowleru pak zůstane, jak je. Prázdný
  objekt (např. `tymp: { R: {}, L: {} }` ze šablony, když pod-skripty nic neurčily) se vynechá také.
- Co nelze určit (např. chybí potřebné hodnoty), nevracejte a důvod napište do `note`.
- Identifikaci vyšetření (`patient`, `exam.time`) ani hlavičku odpovědi skript nevrací, doplní je spouštěč.

```js
return {
  tymp: {
    R: { type: "...", ipsi: [ { stim: 1000, res: "+" } ], decayIpsi: [ { stim: 500, pct: -20 } ] }
  },
  note: ["první řádek zprávy", "druhý řádek"]
};
```

## Formální kontrola

Spouštěč výsledek zkontroluje podle [format.md](format.md#odpověď-programu-fowler-eval) dřív, než ho předá
Fowleru. Fowler by totiž při jediném neplatném údaji odmítl celou odpověď. **Nic neopravuje ani nezaokrouhluje:**
pole, které neodpovídá, vynechá a do poznámky napíše proč. Ostatní platná pole zapíše.

| co se kontroluje | pravidlo |
|---|---|
| část | jen ta, která byla předána (a ke které hlavní skript patří). Jiná část se vynechá („tymp vynecháno: Fowler předal část audio…“) |
| klíče | jen klíče z format.md. Neznámý klíč (např. překlep `acAVG`) se vynechá s nápovědou („myslíte acAvg?“) |
| čísla `fowler` | 0 až 100, nejvýše 2 desetinná místa a 5 znaků zápisu (např. `12.25`, `100`) |
| čísla `acAvg`, `bcAvg` | −10 až 130, nejvýše 2 desetinná místa a 5 znaků zápisu (`129.99` má 6 znaků, `100.5` projde) |
| `srt` | −10 až 120, nejvýše 1 desetinné místo |
| texty `who`, `group`, `sisi` | nejvýše 100 znaků; `type` nejvýše 10 znaků; bez řídicích znaků (konců řádků), ne prázdné |
| `ipsi`, `contra` | seznam `{ stim, res }`, `res` je `"+"` nebo `"-"` |
| `decayIpsi`, `decayContra` | seznam `{ stim, pct }`, `pct` je celé číslo nebo `"-"` (neměřitelné) |
| `stim` | frekvence v Hz (celé číslo 1 až 99 999) nebo `"LP"`, `"HP"`, `"WB"` |
| stejný podnět v seznamu dvakrát | vynechají se všechny jeho položky |
| hodnoty, které JSON nemá | `NaN`, `Infinity`, `undefined`, funkce, `Date`, getter apod. se ohlásí a vynechají |

Počet desetinných míst se posuzuje podle zápisu čísla v JavaScriptu (`String(x)`). `0.1 + 0.2` je
`0.30000000000000004`, má tedy 17 desetinných míst a do pole se nezapíše:

```
Kontrola spouštěče:
- audio.acAvg.R vynecháno: 0.30000000000000004 má víc desetinných míst, než pole dovoluje (nejvýše 2) – zaokrouhlete ve skriptu.
```

Hodnota `null` a chybějící údaj se vynechají bez poznámky (skript údaj neurčuje). Položka seznamu reflexů bez `res`
(decay bez `pct`) se vynechá také bez poznámky. Rozsahy `stim` a `pct` jsou technické meze čtení odpovědi ve Fowleru.
Kontrola posuzuje jen tvar, ne věcnou správnost: to, že hodnota prošla, neznamená, že je správná.

**Poznámka v odpovědi** = poznámky skriptu a pod nimi upozornění kontroly (nadpis „Kontrola spouštěče:“), nejvýše
2000 znaků, víc Fowler nezobrazí. Při delším textu se zkrátí poznámka skriptu, upozornění kontroly zůstanou.

## Běh skriptu

- Prostředí: JavaScript podle současného standardu ECMAScript (interpret [Jint](https://github.com/sebastienros/jint)),
  **přísný režim** (strict mode: např. přiřazení do nedeklarované proměnné je chyba).
- K dispozici je jen standardní JavaScript (`Math`, `JSON`, `Array`, `Date`…), `import` pod-skriptů ze složky
  skriptů a `console.log`, `console.info`, `console.warn`, `console.error`. Skript nemá přístup k jiným souborům,
  síti, systému ani k jiným programům.
- `console.log` zapisuje do logu spouštěče (nejvýše 200 řádků po 1000 znacích), při ladění přes `--run` se vypíše.
  Objekty se vypíšou jako JSON, ve kterém je `NaN` a `Infinity` zapsané jako `null` a `undefined` chybí. Do logu
  nevypisujte celá data vyšetření ani poznámku k vyšetření (`note`), do které uživatel mohl napsat cokoli.
- Čísla a texty se formátují nezávisle na jazyku Windows (`(12.5).toLocaleString()` je `"12.5"`). Datum a čas
  (`Date`) jsou v místním časovém pásmu počítače.
- Funkce `hodnot` nesmí být `async`.
- **Když skript selže** (syntaktická chyba, výjimka, chybí pod-skript nebo funkce `hodnot`, překročený limit),
  odpověď je bez hodnocení a poznámka uvádí chybu, soubor a řádek, např. „Skript selhal: ReferenceError: prumer is
  not defined (ztrata-fowler.js, řádek 14)“. Fowler ji zobrazí a nic nevloží. Platí to pro chybu v kterémkoli
  pod-skriptu: nevloží se ani pole, která ostatní pod-skripty určily. Co z dat nelze určit, má proto pod-skript
  vracet jako `null` s důvodem v poznámkách, ne jako výjimku.

### Limity

| nastavení | výchozí | povoleno | |
|---|---|---|---|
| `cas` | 5 | 1–600 | sekundy celého běhu skriptu (načtení hlavního skriptu a pod-skriptů i volání `hodnot`) |
| `pamet` | 64 | 16–4096 | MB, o které může skript zvětšit paměť programu |
| `rekurze` | 256 | 16–10000 | největší hloubka vnoření volání funkcí |

Změnit je lze v souboru `spoustec.ini` ve složce spouštěče. Bez souboru platí výchozí hodnoty. Neplatná hodnota se
zapíše do logu a použije se výchozí.

```ini
[limity]
cas=5
pamet=64
rekurze=256
```

Seznam v JavaScriptu může mít nejvýše 10 milionů prvků. Výsledek funkce `hodnot` nejvýše 100 000 hodnot a odpověď
nejvýše 1 MB. Nic z toho běžný skript nepotřebuje.

## Otisk skriptů

Aby bylo dohledatelné, kterou verzí skriptů hodnocení vzniklo, spouštěč u každého běhu spočítá **otisk**: SHA-256
seznamu všech načtených souborů (hlavní skript a pod-skripty), kde každý soubor má řádek „SHA-256 souboru, dvě
mezery, cesta ve složce skriptů“ a řádky jsou seřazené podle cesty. Je to stejný tvar, jaký vypisuje `sha256sum`,
otisk lze tedy ověřit i bez spouštěče (ve složce skriptů, Git Bash):

```
sha256sum audio.js cteni-dat.js ztrata-fowler.js | sed 's/ \*/  /' | sha256sum
```

- V odpovědi pro Fowler je v `app` prvních 8 znaků: `spoustec 1.0.0; audio.js #1a2b3c4d`.
- V logu, výpisu `--run` a protokolu `--test` je celý otisk a SHA-256 každého načteného souboru.
- Změna kteréhokoli načteného souboru otisk změní. Soubory ve složce, které hlavní skript nenačte, otisk neovlivní.

## Ladění (`--run`)

```powershell
.\spoustec.exe --run C:\Fowler\skripty priklady\audio.json odpoved.json | Out-Host
```

Spustí hlavní skript předané části stejně jako z Fowleru a vypíše načtené skripty s SHA-256 a otiskem, výstup
`console.log`, poznámku a odpověď pro Fowler. Výpis se uloží
také do `spoustec-run.txt` vedle logu. Identifikace pacienta (`pid`, `rc`) je ve výpisu skrytá. V souboru odpovědi
(třetí parametr, nepovinný) je tak, jak ji dostane Fowler. Pro ladění používejte smyšlená data (např. z `priklady`).
Skutečná data pacientů Fowler po skončení programu maže a do souborů je nekopírujte.

V příkazovém řádku (cmd) spouštějte přes `start /wait spoustec.exe --run ...`, jinak se výstup smíchá s promptem.

## Testovací režim

Testovací režim slouží k ověření skriptů na případech, u kterých pracoviště zná správné hodnocení, a k uložení
výsledku do validační dokumentace pracoviště. Testuje se celá složka skriptů: každý případ spustí hlavní skript
své části i s pod-skripty.

```
testy\pripad01.json              vstup fowler-exam (smyšlená nebo anonymizovaná data)
testy\pripad01.ocekavano.json    očekávaný výsledek ve tvaru, který vrací hodnot()
testy\pripad02.json
testy\pripad02.ocekavano.json
```

```powershell
.\spoustec.exe --test C:\Fowler\skripty C:\Fowler\skripty\testy | Out-Host
```

- **Očekávaný výsledek určuje lékař** (např. hodnocení vyšetření, které provedl ručně), ne skript. Jinak by test
  ověřoval skript sám proti sobě.
- Očekávání má stejný tvar jako výsledek funkce `hodnot`, např. `{ "audio": { "acAvg": { "R": 25, "L": 45.5 } } }`.
  Klíč `note` se ignoruje a lze do něj napsat komentář k případu.
- Každý případ se zpracuje stejně jako při běhu z Fowleru (včetně formální kontroly) a porovnají se jednotlivá
  pole. Čísla podle hodnoty v JSON (`25` = `25.0`), bez tolerance, protože spouštěč nezaokrouhluje. Texty přesně.
  Chybějící pole a `null` znamenají „neurčeno“. Seznamy reflexů a decay se porovnávají podle podnětu, na pořadí
  nezáleží. Poznámky se neporovnávají, jen vypíšou.
- Výsledek případu: **OK** (všechna pole se shodují), **ROZDÍL** (vypíše pole, očekávanou hodnotu a výsledek),
  **CHYBA** (skript selhal nebo pro část chybí hlavní skript, vstup nebo očekávání nelze přečíst; zpráva, soubor
  a řádek).
- **Protokol** se uloží do složky s testy jako `protokol-<složka skriptů>-<datum>_<čas>.txt`. Obsahuje datum a čas,
  verzi spouštěče a Jintu, cestu ke složce skriptů, u každého případu otisky vstupu a očekávání, hlavní skript
  s otiskem a načtené soubory, výsledek, dále část **Použité skripty** (každý hlavní skript s plným otiskem
  a SHA-256 všech souborů, které načetl) a souhrn.
- Návratový kód: 0 = všechny případy OK, 5 = některý případ ROZDÍL nebo CHYBA, 2 = složka skriptů nebo složka
  s testy neexistuje nebo neobsahuje žádný případ.

Ukázkové testy ke složce [priklady/ukazka](../priklady/ukazka) jsou v [priklady/testy](../priklady/testy):
`spoustec.exe --test priklady\ukazka priklady\testy`.

Podle otisku v odpovědi (`app`) lze ověřit, že ve Fowleru běží stejná verze skriptů, jaká byla otestována
(protokol, část Použité skripty). Po každé změně kteréhokoli skriptu testy spusťte znovu.

## Úskalí JavaScriptu

JavaScript některé chyby neohlásí, ale tiše spočítá jiný výsledek. Spouštěč zachytí jen část z nich (`NaN`,
`Infinity`, `undefined` a příliš mnoho desetinných míst ve výsledku). Ostatní musí ošetřit skript.

| výraz | výsledek | proč |
|---|---|---|
| `(20 + null + 30) / 3` | `16.666666666666668` | `null` (neměřeno) se v aritmetice tiše mění na 0 |
| `Math.max(null, -5)` | `0` | totéž |
| `"NR" + 5` | `"NR5"` | `+` s textem spojuje texty |
| `"NR" * 1`, `undefined + 1` | `NaN` | text nebo chybějící údaj v aritmetice |
| `"NR" > 50`, `null >= 0` | `false`, `true` | porovnání s textem a `null` |
| `0.1 + 0.2` | `0.30000000000000004` | desetinná čísla nejsou v počítači přesná |
| `Math.round(-2.5)` | `-2` | `Math.round` zaokrouhluje polovinu nahoru (k +∞), ne od nuly |
| `(1.005).toFixed(2)` | `"1.00"` | 1.005 je ve skutečnosti 1.00499999…; výsledkem je navíc text |
| `Math.round(1.005 * 100) / 100` | `1` | totéž |
| `[10, 9, 100].sort()` | `[10, 100, 9]` | `sort()` bez funkce řadí jako texty |

Doporučení:

- **Chybějící hodnoty (`null`, chybějící klíč) a `"NR"` vždy ošetřete výslovně**, podle metodiky pracoviště:
  rozhodněte, zda se má údaj vynechat, výsledek neurčit, nebo jinak. Spoléhat na automatické převody nelze.
- **Zaokrouhlete ve skriptu** na tvar polí (počet desetinných míst v tabulce výše), způsobem podle metodiky
  pracoviště. Spouštěč nezaokrouhluje. Hodnotu s víc desetinnými místy vynechá.
- Číslo vracejte jako číslo, ne jako text (`toFixed` vrací text).
- Hodnotu hledejte podle frekvence nebo podnětu, ne podle pozice v seznamu.
