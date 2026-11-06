// speech.js – HLAVNÍ SKRIPT PRO SLOVNÍ AUDIOMETRII (pole SRT)
//
// Pracoviště:     (doplňte)
// Verze skriptů:  (doplňte, např. 1 z 2026-11-15; při každé změně zvyšte a znovu spusťte testy)
// Schválil:       (doplňte jméno a datum)
//
// ŠABLONA ze složky priklady/sablona. Celou složku zkopírujte mimo složku spouštěče (např. C:\Fowler\skripty)
// a tu zadejte ve Fowleru jako složku skriptů. Postup: docs/navod.md.
//
// Spouštěč tento skript spustí, když Fowler předá slovní audiometrii, a zavolá funkci hodnot(data). Hlavní skript
// sám nic nepočítá: zavolá pod-skripty pracoviště a jejich výsledky předá Fowleru. Pole, pro které pracoviště
// pod-skript nemá, se nevrací a Fowler ho nechá, jak je.
//
// PRO AI ASISTENTA:
// - V tomto souboru jen zapojuj pod-skripty: import nahoře a přiřazení pole ve funkci hodnot. Nic nepočítej.
// - Výpočet patří do pod-skriptu (kostra: sablona-podskriptu.js). Postup hodnocení nevymýšlej, určuje ho uživatel
//   (Dotazník metodiky v docs/navod.md).

// ===== ZAPOJENÍ POD-SKRIPTŮ (doplní pracoviště) =====

// Import pod-skriptů, např. (soubor srt.js si pracoviště napíše samo):
//   import { srt } from "./srt.js";

export function hodnot(data) {
  const poznamky = [];                // poznámky pro uživatele (např. proč něco nelze určit), Fowler je zobrazí
  const speech = { srt: {}, aided: { srt: {} } };   // pole SRT (docs/format.md, Odpověď programu)

  // Pro každé pole, které pracoviště určuje, jeden řádek. Pod-skript dostane křivku (data.speech.R, .L, .bin,
  // se sluchadlem data.speech.aided.R …; undefined, když křivka předaná není), seznam poznámek a celá data a vrátí
  // hodnotu pole, nebo null, když ji nelze určit (důvod přidá do poznámek). Řádky polí, která pracoviště neurčuje,
  // nechte zakomentované. Křivky se sluchadlem jsou v data.speech.aided, který nemusí být předaný.
  //   const aided = data.speech.aided || {};
  //   speech.srt.R   = srt(data.speech.R, poznamky, data);     // SRT vpravo [dB], nejvýše 1 desetinné místo
  //   speech.srt.L   = srt(data.speech.L, poznamky, data);
  //   speech.srt.bin = srt(data.speech.bin, poznamky, data);
  //   speech.aided.srt.R = srt(aided.R, poznamky, data);      // totéž se sluchadlem

  // Tento řádek smažte, až zapojíte první pod-skript.
  poznamky.push("Šablona: hlavní skript speech.js nemá zapojený žádný pod-skript, nic se neurčuje.");

  return { speech: speech, note: poznamky };
}
