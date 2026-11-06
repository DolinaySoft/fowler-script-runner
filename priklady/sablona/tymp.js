// tymp.js – HLAVNÍ SKRIPT PRO TYMPANOMETRII (okno Hodnotit tympanometrie)
//
// Pracoviště:     (doplňte)
// Verze skriptů:  (doplňte, např. 1 z 2026-11-15; při každé změně zvyšte a znovu spusťte testy)
// Schválil:       (doplňte jméno a datum)
//
// ŠABLONA ze složky priklady/sablona. Celou složku zkopírujte mimo složku spouštěče (např. C:\Fowler\skripty)
// a tu zadejte ve Fowleru jako složku skriptů. Postup: docs/navod.md.
//
// Spouštěč tento skript spustí, když Fowler předá tympanometrii, a zavolá funkci hodnot(data). Hlavní skript sám
// nic nepočítá: zavolá pod-skripty pracoviště (každý určí jedno pole okna pro jedno ucho) a jejich výsledky předá
// Fowleru. Pole, pro které pracoviště pod-skript nemá, se nevrací a Fowler ho nechá, jak je.
//
// PRO AI ASISTENTA:
// - V tomto souboru jen zapojuj pod-skripty: import nahoře a přiřazení pole ve funkci hodnot. Nic nepočítej.
// - Výpočet patří do pod-skriptu (kostra: sablona-podskriptu.js). Postup hodnocení nevymýšlej, určuje ho uživatel
//   (Dotazník metodiky v docs/navod.md).

// ===== ZAPOJENÍ POD-SKRIPTŮ (doplní pracoviště) =====

// Import pod-skriptů, např. (soubor typ-tympanogramu.js si pracoviště napíše samo):
//   import { typTympanogramu } from "./typ-tympanogramu.js";

export function hodnot(data) {
  const poznamky = [];           // poznámky pro uživatele (např. proč něco nelze určit), Fowler je zobrazí
  const tymp = { R: {}, L: {} }; // pole okna Hodnotit tympanometrie pro každé ucho (docs/format.md, Odpověď programu)

  // Pro každé ucho a pole, které pracoviště určuje, jeden řádek. Pod-skript dostane data ucha (data.tymp.R nebo
  // data.tymp.L, undefined když ucho předané není), seznam poznámek a celá data a vrátí hodnotu pole, nebo null,
  // když ji nelze určit (důvod přidá do poznámek). Řádky polí, která pracoviště neurčuje, nechte zakomentované.
  //   tymp.R.type   = typTympanogramu(data.tymp.R, poznamky, data);   // typ křivky, text nejvýše 10 znaků
  //   tymp.L.type   = typTympanogramu(data.tymp.L, poznamky, data);
  //   tymp.R.ipsi   = ...;   // reflexy IPSI [{ stim, res: "+" nebo "-" }]
  //   tymp.R.contra = ...;   // reflexy CONTRA [{ stim, res }]
  //   tymp.R.decayIpsi   = ...;   // decay [{ stim, pct: celé číslo nebo "-" }]
  //   tymp.R.decayContra = ...;
  //   (a totéž pro tymp.L)

  // Tento řádek smažte, až zapojíte první pod-skript.
  poznamky.push("Šablona: hlavní skript tymp.js nemá zapojený žádný pod-skript, nic se neurčuje.");

  return { tymp: tymp, note: poznamky };
}
