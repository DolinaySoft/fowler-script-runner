# Formát předání vyšetření a odpovědi externího programu

> **Kopie.** Tento soubor je kopie `docs/format.md` z repozitáře
> [DolinaySoft/fowler-external-program](https://github.com/DolinaySoft/fowler-external-program/blob/0b6d5f9/docs/format.md)
> (commit 0b6d5f9, verze formátu 1). Při změně formátu platí originál, kopie se aktualizuje s novou verzí spouštěče.
> Spouštěč předává skriptu data podle části „Předávaná data“ bez bloku `patient` a výsledek skriptu kontroluje
> podle části „Odpověď programu“ (viz [skripty.md](skripty.md)).

Verze formátu 1 (Fowler od verze s úpravami pro MDR, 2026).

Fowler předá uložené vyšetření externímu programu pracoviště v souboru JSON (`fowler-exam`). Program zapíše
odpověď (`fowler-eval`) a Fowler ji vloží do polí okna, kde ji uživatel zkontroluje, případně opraví a potvrdí.

- **Fowler při předání nic nehodnotí ani nepřepočítává.** Předá data tak, jak jsou uložená, převádí jen jednotky
  a zápis.
- **Části:** tónová audiometrie (`audio`), slovní audiometrie (`speech`), tympanometrie (`tymp`). Formát umí předat
  libovolnou kombinaci, Fowler ale předává vždy jen jednu část podle okna, ze kterého uživatel program spustil:
  z okna Hodnotit audiogramu `audio`, z okna Hodnotit tympanometrie `tymp`, ze slovní audiometrie `speech`.
- **Nepředává se:** ladičkové zkoušky, předchozí vyšetření pacienta a hodnocení zadané uživatelem (viz Obecná pravidla).

Obsah: [Spuštění programu](#spuštění-programu) · [Předávaná data](#předávaná-data-fowler-exam) ·
[Odpověď programu](#odpověď-programu-fowler-eval) · [Známá omezení](#známá-omezení)

## Spuštění programu

1. **Nastavení ve Fowleru:** Nastavení programu → tlačítko Extra... → skupina „Externí program pro hodnocení měření“.
   Ukládá se do INI Fowleru (`fowler.ini`):

   ```ini
   [HODNOCENI]
   program=evaluator\evaluator.exe
   parametry=
   vstup=hodnoceni_vstup.json
   vystup=hodnoceni_vystup.json
   automaticky=0
   ```

   - Relativní cesty (program, vstup, výstup) jsou vůči složce programu Fowler.
   - `program`: cesta k programu. Prázdné = není nastaven, tlačítko Externí program... jen ohlásí, že program
     není nastaven.
   - `parametry`: `%VSTUP%` se nahradí cestou ke vstupnímu souboru, `%VYSTUP%` cestou k výstupnímu souboru
     (na velikosti písmen nezáleží; cesty mohou obsahovat mezery, proto je dejte do uvozovek).
     Prázdné = `"%VSTUP%" "%VYSTUP%"`.
   - `vstup`, `vystup`: soubory pro předání. Prázdné = `hodnoceni_vstup.json` a `hodnoceni_vystup.json`.
   - `automaticky=1`: po stisku Hodnotit (audiogram, tympanometrie) se otevře okno hodnocení a program se hned spustí.
     Jinak se spouští tlačítkem Externí program... v okně Hodnotit. Slovní audiometrie okno Hodnotit nemá, program se
     spouští vždy tlačítkem Externí program... pod poli SRT.
2. **Soubory:** před spuštěním Fowler smaže starou odpověď (když to nejde, program nespustí) a zapíše vstupní soubor.
   Po skončení programu se oba soubory smažou, protože obsahují osobní údaje.
3. **Běh:** program se spustí ve své složce (u skriptu je to složka interpretu, viz README verze pro Python).
   Fowler čeká na jeho konec bez časového limitu, okno hodnocení je mezitím neaktivní. Program, který visí,
   je nutné ukončit (např. ve Správci úloh).
4. **Výsledek:** odpověď platí, když program skončí s kódem 0 a výstupní soubor existuje. Jiný kód nebo chybějící
   soubor Fowler ohlásí a nic nenačte.

## Předávaná data (`fowler-exam`)

### Příklad

Příklad obsahuje všechny tři části, aby byla vidět celá struktura (Fowler předává jen jednu). Pacient je smyšlený,
křivky tympanometrie jsou zkrácené – skutečná křivka má desítky až stovky bodů. Soubory s příklady jsou ve složce
[`priklady`](../priklady).

```json
{
  "fmt": "fowler-exam",
  "v": 1,
  "app": "Fowler 3.0.0.123",
  "created": "2026-10-29T10:15:31",
  "patient": {
    "pid": "4F0C1E2A-7B3D-4C55-9E21-0A6B3D9F1C77",
    "rc": "5704150000",
    "firstName": "Jan",
    "lastName": "Novák",
    "birthDate": "1957-04-15",
    "insurance": "111"
  },
  "exam": {
    "time": "2026-10-28T09:42:00",
    "age": { "y": 69, "m": 6 },
    "sex": "M"
  },
  "audio": {
    "legacy": false,
    "device": "MA 42",
    "note": "Pacient udává tinnitus vpravo.\nSpolupráce dobrá.",
    "hz": [125, 250, 500, 1000, 1500, 2000, 3000, 4000, 6000, 8000],
    "R": {
      "ac":     [15, 15, 20, 25, 30, 40, 55, 60, 70, "NR"],
      "bc":     [null, 10, 15, 20, 25, 35, 50, 55, null, null],
      "bcMask": [null, 40, 45, 50, 50, 60, 70, 70, null, null],
      "ucl":    [null, null, 100, 100, null, 95, null, 95, null, null],
      "mcl":    [null, null, 70, 70, null, 75, null, 80, null, null],
      "sisi":   [null, null, null, 10, null, 20, 60, 85, null, null]
    },
    "L": {
      "ac":       [30, 35, 45, 50, 55, 60, 65, 70, 75, 80],
      "acMasked": true,
      "acMask":   [null, null, 60, 65, null, 70, null, 75, null, null],
      "bc":       [null, 10, 15, 15, 20, 30, 45, 50, null, null]
    }
  },
  "speech": {
    "legacy": false,
    "note": "Sluchadlo vpravo.",
    "R": { "db": [20, 30, 40, 50, 60], "pct": [0, 20, 60, 90, 100] },
    "L": { "db": [30, 40, 50, 60, 70], "pct": [0, 10, 50, 80, 100], "mask": [null, null, 40, 45, "M"] },
    "aided": {
      "R": { "db": [20, 30, 40], "pct": [30, 80, 100] }
    }
  },
  "tymp": {
    "legacy": false,
    "device": "AT235",
    "R": {
      "tg": [
        {
          "probeHz": 226, "peakP": -15, "peakY": 0.65, "ecv": 1.12, "gradP": 85,
          "p": [200, 100, 0, -15, -100, -200, -300, -400],
          "y": [1.14, 1.22, 1.7, 1.77, 1.33, 1.18, 1.15, 1.14]
        }
      ],
      "ipsi": [
        {
          "stim": 1000, "p": -15, "res": "+", "src": "dev",
          "lv": [
            { "db": 85, "t": [0, 100, 200, 300], "y": [0, 0.002, 0.003, 0.001], "found": false },
            { "db": 95, "t": [0, 100, 200, 300], "y": [0, 0.021, 0.034, 0.03], "found": true }
          ]
        },
        {
          "stim": "WB", "p": -15, "res": "+", "src": "user",
          "lv": [ { "db": 75, "t": [0, 100, 200, 300], "y": [0, 0.015, 0.022, 0.02] } ]
        }
      ],
      "decayIpsi": [
        { "stim": 500, "p": -15, "db": 95, "pct": -20, "t": [0, 2000, 4000, 6000, 8000, 10000], "y": [0, 0.04, 0.038, 0.036, 0.034, 0.032] }
      ]
    }
  }
}
```

Příklad je kvůli čitelnosti odsazený, Fowler může psát vše na jeden řádek.

### Obecná pravidla

1. **Soubor:** UTF-8 bez BOM, JSON podle RFC 8259. Jeden soubor obsahuje jedno vyšetření jednoho pacienta.
2. **Části:** soubor obsahuje aspoň jednu z částí `audio`, `speech`, `tymp`. Chybějící část znamená „nepředáno“,
   ne „neměřeno“.
3. **Co není známo, ve výstupu vůbec není.** U klíčů se nepoužívá `null` ani `""`. `null` se smí objevit jen uvnitř
   pole hodnot, kde to pravidla dané části výslovně dovolují.
4. **Souběžná pole:** pole, která k sobě patří (`hz` a křivky audiogramu, `db`/`pct`/`mask`, `p`/`y`, `t`/`y`),
   mají vždy stejnou délku; i-tá hodnota jednoho patří k i-té hodnotě druhého.
5. **Vynechávání:** křivka, ucho nebo seznam bez jediné hodnoty se vynechá celý.
6. **Časy** jsou ve tvaru `YYYY-MM-DDThh:mm:ss` v místním čase pracoviště bez časové zóny.
7. **Čísla:** dB, daPa, Hz, ms a % jsou celá čísla. Hodnoty v ml (mmho) jsou desetinná čísla s nejvýše
   3 desetinnými místy.
8. **Texty:** konce řádků jsou `\n`, ostatní znaky podle JSON.
9. **Kompatibilita:** čtenář ignoruje klíče, které nezná. Přidání nového klíče verzi nemění, nekompatibilní změna
   zvýší `v`. Čtenář odmítne soubor s jiným `fmt` nebo s vyšším `v`, než zná.
10. **Bez výpočtů:** Fowler nic nedoplňuje, neprůměruje ani nevyhodnocuje. Jedinou odvozenou hodnotou je věk
    (administrativní údaj).
11. **Hodnocení zadané uživatelem se nepředává**, aby vstupem byla jen naměřená data: ztráta dle Fowlera, průměry,
    WHO, skupina podle věku, výsledek SISI, SRT, typ tympanogramu ani decay přepsaný uživatelem. Předává se jen to,
    co změřil nebo vyhodnotil přístroj (např. decay v %). Výjimkou je výsledek reflexu (vybavný +/−), který se
    předává vždy spolu se zdrojem: z přístroje, nebo zadaný či přepsaný uživatelem (`res`, `src`).

### Hlavička

| klíč | povinný | význam |
|---|---|---|
| `fmt` | ano | vždy `"fowler-exam"` |
| `v` | ano | verze formátu, nyní `1` |
| `app` | ano | program a verze, která soubor vytvořila |
| `created` | ano | čas vytvoření souboru |
| `patient` | ne | identifikace pacienta |
| `exam` | ano | společné údaje vyšetření |
| `audio`, `speech`, `tymp` | aspoň jedna | části vyšetření |

#### `patient` (všechny položky nepovinné)

| klíč | význam |
|---|---|
| `pid` | identifikátor pacienta ve Fowleru (GUID); chybí u pacienta, který ještě není v databázi |
| `rc` | rodné číslo bez lomítka a mezer, u cizince jeho identifikátor |
| `firstName`, `lastName` | jméno, příjmení |
| `birthDate` | datum narození `YYYY-MM-DD` |
| `insurance` | pojišťovna |

#### `exam`

| klíč | povinný | význam |
|---|---|---|
| `time` | ano | čas vyšetření |
| `age` | ne | věk v den vyšetření: dokončené roky (`y`) a měsíce (`m`) |
| `sex` | ne | `"M"` nebo `"F"` (z rodného čísla, u cizinců z údaje u pacienta) |

`age` a `sex` se vynechají, když nejsou známé. Fowler předává vždy `pid` (je-li pacient v databázi), `rc`
a `exam.time`; odpověď je musí zopakovat (viz Odpověď programu).

#### Společné klíče částí

| klíč | povinný | význam |
|---|---|---|
| `legacy` | ano | `true` = část uložená verzí Fowleru před úpravami pro MDR, viz upozornění u tónové audiometrie |
| `device` | ne | název přístroje z nastavení Fowleru |
| `note` | ne | popis vyšetření zadaný uživatelem |

### Tónová audiometrie (`audio`)

| klíč | povinný | význam |
|---|---|---|
| `hz` | ano | seznam frekvencí, viz pravidla níže |
| `R`, `L` | ne | data pravého a levého ucha, pole podle `hz` |

**Pravidla pro `hz`:**
- Rostoucí seznam různých kladných celých čísel (frekvence v Hz).
- Obsahuje jen frekvence, na kterých je v části `audio` aspoň jedna hodnota.
- Nepředpokládejte pevnou sadu ani počet frekvencí. Program musí zpracovat i frekvence, které Fowler dnes
  neukládá (např. 12 500 nebo 16 000 Hz); frekvenci, kterou nepoužívá, vynechá, ale soubor neodmítne.
- Fowler dnes ukládá: vzduch, UCL a MCL na 125, 250, 500, 750, 1000, 1500, 2000, 3000, 4000, 6000, 8000
  a 10 000 Hz; kost na 250, 500, 750, 1000, 1500, 2000, 3000, 4000 a 6000 Hz; SISI na 500, 1000, 2000, 3000
  a 4000 Hz.

**Hodnoty v polích:**
- `null` = na této frekvenci neměřeno.
- Prahy `ac`, `bc`, `ucl`, `mcl` a maskování `acMask`, `bcMask` jsou celá čísla v dB, prahy mohou být i záporné.
- `"NR"` znamená neslyší (ve Fowleru „-“). Smí být jen v `ac` a `bc`.
- `sisi` je celé číslo 0–100 v %.

| klíč | význam |
|---|---|
| `ac` | vzdušné vedení [dB HL] |
| `acMasked` | `true` = křivka vzduchu označená jako maskovaná, jinak se neuvádí |
| `acMask` | úroveň maskovacího šumu při měření vzduchu [dB] |
| `bc` | kostní vedení [dB HL] |
| `bcMask` | úroveň maskovacího šumu při měření kosti [dB] |
| `ucl` | práh nepohodlí [dB] |
| `mcl` | nejpříjemnější hlasitost [dB] |
| `sisi` | SISI [%] |

**Upozornění k `legacy`:** starší verze Fowleru nabízela doplnění kostního vedení odhadem. Odhadnuté hodnoty se
uložily stejně jako naměřené a nelze je od nich odlišit. U `audio` s `"legacy": true` proto `bc` může obsahovat
hodnoty, které nebyly naměřené; program je má brát s výhradou. U `speech` a `tymp` `legacy` předávaná data
neovlivňuje.

### Slovní audiometrie (`speech`)

| klíč | význam |
|---|---|
| `R`, `L` | křivka pravého a levého ucha |
| `bin` | binaurálně ve volném poli |
| `aided` | totéž se sluchadlem, obsahuje `R`, `L`, `bin` |

Každá křivka obsahuje souběžná pole bodů seřazená podle intenzity:

| klíč | povinný | význam |
|---|---|---|
| `db` | ano | intenzita podnětu [dB] podle stupnice slovní audiometrie |
| `pct` | ano | srozumitelnost [%], 0–100 |
| `mask` | ne | maskování bodu: `null` = nemaskováno, číslo = úroveň maskování [dB], `"M"` = maskováno, úroveň neuvedena; pole se uvádí, jen když je maskovaný aspoň jeden bod |

### Tympanometrie (`tymp`)

Obsah ucha `R`, `L`:

| klíč | význam |
|---|---|
| `tg` | seznam tympanogramů (226 Hz, případně 1000 Hz) |
| `ipsi`, `contra` | stapediové reflexy, seznam podnětů |
| `decayIpsi`, `decayContra` | decay reflexu, seznam podnětů |
| `etf` | funkce Eustachovy trubice, neperforovaný bubínek |
| `etfPerf` | funkce Eustachovy trubice, perforovaný bubínek |

**Zdroj dat:** stejná data, ze kterých Fowler kreslí graf tympanometrie, tedy graf, který vidí lékař.
Data různých přístrojů jsou převedená do jedné podoby. Výsledky reflexu (`res`) jsou z panelu tympanometrie.

**Jednotky:** tlak v daPa. Hodnoty `y`, `peakY` a `ecv` jsou u sondy 226 Hz v ml, u sondy 1000 Hz v mmho.

#### Tympanogram (položka `tg`)

| klíč | povinný | význam |
|---|---|---|
| `probeHz` | ano | kmitočet sondy, 226 nebo 1000 |
| `peakP` | ne | tlak vrcholu [daPa] |
| `peakY` | ne | výška vrcholu (poddajnost) |
| `ecv` | ne | objem zvukovodu |
| `gradP` | ne | šířka tympanogramu (gradient) [daPa] |
| `gradY` | ne | gradient vyjádřený poddajností, když ho přístroj udává v ml |
| `p`, `y` | ne | křivka: tlak a poddajnost v pořadí měření |

Hodnoty vrcholu i křivka jsou z dat přístroje. `y` je poddajnost tak, jak ji poslal přístroj, tedy včetně objemu
zvukovodu; graf Fowleru ukazuje `y − ecv` (vrchol pak odpovídá `peakY`). Gradient se uvádí jen u 226 Hz: některé
starší tympanometry ho udávají v ml (`gradY`), ostatní v daPa (`gradP`). Pokud Fowler u vyšetření data přístroje
nemá a hodnoty uživatel zapsal ručně (pole Compl., Tlak, Obj. v panelu tympanometrie), předají se `peakY`, `peakP`
a `ecv` bez křivky a bez `probeHz` a považují se za 226 Hz.

#### Reflex (položka `ipsi`, `contra`)

| klíč | povinný | význam |
|---|---|---|
| `stim` | ano | podnět: frekvence v Hz, nebo šum `"LP"`, `"HP"`, `"WB"` |
| `p` | ne | tlak v uchu během měření [daPa] |
| `res` | ne | výsledek: `"+"` = reflex vybavný (nalezen), `"-"` = nevybavný |
| `src` | s `res` | zdroj výsledku: `"dev"` = přístroj, `"user"` = zadal nebo přepsal uživatel |
| `lv` | ano | seznam měřených intenzit podnětu |

Položka `lv`:

| klíč | povinný | význam |
|---|---|---|
| `db` | ano | intenzita podnětu [dB] |
| `t`, `y` | ano | časový průběh: poloha v záznamu dané intenzity 0–1000 a změna poddajnosti [ml]; kladná hodnota = pokles poddajnosti |
| `found` | ne | výsledek detekce z přístroje pro tuto intenzitu (`true`/`false`); jen když ho přístroj posílá (Interacoustics, Maestro) |

`t` není čas v ms. Celý záznam jedné intenzity je vždy rozsah 0–1000: u přístrojů Interacoustics a Maestro je
skutečná délka záznamu přepočítaná na tento rozsah, u MAICO jsou to ms tak, jak je posílá přístroj (záznam trvá
asi 1 s). Každá intenzita začíná od 0. `y` je oříznuté na −0,1 až 0,2 ml (Maestro −0,2 až 0,2 ml), stejně jako
v grafu.

Výsledek `res`:
- podněty 500, 1000, 2000 a 4000 Hz: hodnota z panelu tympanometrie (zadal ji uživatel, nebo ji Fowler po měření
  vyplnil z přístroje); `src` je `"dev"`, když se shoduje s výsledkem z přístroje, jinak `"user"`; prázdný panel =
  bez `res`;
- šum IPSI (`LP`, `HP`, `WB`): reflex na šum zadaný v okně Hodnotit, jinak výsledek z přístroje;
- ostatní (např. 3000 Hz, šum CONTRA): jen výsledek z přístroje.

#### Decay (položka `decayIpsi`, `decayContra`)

| klíč | povinný | význam |
|---|---|---|
| `stim` | ano | frekvence podnětu [Hz] |
| `p` | ne | tlak v uchu [daPa] |
| `db` | ne | intenzita podnětu [dB] |
| `pct` | ne | decay v % z přístroje, se znaménkem, jak ho přístroj posílá; chybí, když přístroj hodnotu nedal |
| `t`, `y` | ne | časový průběh jako u reflexu, ale celý záznam je rozsah 0–10 000 (při obvyklé délce 10 s odpovídá ms) |

Decay se měří na jedné intenzitě. Kdyby přístroj poslal pro jeden podnět víc intenzit, předá se každá jako
samostatná položka bez `pct`.

#### Eustachova trubice (`etf`, `etfPerf`)

- `etf` je seznam tympanogramů v pořadí z přístroje. Každý obsahuje `p`, `y`, případně `ecv` a `press` = tlak,
  se kterým se test v daném kroku prováděl [daPa]. `y` je včetně objemu zvukovodu jako u tympanogramu.
- `etfPerf` obsahuje souběžná pole `t` [ms] a `p` [daPa]: průběh tlaku v čase.

## Odpověď programu (`fowler-eval`)

Program zapíše odpověď do výstupního souboru (`%VYSTUP%`) a skončí s kódem 0. Odpověď obsahuje jen hodnocení
a identifikaci vyšetření, předaná data neopakuje.

### Příklad

Odpověď na předání z okna Hodnotit audiogramu (části pro tympanometrii a slovní audiometrii níže). Hodnoty jsou
jen ukázkou zápisu.

```json
{
  "fmt": "fowler-eval",
  "v": 1,
  "app": "Hodnotitel 2.1",
  "created": "2026-10-29T10:15:40",
  "patient": { "pid": "4F0C1E2A-7B3D-4C55-9E21-0A6B3D9F1C77", "rc": "5704150000" },
  "exam": { "time": "2026-10-28T09:42:00" },
  "note": "Doporučeno kontrolní vyšetření za 6 měsíců.",
  "audio": {
    "fowler": { "R": 12.5, "L": 30, "total": 20.1 },
    "acAvg": { "R": 25, "L": 45.5, "total": 35 },
    "bcAvg": { "R": 20, "L": 40, "total": 30 },
    "who": "lehká",
    "group": "Zvýšený dozor",
    "sisi": { "R": "pozitivní", "L": "negativní" }
  }
}
```

```json
  "tymp": {
    "R": {
      "type": "A",
      "ipsi": [ { "stim": 500, "res": "+" }, { "stim": 1000, "res": "+" }, { "stim": "WB", "res": "-" } ],
      "contra": [ { "stim": 1000, "res": "-" } ],
      "decayIpsi": [ { "stim": 500, "pct": -20 }, { "stim": 1000, "pct": "-" } ]
    },
    "L": { "type": "B" }
  }
```

```json
  "speech": {
    "srt": { "R": 35.5, "L": 40, "bin": 30 },
    "aided": { "srt": { "R": 20 } }
  }
```

### Soubor a hlavička

1. **Soubor:** UTF-8 (BOM se toleruje), JSON podle RFC 8259, nejvýše 1 MB. Čte se přísně: chyba syntaxe,
   neplatné UTF-8 nebo klíč opakovaný v jednom objektu znamená, že se nic nevloží.
2. **Hlavička:**

   | klíč | povinný | význam |
   |---|---|---|
   | `fmt` | ano | vždy `"fowler-eval"` |
   | `v` | ano | verze formátu, celé číslo; vyšší, než Fowler zná (nyní `1`), Fowler odmítne |
   | `app` | ano | název a verze programu, zobrazí se uživateli |
   | `created` | ne | čas vytvoření odpovědi |
   | `patient` | ano | `pid` a `rc` přesně tak, jak byly předány (které byly předány, ty musí být i v odpovědi) |
   | `exam` | ano | `time` přesně jako v předaných datech |
   | `note` | ne | zpráva programu pro uživatele, zobrazí se po zpracování odpovědi (prvních 2000 znaků) |
   | `audio`, `tymp`, `speech` | ne | hodnocení části, která byla předána; ostatní části se ignorují |

3. **Příslušnost k vyšetření:** odpověď se vloží, jen když souhlasí `patient.pid` (na velikosti písmen nezáleží),
   `patient.rc` (bez lomítka a mezer) a `exam.time` s předanými daty. Jinak se nic nevloží a Fowler ohlásí,
   co nesouhlasí.
4. **Obecná pravidla pro údaje:**
   - Údaj, který v odpovědi není nebo je `null`, program nehodnotí a pole v okně zůstane, jak je.
   - Neznámé klíče se ignorují.
   - Nejdřív se zkontroluje celá odpověď. Když je některý údaj neplatný (typ, rozsah, formát), nevloží se nic
     a hlášení vypíše všechny chyby.
   - Údaj, pro který okno nemá pole (např. reflex 3000 Hz, SISI na straně bez naměřených bodů, SRT křivky bez bodů),
     se nevloží a hlášení ho vypíše; ostatní údaje se vloží.
   - Odpověď bez hodnocení předané části je platná, Fowler ohlásí, že program nevrátil žádné hodnocení
     (s případnou zprávou `note`). Program tak může sdělit, že vyšetření nehodnotí.
   - Texty: bez řídicích znaků (konců řádků), oříznou se mezery na začátku a konci, prázdný text je chyba.

### Tónová audiometrie (`audio`) – okno Hodnotit audiogramu

| klíč | pole v okně | hodnota |
|---|---|---|
| `fowler.R`, `.L`, `.total` | ztráta sluchu dle Fowlera vpravo, vlevo, celkem | číslo 0–100 [%] |
| `acAvg.R`, `.L`, `.total` | průměr vzdušného vedení vpravo, vlevo, celkem | číslo −10 až 130 [dB] |
| `bcAvg.R`, `.L`, `.total` | průměr kostního vedení | číslo −10 až 130 [dB] |
| `who` | ztráta sluchu dle WHO | text, nejvýše 100 znaků |
| `group` | skupina ztráty sluchu dle věku | text, nejvýše 100 znaků |
| `sisi.R`, `.L` | výsledek SISI testu | text, nejvýše 100 znaků; jen na straně s naměřenými body SISI |

- Čísla mají nejvýše 2 desetinná místa a zapsaná bez zbytečných nul nejvýše 5 znaků (pole v okně), např. `12.25`,
  `-10.5`, `100`.
- Texty se vloží, jak jsou. Seznamy v okně jsou jen nabídka:
  - `who`: normální sluch, lehká, středně těžká, středně těžká až těžká, těžká, velmi těžká, úplná (hluchota),
    jednostranná (v dekurzu a tisku se k nim doplní „dle WHO 2021“);
  - `group`: Fyziol. presbyakuze, Bez problémů, Zvýšený dozor, Přeřazení, Nemoc z povolání, Nezařazeno;
  - `sisi`: negativní, neurčitý, pozitivní.

### Tympanometrie (`tymp`) – okno Hodnotit tympanometrie

Obsah ucha `R`, `L`:

| klíč | pole v okně | hodnota |
|---|---|---|
| `type` | typ křivky | text nejvýše 10 znaků; nabídka okna `A`, `B`, `C`, `C1`, `C2`, `-` (nelze změřit), jiný text se přidá do seznamu |
| `ipsi`, `contra` | reflexy IPSI, CONTRA | seznam `{ "stim": ..., "res": "+" nebo "-" }` |
| `decayIpsi`, `decayContra` | decay | seznam `{ "stim": ..., "pct": celé číslo [%] nebo "-" (neměřitelné) }` |

- `stim` je jako v předaných datech: frekvence v Hz, nebo `"LP"`, `"HP"`, `"WB"`.
- Okno má reflexy a decay pro 500, 1000, 2000 a 4000 Hz a reflex na šum IPSI jen pro podnět, který je
  v naměřených datech. Jiné podněty se vypíšou jako nevložené.
- Položka bez `res` (bez `pct`) se přeskočí. Stejný podnět dvakrát v jednom seznamu je chyba.

### Slovní audiometrie (`speech`) – pole SRT

| klíč | pole | hodnota |
|---|---|---|
| `srt.R`, `.L`, `.bin` | SRT vpravo, vlevo, binaurálně | číslo −10 až 120 [dB], nejvýše 1 desetinné místo |
| `aided.srt.R`, `.L`, `.bin` | totéž se sluchadlem | |

SRT se vloží jen ke křivce, která má body. Slovní audiometrie spouští program jen v režimu editace.

### Kontrola uživatelem a uložení

- Okno se samo nezavře. Hodnoty jsou v polích stejně jako zadané uživatelem a uloží se až potvrzením (OK v okně
  Hodnotit, ve slovní audiometrii uložením vyšetření).
- Hodnocení, které uživatel po vložení nezměnil a potvrdil, se uloží s označením, že bylo převzato z externího
  programu, se jménem uživatele, který ho potvrdil, a časem potvrzení. Okno Hodnotit pak ukazuje „převzato
  z externího programu, potvrdil(a) …“. Když uživatel po vložení cokoli změnil, hodnocení se uloží jako zadané ručně.

## Známá omezení

1. **Kontralaterální reflex:** formát zatím neurčuje, zda ucho `R`/`L` u `contra` znamená ucho podnětu, nebo ucho
   sondy. Řiďte se tím, jak stejná data zobrazuje Fowler v grafu tympanometrie.
2. **`bin` ve slovní audiometrii** je měření binaurálně ve volném poli.
3. **ETF:** pořadí a význam jednotlivých kroků se mohou lišit podle přístroje.
4. **Identifikace pacienta:** blok `patient` se předává vždy, protože podle něj Fowler kontroluje, že odpověď patří
   k vyšetření. Program s osobními údaji zachází podle pravidel pracoviště (Fowler vstupní soubor po skončení
   programu maže).
