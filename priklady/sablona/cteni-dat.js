// cteni-dat.js – společné funkce pro čtení dat předaných z Fowleru. NIC NEHODNOTÍ.
//
// Pod-skripty si funkce načtou příkazem import, např.:
//   import { ucha, hodnotaNa, popisStavu } from "./cteni-dat.js";
//
// Funkce jen čtou data (popis v docs/format.md) a vracejí, v jakém stavu hodnota je (číslo, "NR", neměřeno).
// Co se s kterým stavem udělá (vynechat, neurčit výsledek, …), určuje metodika pracoviště v pod-skriptu,
// ne tyto funkce.
//
// PRO AI ASISTENTA: tento soubor neměň a nepřidávej do něj žádný postup hodnocení. Vlastní pomocné funkce
// pracoviště patří do pod-skriptů nebo do dalšího souboru ve složce skriptů.

// Uši, které jsou v části (audio, tymp nebo křivky slovní audiometrie): "R", "L". Chybějící ucho v datech není.
export function ucha(cast) {
  return ["R", "L"].filter(function (ucho) { return cast[ucho] !== undefined; });
}

// Název ucha nebo křivky pro poznámky: "vpravo", "vlevo", "binaurálně".
export function nazevUcha(ucho) {
  return (ucho === "R") ? "vpravo" : (ucho === "L") ? "vlevo" : "binaurálně";
}

// Hodnota křivky audiogramu (ac, bc, ucl, mcl, sisi, acMask, bcMask) na frekvenci hz.
// Hodnotu hledá podle frekvence v audio.hz, nikdy podle pořadí (sada frekvencí není pevná).
// Vrací jeden ze stavů:
//   { stav: "číslo", hodnota: 25 }   naměřená hodnota (dB, u sisi %)
//   { stav: "NR" }                   neslyší (jen ac a bc)
//   { stav: "neměřeno" }             null, chybí ucho, křivka nebo frekvence v audio.hz
//   { stav: "neplatné", hodnota }    jiná hodnota (Fowler ji nepředává, ale skript na to nemá spoléhat)
export function hodnotaNa(audio, ucho, krivka, hz) {
  const i = audio.hz.indexOf(hz);
  const hodnoty = audio[ucho] ? audio[ucho][krivka] : undefined;
  if (i < 0 || !hodnoty || hodnoty[i] === null || hodnoty[i] === undefined) {
    return { stav: "neměřeno" };
  }
  const h = hodnoty[i];
  if (h === "NR") {
    return { stav: "NR" };
  }
  if (typeof h === "number" && Number.isFinite(h)) {
    return { stav: "číslo", hodnota: h };
  }
  return { stav: "neplatné", hodnota: h };
}

// Stav hodnoty z hodnotaNa do poznámky pro uživatele: "25", "neslyší (NR)", "neměřeno", "neplatná hodnota".
export function popisStavu(p) {
  if (p.stav === "číslo") {
    return String(p.hodnota);
  }
  if (p.stav === "NR") {
    return "neslyší (NR)";
  }
  return (p.stav === "neměřeno") ? "neměřeno" : "neplatná hodnota";
}

// Kmitočet sondy tympanogramu. Ručně zapsané hodnoty (bez probeHz) se podle docs/format.md považují za 226 Hz.
export function sondaTympanogramu(tg) {
  return (tg.probeHz !== undefined) ? tg.probeHz : 226;
}

// Tympanogramy ucha (tymp.R nebo tymp.L) s danou sondou (226 nebo 1000 Hz), v pořadí z dat.
export function tympanogramy(uchoTymp, sonda) {
  return (uchoTymp.tg || []).filter(function (tg) { return sondaTympanogramu(tg) === sonda; });
}

// Položka seznamu reflexů nebo decay (ipsi, contra, decayIpsi, decayContra) pro podnět (500, 1000, …, "LP", "HP",
// "WB"), undefined když není.
export function polozkaPodnetu(seznam, stim) {
  return (seznam || []).find(function (p) { return p.stim === stim; });
}

// Podnět pro poznámky: "1000 Hz", "šum WB".
export function popisPodnetu(stim) {
  return (typeof stim === "number") ? stim + " Hz" : "šum " + stim;
}

// Křivky slovní audiometrie, které jsou v datech: [{ klic, nazev, krivka, sluchadlo }].
// klic je "R", "L" nebo "bin"; křivky se sluchadlem jsou v speech.aided.
export function krivkySlovni(speech) {
  const krivky = [];
  for (const sluchadlo of [false, true]) {
    const skupina = sluchadlo ? speech.aided : speech;
    if (!skupina) {
      continue;
    }
    for (const klic of ["R", "L", "bin"]) {
      if (skupina[klic]) {
        krivky.push({ klic: klic, nazev: nazevUcha(klic) + (sluchadlo ? " se sluchadlem" : ""), krivka: skupina[klic], sluchadlo: sluchadlo });
      }
    }
  }
  return krivky;
}

// Body křivky slovní audiometrie jako seznam objektů [{ db, pct, mask }] (souběžná pole db, pct, mask spojená
// podle pořadí). mask: null = nemaskováno, číslo = úroveň maskování [dB], "M" = maskováno bez úrovně.
export function bodyKrivky(krivka) {
  return krivka.db.map(function (db, i) {
    return { db: db, pct: krivka.pct[i], mask: (krivka.mask && krivka.mask[i] !== undefined) ? krivka.mask[i] : null };
  });
}
