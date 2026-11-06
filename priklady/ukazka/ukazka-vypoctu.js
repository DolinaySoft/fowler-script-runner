// ukazka-vypoctu.js – ukázkový pod-skript S VÝPOČTEM. VÝSLEDEK NEMÁ AUDIOLOGICKÝ VÝZNAM A NIC NEHODNOTÍ.
//
// Určuje:         průměrnou frekvenci, na které je naměřen práh vzduchu, vpravo, vlevo a z obou uší [kHz];
//                 veličina je vymyšlená jen pro ukázku, Fowler pro ni nemá pole a nic z ní neplyne
// Metodika:       žádná (ukázka techniky výpočtu, ne postupu hodnocení)
// Verze:          1 z 2026-11-06
// Schválil:       – (ukázka se k hodnocení nepoužívá)
//
// Je to kostra sablona-podskriptu.js vyplněná od začátku do konce. Ukazuje, jak se v pod-skriptu počítá:
// - konstanty „metodiky“ na jednom místě nahoře (část METODIKA),
// - čtení hodnot podle frekvence funkcí hodnotaNa (cteni-dat.js) a výslovné rozhodnutí o každém stavu hodnoty
//   (číslo, "NR", neměřeno, neplatné),
// - výpočet v celých číslech a zaokrouhlení „polovina od nuly“ bez chyb binárních desetinných čísel,
// - výsledek ve tvaru pole odpovědi { R, L, total } a null s důvodem v poznámkách, když ho nelze určit.
// Hlavní skript ukazka/audio.js výsledek jen vypíše do poznámky (note), do žádného pole okna ho nepřiřazuje.
//
// Vyzkoušení: spoustec.exe --run priklady\ukazka priklady\audio.json
//   (vzorová data: vpravo 9 naměřených prahů a "NR" na 8000 Hz, vlevo 10 prahů → { R: 2.04, L: 2.64, total: 2.36 })
//
// PRO AI ASISTENTA:
// - Soubor ukazuje TECHNIKU výpočtu, ne postup hodnocení. Frekvence, ošetření "NR" a chybějících hodnot ani způsob
//   zaokrouhlení z něj nepřebírej do skriptů pracoviště, ty určuje metodika uživatele (Dotazník metodiky
//   v docs/navod.md).
// - Výsledek této ukázky nikdy nepřiřazuj do pole hodnocení (audio.fowler, audio.acAvg, …).

import { ucha, nazevUcha, hodnotaNa } from "./cteni-dat.js";

// ===== METODIKA UKÁZKY (vymyšlená, bez odborného významu) =====

const KRIVKA = "ac";          // křivka, ze které se počítá: vzdušné vedení
const ZAPOCITAT_NR = false;   // "NR" (neslyší): false = frekvence se vynechá, true = počítá se jako naměřená
const DESETINNA_MISTA = 2;    // výsledek v kHz na 2 desetinná místa, polovina se zaokrouhluje od nuly

// Funkce pod-skriptu se stejným rozhraním jako pod-skript pracoviště (cast, poznamky, data). Vrátí
// { R, L, total } [kHz]: ucho, pro které výsledek nelze určit, se vynechá (důvod v poznámkách), total jen když
// jsou určené obě uši; nic = null.
export function prumernaFrekvence(audio, poznamky) {
  if (audio === undefined) {
    return null;
  }
  const vysledek = {};
  const soucty = {};          // ucho → { soucetHz, pocet } pro výpočet z obou uší
  for (const ucho of ucha(audio)) {
    let soucetHz = 0;
    let pocet = 0;
    let neplatnaHz = null;
    for (const hz of audio.hz) {
      const p = hodnotaNa(audio, ucho, KRIVKA, hz);
      if (p.stav === "neplatné") {
        neplatnaHz = hz;
        break;
      }
      if (p.stav === "číslo" || (p.stav === "NR" && ZAPOCITAT_NR)) {
        soucetHz += hz;
        pocet++;
      }
      // "neměřeno" (a "NR", když se nezapočítává) se vynechá
    }
    if (neplatnaHz !== null) {
      poznamky.push("Ukázka výpočtu " + nazevUcha(ucho) + ": na " + neplatnaHz + " Hz je neplatná hodnota, nelze určit.");
      continue;
    }
    if (pocet === 0) {
      poznamky.push("Ukázka výpočtu " + nazevUcha(ucho) + ": žádný naměřený práh vzduchu, nelze určit.");
      continue;
    }
    soucty[ucho] = { soucetHz: soucetHz, pocet: pocet };
    vysledek[ucho] = prumerKHz(soucetHz, pocet);
  }
  if (soucty.R && soucty.L) {
    vysledek.total = prumerKHz(soucty.R.soucetHz + soucty.L.soucetHz, soucty.R.pocet + soucty.L.pocet);
  }
  return (Object.keys(vysledek).length > 0) ? vysledek : null;
}

// Průměr soucetHz / pocet převedený na kHz a zaokrouhlený na DESETINNA_MISTA. Počítá se v celých číslech
// (u DESETINNA_MISTA = 2 v setinách kHz) a dělí se až nakonec, protože desetinná čísla JavaScriptu jsou binární:
// (1.005).toFixed(2) je "1.00" a Math.round(-2.5) je -2 (docs/skripty.md, Úskalí JavaScriptu).
function prumerKHz(soucetHz, pocet) {
  const dilku = 10 ** DESETINNA_MISTA;   // dílků kHz v 1 kHz (100 = setiny)
  // soucetHz / pocet [Hz] = soucetHz / pocet / 1000 [kHz] = soucetHz * dilku / (pocet * 1000) [dílků kHz]
  return podilZaokrouhleny(soucetHz * dilku, pocet * 1000) / dilku;
}

// Podíl celých čísel citatel / jmenovatel (jmenovatel > 0) zaokrouhlený na celé číslo, polovina od nuly.
function podilZaokrouhleny(citatel, jmenovatel) {
  const a = Math.abs(citatel);
  let cele = Math.floor(a / jmenovatel);
  if (2 * (a - cele * jmenovatel) >= jmenovatel) {
    cele++;
  }
  return (citatel < 0 && cele !== 0) ? -cele : cele;
}
