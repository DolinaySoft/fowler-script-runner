Spouštěč skriptů pracoviště pro Fowler (spoustec.exe)
=====================================================

Spouštěč je externí program pro Fowler. Převezme data vyšetření, spustí skripty pracoviště v JavaScriptu
a jejich výsledek předá Fowleru do okna Hodnotit ke kontrole. Sám nic nepočítá ani nevyhodnocuje.
Podrobný popis je v README.md, postup psaní skriptů v docs\navod.md.


OBSAH SLOŽKY

  spoustec.exe (.config, .dll)   program
  skripty\                       BEZPEČNÁ UKÁZKA skriptů: nic nehodnotí a do polí okna nic nevloží, jen do zprávy
                                 popíše předaná data a ukázkový výpočet bez audiologického významu
                                 (kopie priklady\ukazka)
  priklady\sablona\              šablona pro vlastní skripty pracoviště
  priklady\                      vzorová data a testy
  docs\                          dokumentace
  README.md, AGENTS.md           popis programu, pokyny pro AI asistenta
  LICENSE, THIRD-PARTY-NOTICES.md


INSTALACE

  1. Program není digitálně podepsaný. Před rozbalením otevřete Vlastnosti staženého ZIPu a zaškrtněte
     Odblokovat.
  2. Rozbalte ZIP do složky Fowleru (např. C:\Fowler). Vznikne složka spoustec s tímto souborem.

  Program běží bez instalace a bez práv správce. Potřebuje .NET Framework 4.8 (součást Windows 10 a 11)
  a verzi Fowleru, která má v nastavení skupinu "Externí program pro hodnocení měření".


NASTAVENÍ VE FOWLERU

  Nastavení programu -> tlačítko Extra... -> skupina "Externí program pro hodnocení měření":

    Program:           spoustec\spoustec.exe
    Parametry:         "%VSTUP%" "%VYSTUP%"
    Vstupní soubor:    (prázdné)
    Výstupní soubor:   (prázdné)
    Spouštět hned po stisku Hodnotit:  podle potřeby

  Relativní cesty jsou od složky Fowleru. Prázdné soubory znamenají hodnoceni_vstup.json
  a hodnoceni_vystup.json ve složce Fowleru. Bez třetího parametru spouštěč použije složku skripty vedle
  spoustec.exe, tedy bezpečnou ukázku.


VYZKOUŠENÍ

  Otevřete vyšetření s tónovou audiometrií, stiskněte Hodnotit a v okně Externí program... Fowler ohlásí,
  že program nevrátil žádné hodnocení, a zobrazí jeho zprávu (popis předaných dat a ukázkový výpočet).
  Do polí okna se nic nevloží. Stejně to funguje pro tympanometrii a pro slovní audiometrii (tlačítko
  Externí program... pod poli SRT).


VLASTNÍ SKRIPTY PRACOVIŠTĚ

  1. Zkopírujte složku priklady\sablona mimo složku spouštěče, např. do C:\Fowler\skripty
     (nová verze spouštěče složky skripty a priklady přepíše).
  2. Ve Fowleru doplňte tuto složku jako třetí parametr:

       Parametry:  "%VSTUP%" "%VYSTUP%" "C:\Fowler\skripty"

  3. Skripty napište podle docs\navod.md, i s pomocí AI asistenta (pokyny pro něj jsou v AGENTS.md).
     Postup hodnocení určuje pracoviště, spouštěč ani ukázky žádný neobsahují.
  4. Než skripty začnete používat, ověřte jejich výsledky na vlastních vyšetřeních (testovací režim --test,
     docs\skripty.md). Za skripty a jejich výsledky odpovídá pracoviště (README.md, část Odpovědnost).
     Výsledky v okně Hodnotit vždy zkontrolujte, než je potvrdíte.


LADĚNÍ

  Log je v souboru spoustec.log ve složce spouštěče (bez osobních údajů pacienta).
  Vyzkoušení bez Fowleru (PowerShell ve složce spouštěče):

    .\spoustec.exe --run skripty priklady\audio.json | Out-Host
