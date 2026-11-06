// sablona-podskriptu.js – KOSTRA POD-SKRIPTU PRACOVIŠTĚ (jedno pole hodnocení)
//
// Určuje:         (doplňte pole, např. ztráta dle Fowlera, audio.fowler)
// Metodika:       (doplňte dokument, kapitolu a verzi, podle které pod-skript počítá)
// Verze:          (doplňte, např. 1 z 2026-11-15; při každé změně zvyšte a znovu spusťte testy)
// Schválil:       (doplňte jméno a datum)
//
// Postup: kostru zkopírujte pod názvem podle toho, co počítá (např. ztrata-fowler.js), přejmenujte funkci urciPole
// (např. na ztrataFowler), doplňte část METODIKA PRACOVIŠTĚ a pod-skript zapojte v hlavním skriptu (audio.js,
// tymp.js nebo speech.js): import nahoře a přiřazení pole ve funkci hodnot. Podrobně: docs/navod.md.
// Dokud se metodika nedoplní, pod-skript nic neurčí.
//
// PRO AI ASISTENTA:
// - Postup hodnocení nevymýšlej. Frekvence, tabulky, hranice, vzorce, ošetření chybějících hodnot a "NR"
//   i způsob zaokrouhlení musí určit uživatel (Dotazník metodiky v docs/navod.md). Když něco chybí, zeptej se.
// - Jeden pod-skript = jedno pole odpovědi (tvar pole v docs/format.md, část Odpověď programu).
// - Hodnotu, kterou nelze určit, vrať jako null a důvod přidej do poznamky. Nevyhazuj výjimku: chyba v kterémkoli
//   pod-skriptu zastaví celé hodnocení (Fowler pak nedostane ani pole, která jiné pod-skripty určily).
// - Data čti funkcemi z cteni-dat.js. Rozhraní a úskalí JavaScriptu: docs/skripty.md.

import { ucha, nazevUcha, hodnotaNa, popisStavu } from "./cteni-dat.js";

// ===== METODIKA PRACOVIŠTĚ (doplní pracoviště) =====

// Konstanty metodiky: frekvence, hranice, tabulky… Každou s odkazem na zdroj v metodice. Žádné výchozí hodnoty.
//   const ...   // metodika, kapitola ...

// Funkce pod-skriptu, kterou volá hlavní skript. Dostane:
//   cast      předaná data, se kterými pod-skript počítá (např. data.audio, data.tymp.R, data.speech.L);
//             undefined, když je Fowler nepředal
//   poznamky  seznam poznámek pro uživatele, sem patří důvod, proč pole nelze určit
//   data      celá předaná data bez údajů o pacientovi (např. data.exam.age, data.exam.time)
// Vrátí hodnotu pole ve tvaru z docs/format.md (např. { R: 12.5, L: 30, total: 20.1 }), nebo null.
export function urciPole(cast, poznamky, data) {
  if (cast === undefined) {
    return null;
  }
  // Vzor čtení hodnoty audiogramu a údaje, který nelze určit (frekvenci a ošetření stavů určuje metodika):
  //   const vysledek = {};
  //   for (const ucho of ucha(cast)) {
  //     const p = hodnotaNa(cast, ucho, "ac", HZ_Z_METODIKY);
  //     if (p.stav !== "číslo") {
  //       poznamky.push("… " + nazevUcha(ucho) + " nelze určit: na " + HZ_Z_METODIKY + " Hz je " + popisStavu(p) + ".");
  //       continue;
  //     }
  //     vysledek[ucho] = zaokrouhli(...);
  //   }
  //   return vysledek;
  poznamky.push("Kostra pod-skriptu: výpočet není doplněný, nic se neurčuje.");
  return null;
}

// Zaokrouhlení výsledku na tvar pole. ZPŮSOB URČUJE METODIKA (počet desetinných míst, jak zaokrouhlit polovinu,
// zda se zaokrouhlují mezivýsledky). Spouštěč nezaokrouhluje, hodnotu s víc desetinnými místy vynechá.
// Pozor: Math.round(-2.5) je -2 a (1.005).toFixed(2) je text "1.00" (docs/skripty.md, Úskalí JavaScriptu).
function zaokrouhli(x) {
  throw new Error("Kostra pod-skriptu: doplňte zaokrouhlení podle metodiky pracoviště (funkce zaokrouhli).");
}
