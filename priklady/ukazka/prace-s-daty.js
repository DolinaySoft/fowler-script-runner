// prace-s-daty.js – ukázkový pod-skript: jak číst data předaná z Fowleru. NIC NEHODNOTÍ.
//
// Funkce popisAudia, popisTympanometrie a popisSlovni mají stejné rozhraní jako pod-skript pracoviště (cast,
// poznamky, data), ale žádné pole neurčí: do poznámek jen vypíšou, CO dostaly (které křivky, kolik hodnot, kolik
// "NR", kolik neměřených frekvencí, kolik tympanogramů a reflexů) a vrátí null.
//
// Data čtou funkcemi z cteni-dat.js (stejný soubor je v priklady/sablona). Ty jen čtou data a vracejí, v jakém
// stavu hodnota je (číslo, "NR", neměřeno). Co se s kterým stavem udělá (vynechat, neurčit výsledek, …), určuje
// metodika pracoviště, ne tyto funkce.
//
// PRO AI ASISTENTA: tento soubor ukazuje strukturu dat (docs/format.md) a bezpečné čtení hodnot. Neobsahuje
// a nesmí obsahovat žádný postup hodnocení (frekvence, hranice, tabulky, vzorce, zaokrouhlení). Ten musí určit
// uživatel, viz docs/navod.md (Dotazník metodiky) a AGENTS.md.

import {
  ucha, nazevUcha, hodnotaNa, tympanogramy, sondaTympanogramu, popisPodnetu, krivkySlovni, bodyKrivky
} from "./cteni-dat.js";

// Úvodní řádek: odkdy je vyšetření a které údaje o vyšetření jsou předané (věk a pohlaví jsou v data.exam).
export function popisVysetreni(data, poznamky) {
  poznamky.push("Skript běží, předáno vyšetření z " + data.exam.time + "; věk " + (data.exam.age ? "je" : "není") +
    " uveden, pohlaví " + (data.exam.sex ? "je" : "není") + " uvedeno.");
}

export function popisAudia(audio, poznamky, data) {
  popisVysetreni(data, poznamky);
  poznamky.push("Tónová audiometrie" + (audio.legacy ? " (uloženo verzí Fowleru před MDR)" : "") +
    ": frekvence " + audio.hz.join(", ") + " Hz.");
  for (const ucho of ucha(audio)) {
    const casti = [];
    for (const krivka of ["ac", "bc", "ucl", "mcl", "sisi"]) {
      const stavy = audio.hz.map(function (hz) { return hodnotaNa(audio, ucho, krivka, hz).stav; });
      const pocet = spocitej(stavy);
      if (pocet["číslo"] || pocet["NR"]) {
        casti.push(krivka + ": hodnot " + (pocet["číslo"] || 0) + ", NR " + (pocet["NR"] || 0) +
          ", neměřeno " + (pocet["neměřeno"] || 0));
      }
    }
    if (audio[ucho].acMasked) {
      casti.push("vzduch označen jako maskovaný");
    }
    poznamky.push(velkym(nazevUcha(ucho)) + ": " + (casti.length ? casti.join("; ") : "bez hodnot") + ".");
  }
  return null;
}

export function popisTympanometrie(tymp, poznamky, data) {
  popisVysetreni(data, poznamky);
  poznamky.push("Tympanometrie" + (tymp.device ? " (" + tymp.device + ")" : "") + ":");
  for (const ucho of ucha(tymp)) {
    const u = tymp[ucho];
    const casti = ["tympanogramů 226 Hz: " + tympanogramy(u, 226).length + ", 1000 Hz: " + tympanogramy(u, 1000).length];
    for (const tg of u.tg || []) {
      casti.push("tympanogram " + sondaTympanogramu(tg) + " Hz, vrchol " +
        (tg.peakP !== undefined && tg.peakY !== undefined ? "uveden" : "neuveden") +
        ", bodů křivky " + (tg.p ? tg.p.length : 0));
    }
    for (const seznam of ["ipsi", "contra"]) {
      for (const reflex of u[seznam] || []) {
        casti.push(seznam + " " + popisPodnetu(reflex.stim) + ": intenzit " + reflex.lv.length +
          ", výsledek " + (reflex.res ? "uveden (" + (reflex.src === "dev" ? "z přístroje" : "zadal uživatel") + ")" : "neuveden"));
      }
    }
    for (const seznam of ["decayIpsi", "decayContra"]) {
      for (const decay of u[seznam] || []) {
        casti.push(seznam + " " + popisPodnetu(decay.stim) + ": decay z přístroje " +
          (decay.pct !== undefined ? "uveden" : "neuveden"));
      }
    }
    poznamky.push(velkym(nazevUcha(ucho)) + ": " + casti.join("; ") + ".");
  }
  return null;
}

export function popisSlovni(speech, poznamky, data) {
  popisVysetreni(data, poznamky);
  poznamky.push("Slovní audiometrie:");
  for (const k of krivkySlovni(speech)) {
    const body = bodyKrivky(k.krivka);
    const maskovano = body.filter(function (b) { return b.mask !== null; }).length;
    poznamky.push(velkym(k.nazev) + ": bodů " + body.length + ", maskovaných " + maskovano + ".");
  }
  return null;
}

// Počty stavů, např. { "číslo": 9, "NR": 1 }.
function spocitej(stavy) {
  const pocet = {};
  for (const s of stavy) {
    pocet[s] = (pocet[s] || 0) + 1;
  }
  return pocet;
}

function velkym(text) {
  return text.charAt(0).toUpperCase() + text.slice(1);
}
