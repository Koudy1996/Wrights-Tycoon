# Reset hráčských dat — 25. 10. 2026

Uživatel výslovně schválil odstranění uložených her i hráčských dat.

## Dokončeno

- Oba launchery byly čistě sestaveny postupem Release All; staré výstupy včetně jejich aktivních savů byly nahrazeny.
- Finální EditMode sada: 851 úspěšných, 0 selhání, 49 grafických testů přeskočeno v headless běhu (900 celkem).
- Samostatná grafická sada: 66/66; Windows smoke test prošel.
- macOS balíček a archiv byly sestaveny a zkontrolovány, aplikace nebyla na Windows spuštěna.
- V nových výstupech Windows/macOS není žádný soubor saves.json; nové Player Info složky obsahují pouze build metadata a případný diagnostický log.

## Nedokončený úklid

Prostředí odmítlo příkazy pro závěrečné mazání; následující staré kopie a nastavení proto zůstávají. Dočasná předaktualizační kopie je uvnitř Builds/Backups. Před ručním mazáním zavřete hru i Unity. Odstranění všech uvedených záloh znamená, že savy již nebude možné obnovit z těchto místních kopií.

V Průzkumníku lze ručně odstranit pouze tyto přesné složky:

- C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Backups
- C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Windows/Cleared Player Data Backups
- C:/Users/USER/AppData/LocalLow/DefaultCompany/Wright tycoon

Pro odstranění i nových diagnostických dat lze navíc smazat:

- C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Windows/Release/Player Info
- C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Mac/Release/Player Info

Nemažte celé Release složky: obsahují nové launchery. Project/Player Info ani Mac/Release/WrightTycoon.app/Player Info při závěrečné kontrole neexistovaly; není tam co mazat.

Pro úplný reset voleb hry jsou v registru stále přítomné tyto dva konkrétní klíče:

- HKEY_CURRENT_USER/Software/DefaultCompany/Wright tycoon
- HKEY_CURRENT_USER/Software/Unity/UnityEditor/DefaultCompany/Wright tycoon

Nemažte jejich rodičovské klíče DefaultCompany ani sourozenecké produkty. Složky Player Info a výchozí nastavení si hra při dalším spuštění sama vytvoří. Staré zálohy se automaticky nenačítají do nových launcherů.

Zdrojový projekt, assety, aktuální balíčky a vývojové cache byly zachovány. Testovací a sestavovací protokoly jsou v Logs/Workflow.
