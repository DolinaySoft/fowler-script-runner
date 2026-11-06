// audio.js – UKÁZKOVÝ HLAVNÍ SKRIPT PRO TÓNOVOU AUDIOMETRII. NIC NEHODNOTÍ.
//
// Ukázka složky skriptů: hlavní skript načte pod-skripty (příkaz import). prace-s-daty.js do poznámek popíše,
// co Fowler předal, ukazka-vypoctu.js ukazuje techniku výpočtu na veličině bez audiologického významu. Žádné pole
// hodnocení se nevrací. Skripty pracoviště zakládejte ze šablony v priklady/sablona, ne z této ukázky.
//
// Vyzkoušení: spoustec.exe --run priklady\ukazka priklady\audio.json

import { popisAudia } from "./prace-s-daty.js";
import { prumernaFrekvence } from "./ukazka-vypoctu.js";

export function hodnot(data) {
  const poznamky = [];
  const audio = {};

  // Pod-skript pracoviště by tu určil pole, např. audio.fowler = ztrataFowler(data.audio, poznamky, data);
  // ukázkový pod-skript nic neurčí, jen přidá do poznámek popis dat.
  popisAudia(data.audio, poznamky, data);

  // Ukázkový výpočet: výsledek má tvar pole { R, L, total }, ale nemá audiologický význam, proto se do žádného pole
  // okna nepřiřazuje, jen se vypíše do poznámky.
  const ukazka = prumernaFrekvence(data.audio, poznamky, data);
  if (ukazka !== null) {
    poznamky.push("Ukázka výpočtu bez audiologického významu (do okna se nevkládá): průměrná frekvence naměřených " +
      "prahů vzduchu [kHz] " + JSON.stringify(ukazka) + ".");
  }

  // výstup console.log se zapíše do logu spouštěče (při ladění přes --run se vypíše)
  console.log("Předané části: " + Object.keys(data).join(", "));

  return { audio: audio, note: poznamky };
}
