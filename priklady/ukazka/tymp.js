// tymp.js – UKÁZKOVÝ HLAVNÍ SKRIPT PRO TYMPANOMETRII. NIC NEHODNOTÍ.
//
// Ukázka složky skriptů: hlavní skript načte pod-skript prace-s-daty.js (příkaz import) a ten do poznámek popíše,
// co Fowler předal. Žádné pole hodnocení se nevrací. Skripty pracoviště zakládejte ze šablony v priklady/sablona,
// ne z této ukázky.
//
// Vyzkoušení: spoustec.exe --run priklady\ukazka priklady\tympanometrie.json

import { popisTympanometrie } from "./prace-s-daty.js";

export function hodnot(data) {
  const poznamky = [];
  const tymp = { R: {}, L: {} };

  // Pod-skript pracoviště by tu určil pole, např. tymp.R.type = typTympanogramu(data.tymp.R, poznamky, data);
  // ukázkový pod-skript nic neurčí, jen přidá do poznámek popis dat.
  popisTympanometrie(data.tymp, poznamky, data);

  return { tymp: tymp, note: poznamky };
}
