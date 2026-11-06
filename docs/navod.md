# Jak napsat skripty pracoviště

Návod pro lékaře a vývojáře pracoviště i pro AI asistenta, který jim se skripty pomáhá. Rozhraní a technické
podrobnosti jsou v [skripty.md](skripty.md), data a pole odpovědi v [format.md](format.md).

Obsah: [Kdo co dělá](#kdo-co-dělá) · [Jak jsou skripty uspořádané](#jak-jsou-skripty-uspořádané) ·
[Postup](#postup) · [Dotazník metodiky](#dotazník-metodiky) · [Práce s daty](#práce-s-daty) ·
[Kontrolní seznam](#kontrolní-seznam-před-nasazením)

## Kdo co dělá

| kdo | dělá | nedělá |
|---|---|---|
| **lékař / pracoviště** | určí metodiku (co a jak se hodnotí), určí očekávané výsledky testovacích případů, skripty schválí a odpovídá za ně | – |
| **AI asistent** | převede metodiku do pod-skriptů a zapojí je v hlavním skriptu, ptá se na vše, co v metodice chybí, spustí ukázky a testy, ukáže výsledky | nevymýšlí frekvence, hranice, tabulky, vzorce ani zaokrouhlení; nepočítá očekávané výsledky testů |
| **spouštěč** | spustí hlavní skript předané části nad daty bez údajů o pacientovi, formálně zkontroluje výsledek | nic nepočítá, nemění ani nezaokrouhluje hodnoty |
| **Fowler** | vloží výsledek do okna Hodnotit jako návrh | nic nepočítá; výsledek kontroluje a potvrzuje uživatel |

Ukázky v [priklady](../priklady) záměrně **neobsahují žádný postup hodnocení**: spouštěč ani jeho dokumentace
nesmí lékaři nabízet hotové hodnocení (viz [README](../README.md#odpovědnost)). Ukazují jen, jak skripty sestavit,
jak číst data a jak v pod-skriptu počítat: [priklady/ukazka/ukazka-vypoctu.js](../priklady/ukazka/ukazka-vypoctu.js)
počítá veličinu bez audiologického významu (průměrnou frekvenci naměřených prahů) a výsledek jde jen do poznámky.

## Jak jsou skripty uspořádané

```
C:\Fowler\skripty\            složka skriptů pracoviště (ve Fowleru 3. parametr)
  audio.js                    hlavní skript: tónová audiometrie
  tymp.js                     hlavní skript: tympanometrie
  speech.js                   hlavní skript: slovní audiometrie
  ztrata-fowler.js            pod-skript: ztráta dle Fowlera  (pole audio.fowler)
  who.js                      pod-skript: ztráta dle WHO      (pole audio.who)
  cteni-dat.js                společné funkce pro čtení dat (ze šablony, neměnit)
  testy\                      testovací případy a protokoly
```

- **Pod-skript** počítá **jedno pole** okna Fowleru podle metodiky pracoviště. Pracoviště napíše jen ty pod-skripty,
  které chce používat. Co nemá, Fowler nechá prázdné (uživatel ho vyplní jako dosud).
- **Hlavní skript** sám nic nepočítá. Pro každý pod-skript má dva řádky: `import` a přiřazení pole.

  ```js
  import { ztrataFowler } from "./ztrata-fowler.js";
  …
  audio.fowler = ztrataFowler(data.audio, poznamky, data);
  ```

- Hlavní skript pro část, kterou pracoviště nehodnotí, ve složce být nemusí (Fowler pak ukáže poznámku, že pro tuto
  část skript není).
- Pod-skript může použít jiný pod-skript (např. WHO z průměru), stačí ho importovat.

## Postup

1. **Sepište metodiku.** Projděte [Dotazník metodiky](#dotazník-metodiky). Odpovědi jsou zadání skriptů a patří
   i do dokumentace pracoviště. AI asistent smí psát kód teprve podle nich.
2. **Založte složku skriptů ze šablony.** Zkopírujte obsah [priklady/sablona](../priklady/sablona) do složky mimo
   spouštěč, např. `C:\Fowler\skripty\`. Hlavní skripty (`audio.js`, `tymp.js`, `speech.js`) zatím nic neurčí.
   Doplňte v nich hlavičku (pracoviště, verze, schválil). Hlavní skript části, kterou pracoviště hodnotit nebude,
   můžete smazat.
3. **Pro každé pole napište pod-skript.** Zkopírujte [sablona-podskriptu.js](../priklady/sablona/sablona-podskriptu.js)
   pod názvem podle toho, co počítá (např. `ztrata-fowler.js`), přejmenujte funkci `urciPole` (např. na
   `ztrataFowler`) a doplňte část METODIKA PRACOVIŠTĚ:
   - konstanty metodiky (frekvence, hranice, tabulky) na začátek s odkazem na zdroj,
   - data čtěte funkcemi z `cteni-dat.js` (viz [Práce s daty](#práce-s-daty)), každý stav hodnoty (číslo, `NR`,
     neměřeno) ošetřete výslovně podle metodiky,
   - co nelze určit, vraťte jako `null` a důvod přidejte do `poznamky`,
   - zaokrouhlete ve funkci `zaokrouhli` způsobem z metodiky.
4. **Zapojte pod-skript v hlavním skriptu:** `import` nahoře a řádek s přiřazením pole ve funkci `hodnot` (vzory
   jsou v šabloně zakomentované). Řádek „Tento řádek smažte, až zapojíte první pod-skript“ smažte.
5. **Vyzkoušejte na vzorových datech:**
   `spoustec.exe --run C:\Fowler\skripty priklady\audio.json | Out-Host`. Zkontrolujte načtené skripty a poznámku.
   Upozornění pod nadpisem „Kontrola spouštěče“ ukazují pole, která se do Fowleru nezapíšou, a proč.
6. **Připravte testovací případy.** Pro každé pravidlo metodiky aspoň jeden případ, včetně hraničních hodnot
   (přesně na hranici, těsně pod a nad), chybějících hodnot, `NR` a případů, kdy se výsledek určit nemá. Vstup je
   soubor `fowler-exam` (zkopírujte a upravte vzor z `priklady`, data smyšlená nebo anonymizovaná), očekávání
   `*.ocekavano.json` **určí lékař**, ne skript. Popis v [skripty.md](skripty.md#testovací-režim).
7. **Spusťte testy:** `spoustec.exe --test C:\Fowler\skripty C:\Fowler\skripty\testy | Out-Host`.
   Každý ROZDÍL vysvětlete: chyba ve skriptu, nebo v očekávání? Protokol (`protokol-*.txt`) s otisky všech skriptů
   uložte do validační dokumentace pracoviště.
8. **Ověřte na skutečných vyšetřeních.** Porovnejte výsledky skriptů s hodnocením lékaře na vlastních vyšetřeních
   pracoviště, než je začnete používat.
9. **Nastavte ve Fowleru** složku skriptů (viz [README](../README.md#nastavení-ve-fowleru)) a výsledky v okně
   Hodnotit vždy zkontrolujte, než je potvrdíte.
10. **Při každé změně** (nový nebo upravený pod-skript) zvyšte verzi v hlavičce, spusťte testy znovu a uložte nový
    protokol. Odpověď Fowleru i log obsahují otisk všech načtených skriptů, podle kterého lze dohledat, která verze
    hodnocení navrhla ([skripty.md](skripty.md#otisk-skriptů)).

## Dotazník metodiky

Na tyto otázky odpovídá lékař. AI asistent je má projít, než začne psát kód, a na každou nezodpovězenou otázku
se zeptat. Odpověď „jako obvykle“ nestačí, potřeba je konkrétní pravidlo nebo číslo a jeho zdroj.

**Obecně**

1. Které části vyšetření se hodnotí (tónová audiometrie, tympanometrie, slovní audiometrie)? Pro ostatní hlavní
   skript nebude.
2. Která pole okna Fowleru se určují (seznam v [format.md](format.md#odpověď-programu-fowler-eval))? Na každé
   pole jeden pod-skript, ostatní pole zůstanou prázdná.
3. Podle jakého dokumentu (metodika, norma, publikace) a jeho verze? Kde jsou v něm tabulky a hranice?
4. Používá věk nebo pohlaví (`exam.age`, `exam.sex`)? Co když chybí?
5. Co se má stát, když údaj nelze určit (chybí data)? (Obvykle: pole se nevyplní a poznámka uvede proč.)
6. Co má uživatel vidět v poznámce (např. použitá metodika a verze skriptu, upozornění)?

**Pro každé pole (pod-skript)**

7. Ze kterých dat se určuje: ucho (vpravo, vlevo, obě), křivka (`ac` vzduch, `bc` kost, `sisi`, tympanogram,
   reflexy, body slovní audiometrie)?
8. Které frekvence (podněty, body) se použijí? Přesný seznam.
9. Výpočet: vzorec nebo tabulka, přesná čísla. U hranic: patří hraniční hodnota do nižší, nebo vyšší skupiny
   (`<`, nebo `<=`)?
10. **Chybějící hodnota** na potřebné frekvenci (`null`, frekvence není v `hz`): vynechat ji, výsledek neurčit,
    nebo jinak?
11. **Neslyší (`"NR"`)**: výsledek neurčit, nebo počítat s nějakou hodnotou? S jakou a proč?
12. Hodnota mimo obvyklý rozsah nebo nečekaný tvar dat: co dělat?
13. Maskování (`acMasked`, `acMask`, `bcMask`, `mask` u slovní audiometrie): má na výsledek vliv?
14. Vyšetření ze starší verze Fowleru (`legacy: true`, kostní vedení mohlo být doplněné odhadem): použít, nebo
    nepoužít, upozornit?
15. **Zaokrouhlení**: na kolik desetinných míst (pole přijmou nejvýše 2, SRT 1), jak se zaokrouhluje polovina
    (nahoru, od nuly, k sudé), zaokrouhlují se mezivýsledky, nebo jen konečná hodnota?
16. U textových polí: přesné znění textů (nabídky okna jsou ve [format.md](format.md)).
17. Vychází pole z jiného pole (např. WHO z průměru vzdušného vedení)? Pak pod-skript použije ten druhý.

**Tympanometrie navíc**

18. Který tympanogram, když jich ucho má víc (sonda 226 nebo 1000 Hz, první, poslední…)? Ručně zapsané hodnoty
    bez křivky (bez `probeHz`): použít?
19. Reflexy: použít výsledek `res` z dat (`src` přístroj, nebo uživatel), nebo ho určovat z průběhu (`lv`)? Podle
    jakého pravidla?
20. Decay: převzít hodnotu z přístroje (`pct`), nebo jinak?

**Slovní audiometrie navíc**

21. Které křivky (`R`, `L`, `bin`, `aided`)? Jak naložit s maskovanými body? Co když křivka nedosáhne potřebné
    hodnoty?

## Práce s daty

Funkce pro čtení dat jsou v [cteni-dat.js](../priklady/sablona/cteni-dat.js), pod-skript si je načte příkazem
`import`. Jen čtou data a vracejí stav hodnoty, nerozhodují o ní. Ukázka
[priklady/ukazka/prace-s-daty.js](../priklady/ukazka/prace-s-daty.js) jimi projde celou předanou část a vypíše,
co dostala (`spoustec.exe --run priklady\ukazka priklady\audio.json`). Celý pod-skript s výpočtem (čtení,
ošetření stavů, výpočet v celých číslech, zaokrouhlení, `null` s důvodem) ukazuje
[priklady/ukazka/ukazka-vypoctu.js](../priklady/ukazka/ukazka-vypoctu.js) na veličině bez audiologického významu.

```js
import { ucha, nazevUcha, hodnotaNa, popisStavu } from "./cteni-dat.js";

// hodnota na frekvenci podle audio.hz (nikdy podle pořadí v seznamu)
const p = hodnotaNa(audio, "R", "ac", hz);
// p = { stav: "číslo", hodnota: 25 } | { stav: "NR" } | { stav: "neměřeno" } | { stav: "neplatné", hodnota }

// každý stav ošetřit výslovně podle metodiky (otázky 10 až 12 dotazníku)
if (p.stav === "číslo") {
  // použít p.hodnota
} else if (p.stav === "NR") {
  // podle metodiky
} else {
  poznamky.push("… vpravo nelze určit: na " + hz + " Hz je " + popisStavu(p) + ".");
  return null;
}
```

```js
for (const ucho of ucha(audio)) { … }                    // jen uši, které jsou v datech ("R", "L")
const tg = tympanogramy(data.tymp.R, 226);               // tympanogramy pravého ucha se sondou 226 Hz
const reflex = polozkaPodnetu(data.tymp.R.ipsi, 1000);   // reflex IPSI 1000 Hz, undefined když není
const body = bodyKrivky(data.speech.L);                  // [{ db, pct, mask }] křivky levého ucha
if (data.exam.age === undefined) { … }                   // věk není znám
```

Zásady:

- **Nespoléhejte na automatické převody JavaScriptu.** `null` se v aritmetice mění na 0, `"NR" + 5` je `"NR5"`.
  Proto má každá hodnota stav a skript ho ošetří výslovně (úplný seznam v
  [skripty.md](skripty.md#úskalí-javascriptu)).
- **Hledejte podle frekvence nebo podnětu**, ne podle pozice. Sada frekvencí se liší podle přístroje
  a vyšetření.
- **Zaokrouhlujte jen tam, kde to určuje metodika**, a výsledek vracejte jako číslo (ne text z `toFixed`).
- **Nevracejte pole, které nelze určit.** Vraťte `null` a důvod napište do poznámky. Výjimku (`throw`) nechte jen
  na chyby v programu: zastaví celé hodnocení, takže se nevloží ani pole z ostatních pod-skriptů.
- **Do `console.log` nevypisujte celá data** ani poznámku k vyšetření, výstup jde do logu.
- Skripty mají být čitelné pro lékaře, který je schvaluje: české názvy, konstanty metodiky s odkazem na zdroj,
  jeden pod-skript na pole.

## Kontrolní seznam před nasazením

- [ ] Metodika je sepsaná (odpovědi na dotazník) a každý pod-skript na ni odkazuje v hlavičce.
- [ ] Skripty neobsahují nic, co nepochází z metodiky (žádné „obvyklé“ hodnoty doplněné bez zadání).
- [ ] Hlavní skripty jen zapojují pod-skripty, nic nepočítají; řádek „Šablona: … nemá zapojený žádný pod-skript“
      je smazaný.
- [ ] Chybějící hodnoty, `NR` a hranice jsou ošetřené výslovně a mají testovací případy.
- [ ] Očekávané výsledky testů určil lékař, `--test` prošel a protokol je v dokumentaci.
- [ ] Výsledky byly porovnané s hodnocením lékaře na vlastních vyšetřeních.
- [ ] `--run` na vzorových datech neukazuje upozornění „Kontrola spouštěče“.
- [ ] Ve Fowleru je nastavená správná složka skriptů. První výsledky v okně Hodnotit někdo zkontroloval a otisk
      v `app` odpovídá protokolu testů.
- [ ] Pracoviště ví, že za skripty odpovídá a že je bez označení CE nesmí předat jiné právnické osobě
      (čl. 5 odst. 5 MDR).
