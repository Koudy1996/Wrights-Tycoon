# Aktualizace launcherů a stav resetu dat — 26. 10. 2026

## Dokončeno

- Obě aplikace byly sestaveny z aktuálního pracovního stromu včetně technického úklidu. Workflow: `Scripts/WrightTycoonWorkflow.cmd -Action Checkpoint -Platform All -TestSuite Full`.
- Oba buildy použily `BuildOptions.None`, nikoliv scripts-only: aktualizoval se celý obsah aplikací, s využitím nepoškozených Unity cache.
- Full EditMode: **1 086/1 086 úspěšných, 0 přeskočených**. Grafické testy běžely na oddělené, neaktivní ploše Windows.
- Windows smoke test prošel; načetla se kampaň a vznikl launch runner.
- U obou balíčků prošla kontrola 14 událostí historické kampaně a jejich obrázků.
- Manifesty mají shodný balance checksum `8c6c977acbae352f141eb6e1879751142b79b836136dfd7dad9d8c1366ca9613`, verzi 0.6.7 a 17 balance souborů. Windows sestaven 2026-10-26 13:33:54 UTC, macOS 13:34:19 UTC.
- Standardní build workflow odstranil zastaralý `Builds/Mac/WrightTycoon-macOS-Release.tar.gz`. Aktuální macOS výstup je složka `.app`; nový distribuční archiv tento checkpoint nevytváří.
- macOS aplikaci nelze na Windows spustit; její běh na Macu nebyl otestován.

Launchery:

- `C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Windows/Release/WrightTycoon.exe`
- `C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Mac/Release/WrightTycoon.app`

## Nedokončeno: mazání starých dat, savů a logů

Uživatel výslovně potvrdil smazání savů i hráčských nastavení. Prostředí ale odmítlo mazací příkazy s výsledkem `blocked by policy`, včetně samostatného příkazu omezeného na přesnou složku `Builds/TestResults`. Toto omezení nebylo obcházeno. Aktualizace tedy **není čistý reset hráčských dat**. Původní savy a zálohy zůstávají a mohou se načíst i v aktualizované hře.

Pro dokončení resetu lze při zavřené hře ručně odstranit pouze následující konkrétní složky, pokud existují:

| Účel | Přesná složka |
| --- | --- |
| Windows hráčská data | `C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Windows/Release/Player Info` |
| macOS hráčská data vedle aplikace | `C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Mac/Release/Player Info` |
| Případná stará data uvnitř macOS aplikace | `C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Mac/Release/WrightTycoon.app/Player Info` |
| Editorová hráčská data | `C:/Wibe coding projekty/Wright tycoon 0.52/Player Info` |
| Staré zálohy hráčských dat | `C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Backups` |
| Staré Windows zálohy hráčských dat | `C:/Wibe coding projekty/Wright tycoon 0.52/Builds/Windows/Cleared Player Data Backups` |
| Prázdná stará složka záloh | `C:/Wibe coding projekty/Wright tycoon 0.52/Backups/ClearedPlayerData` |
| Původní Unity persistent data a logy této hry | `C:/Users/USER/AppData/LocalLow/DefaultCompany/Wright tycoon` |
| Staré výsledky testů | `C:/Wibe coding projekty/Wright tycoon 0.52/Builds/TestResults` |

Ve složkách `Logs` a `Builds/Mac` zůstaly staré diagnostické soubory; inventura před aktualizací zjistila v `Logs` přibližně 624 MiB. Při ručním úklidu logů zachovat minimálně aktuální protokoly uvedené níže a poslední audit technického úklidu `editmode-20261026-130232.*`, `graphics-isolation-20261026-130232.log`. `Logs/Workflow/technical-cleanup-backup-20261026-125842` obsahuje obnovitelné zdrojové soubory a assety z technického úklidu, nikoliv hráčské savy; tuto aktuální rollback zálohu ponechat.

Pro reset voleb hry existují dva konkrétní klíče registru:

- `HKEY_CURRENT_USER/Software/DefaultCompany/Wright tycoon`
- `HKEY_CURRENT_USER/Software/Unity/UnityEditor/DefaultCompany/Wright tycoon`

Nemazat rodičovské klíče ani sourozenecké produkty. Nemazat celé složky Release, zdrojové Assets, Library ani Unity cache. Po odstranění aktivních dat si hra při spuštění vytvoří výchozí prázdnou strukturu. Odstranění všech uvedených kopií savů znemožní obnovu z těchto místních záloh.

## Aktuální důkazy ověření

- `Logs/Workflow/editmode-20261026-143241.xml`
- `Logs/Workflow/editmode-20261026-143241.log`
- `Logs/Workflow/graphics-isolation-20261026-143241.log`
- `Logs/Workflow/build-checkpoint-all-20261026-143241.log`
- `Logs/Workflow/windows-smoke-20261026-143241.log`
