# Pokyny pro AI asistenta

Repozitář obsahuje spouštěč (`spoustec.exe`), který spustí skripty pracoviště v JavaScriptu nad daty vyšetření
předanými programem Fowler a výsledek formálně zkontroluje (popis v README.md). Spouštěč žádné hodnocení
neobsahuje. Tvým úkolem bývá pomoci uživateli (lékaři nebo vývojáři pracoviště) napsat **skripty** podle postupu,
který určí on.

Postup práce je v `docs/navod.md`. Začni **Dotazníkem metodiky** (tamtéž): projdi ho s uživatelem a piš kód teprve
podle jeho odpovědí. Složku skriptů zakládej ze šablony `priklady/sablona`, čtení dat ukazuje
`priklady/ukazka/prace-s-daty.js` a techniku výpočtu v pod-skriptu (čtení, ošetření stavů hodnot, výpočet v celých
číslech, zaokrouhlení, `null` s důvodem) `priklady/ukazka/ukazka-vypoctu.js`. Ten počítá veličinu bez
audiologického významu: přebírej z něj jen techniku, ne frekvence, ošetření `"NR"` ani zaokrouhlení, a jeho výsledek
nikdy nepřiřazuj do pole hodnocení. Ukázky záměrně neobsahují žádný postup hodnocení a ty ho do nich ani do
repozitáře nepřidávej.

## Uspořádání skriptů

- **Složka skriptů** pracoviště (např. `C:\Fowler\skripty`) obsahuje pro každou hodnocenou část vyšetření
  **hlavní skript** pojmenovaný podle části: `audio.js`, `tymp.js`, `speech.js`. Spouštěč spustí jen hlavní skript
  předané části a zavolá jeho exportovanou funkci `hodnot(data)`.
- Výpočet patří do **pod-skriptů**: jeden soubor na jedno pole odpovědi (např. `ztrata-fowler.js` pro
  `audio.fowler`), kostra je `priklady/sablona/sablona-podskriptu.js`. Pod-skript exportuje funkci
  `(cast, poznamky, data)`, která vrátí hodnotu pole, nebo `null` s důvodem v `poznamky`.
- **Hlavní skript nic nepočítá**, jen zapojuje pod-skripty: `import { … } from "./soubor.js";` a přiřazení pole
  ve funkci `hodnot` (vzory jsou v šabloně zakomentované). Když uživatel chce další pole, napiš nový pod-skript
  a zapoj ho, nepřidávej výpočet do hlavního skriptu.
- Importovat lze jen soubory `.js` ze složky skriptů relativní cestou začínající `./` (z podsložky i `../`).
  Žádné knihovny, `require`, soubory mimo složku.

## Pravidla

- **Postup hodnocení neurčuj sám.** Frekvence, tabulky, hranice, klasifikace, vzorce a způsob zaokrouhlení musí
  pocházet od uživatele (metodika jeho pracoviště). Když v zadání chybí, zeptej se. Nedoplňuj je z obecných
  znalostí ani nevybírej „obvyklé“ hodnoty, ani jako výchozí návrh. Za hodnocení odpovídá pracoviště.
- **Piš jen skripty pracoviště:** pod-skripty (podle kostry doplň jen část METODIKA PRACOVIŠTĚ) a zapojení
  v hlavních skriptech. `cteni-dat.js` neměň. Spouštěč (`src/`), testy spouštěče (`tests/`), dokumentaci ani
  příklady neměň. Složku skriptů měj mimo repozitář a mimo složku spouštěče (např. `C:\Fowler\skripty`).
- **Rozhraní** je v `docs/skripty.md`: hlavní skript `export function hodnot(data)` vrací
  `{ audio | tymp | speech: {...}, note: [...] }` nebo `null`. K dispozici je jen standardní JavaScript, `import`
  pod-skriptů a `console.log`. Skripty běží v přísném režimu (strict mode) a s limity času a paměti.
- **Data:** popis v `docs/format.md` (část Předávaná data), vzorová data v `priklady/*.json`. Skript dostane data
  bez bloku `patient`. Používej jen předaná data.
  - Hodnoty čti funkcemi z `cteni-dat.js` (`hodnotaNa` vrací stav `číslo`, `NR`, `neměřeno`, `neplatné`).
  - Chybějící hodnoty (`null`, chybějící klíč) ani `"NR"` nedoplňuj, neodhaduj a nenechávej je projít výpočtem
    (v JavaScriptu se `null` tiše mění na 0, viz „Úskalí JavaScriptu“ v `docs/skripty.md`). Ošetři každý stav
    výslovně podle pokynu uživatele. Když pokyn chybí, zeptej se.
  - Frekvence hledej podle hodnoty v `hz`, ne podle pozice. Nepředpokládej pevnou sadu frekvencí.
  - U `legacy: true` může kostní vedení obsahovat odhad. Zeptej se uživatele, jak s tím naložit.
- **Výsledek:** jen pole z `docs/format.md` (část Odpověď programu) pro předanou část.
  - Co nelze určit, nevracej (vrať `null`) a důvod napiš do poznámek. Výjimku nevyhazuj: chyba v kterémkoli
    pod-skriptu zastaví celé hodnocení.
  - Zaokrouhli v pod-skriptu na tvar pole (počet desetinných míst) způsobem, který určí uživatel. Spouštěč
    nezaokrouhluje a hodnotu s víc desetinnými místy vynechá.
  - Čísla vracej jako čísla, ne texty.
- **Osobní údaje:** skripty je nedostávají. Do `console.log` nevypisuj celá data ani poznámku k vyšetření
  (`note`), výstup jde do logu.
- **Vyzkoušej** skripty na vzorových datech a ukaž uživateli výsledky, aby je mohl porovnat se svým hodnocením:

  ```
  spoustec.exe --run C:\Fowler\skripty priklady\audio.json
  spoustec.exe --test C:\Fowler\skripty C:\Fowler\skripty\testy
  ```

  Ve výstupu zkontroluj seznam načtených skriptů a poznámku „Kontrola spouštěče“. Upozorňuje na pole, která se
  do Fowleru nezapíšou.
- **Testovací případy:** pomoz uživateli připravit složku s testy (`*.json` a `*.ocekavano.json`, popis
  v `docs/skripty.md`). **Očekávané hodnoty musí určit uživatel** (např. z hodnocení, které provedl ručně). Nepočítej
  je skriptem ani sám, jinak test ověřuje skript sám proti sobě. Vstupní data testů jsou smyšlená nebo anonymizovaná.
- Připomeň uživateli, že výsledky skriptů musí před používáním ověřit na vlastních vyšetřeních (protokol `--test`
  do validační dokumentace), po každé změně pod-skriptu testy zopakovat a ve Fowleru výsledky vždy zkontrolovat,
  než je potvrdí.
