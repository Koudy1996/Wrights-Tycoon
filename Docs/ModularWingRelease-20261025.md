# Modulární křídla — aktualizace 25. 10. 2026

## Rozsah

Rodinný designér nahrazuje pevné profily třemi slidery. Číselné tabulky,
pojmenování a výpočet jsou v [ModularWingProfiles.md](ModularWingProfiles.md).
Formát uloženého stavu je 14; starší i budoucí neznámé verze se odmítají
před resetováním aktuální hry. Staré savy nejsou migrovány.

## Ověření

- Kompletní headless EditMode běh: 958 úspěšných, 0 selhání,
  52 grafických testů přeskočeno (1010 celkem).
- Kompletní grafický EditMode běh: **1010/1010**, 0 selhání a 0 přeskočení.
  Protokol: `Logs/Workflow/modular-wing-graphics-20261025.xml`.
- Nové testy zahrnují číselné kotvy, interpolaci, neplatné vstupy,
  54 kombinací při sestavení letadla, výrobní rozpisy, servisní ekonomiku
  přes výsledné parametry, uložené rodiny, odmítnutí nekompatibilních savů
  a Layout/Repaint ovládání profilů.

## Reset dat

Uživatel výslovně požaduje odstranit i hráčská data. Čistý Release postup
nahrazuje předchozí výstupy launcherů včetně jejich zabalených hráčských dat.
Přímé mazání zbylých záloh, AppData a registru ovšem prostředí odmítlo
(`blocked by policy`). Nebyl použit náhradní způsob mazání.

Zbylé staré kopie, které nelze označit za odstraněné:

- `C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Backups`
- `C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Windows/Cleared Player Data Backups`
- `C:/Users/USER/AppData/LocalLow/DefaultCompany/Wright tycoon`
- `HKEY_CURRENT_USER/Software/DefaultCompany/Wright tycoon`
- `HKEY_CURRENT_USER/Software/Unity/UnityEditor/DefaultCompany/Wright tycoon`

Tyto konkrétní složky a klíče může uživatel odstranit ručně při zavřené hře
a editoru; rodičovské složky a klíče neodstraňovat. Úplný reset je nevratný
bez jiné zálohy. Nový launcher jejich staré savy nedokáže načíst.
Zdrojové assety, Unity Library a diagnostické protokoly zůstávají zachovány.

## Sestavení

První IL2CPP pokus selhal kvůli chybějícímu Visual Studio C++ toolchainu
a Windows SDK. Pro opakované čisté sestavení je dočasně použit Mono backend;
výchozí IL2CPP nastavení projektu bude po sestavení obnoveno.
Opakovaný čistý Release All uspěl včetně kompletního headless testu
(958/1010, 52 grafických přeskočeno) a Windows smoke testu. Původní
Standalone IL2CPP backend byl poté obnoven. macOS balíček a distribuční
archiv byly vytvořeny; na Windows nelze aplikaci macOS spustit.
