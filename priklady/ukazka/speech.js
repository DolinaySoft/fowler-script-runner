// speech.js – UKÁZKOVÝ HLAVNÍ SKRIPT PRO SLOVNÍ AUDIOMETRII. NIC NEHODNOTÍ.
//
// Ukázka složky skriptů: hlavní skript načte pod-skript prace-s-daty.js (příkaz import) a ten do poznámek popíše,
// co Fowler předal. Žádné pole hodnocení se nevrací. Skripty pracoviště zakládejte ze šablony v priklady/sablona,
// ne z této ukázky.
//
// Vyzkoušení: spoustec.exe --run priklady\ukazka priklady\slovni.json

import { popisSlovni } from "./prace-s-daty.js";

export function hodnot(data) {
  const poznamky = [];
  const speech = { srt: {}, aided: { srt: {} } };

  // Pod-skript pracoviště by tu určil pole, např. speech.srt.R = srt(data.speech.R, poznamky, data);
  // ukázkový pod-skript nic neurčí, jen přidá do poznámek popis dat.
  popisSlovni(data.speech, poznamky, data);

  return { speech: speech, note: poznamky };
}
