# Repository Workflow

The canonical testing and launcher procedure is documented in `Docs/TestingAndLauncherWorkflow.md`.

## Required commands

- Validate a normal code change: `Scripts\WrightTycoonWorkflow.cmd -Action Test`
- Update both launchers: `Scripts\WrightTycoonWorkflow.cmd -Action Checkpoint -Platform All`
- Build distributable launchers: `Scripts\WrightTycoonWorkflow.cmd -Action Release -Platform All`

The default `Launcher` EditMode suite is the curated 250-400 case update gate. Extended matrices, legacy compatibility and cosmetic checks carry the NUnit `Extended` category and do not run during normal launcher updates. New unclassified tests run by default; review their scope rather than dropping them automatically.

Use `-TestSuite Full` for the exhaustive audit, `-Action Test -TestSuite Extended` for extended checks alone, and `-GraphicsTests` to execute graphics-dependent UI tests instead of skipping them. Extended alone cannot authorize a launcher build.

Use `-SkipTests` only when the complete Launcher (or Full) suite already passed against the same working tree. Use the clean release workflow only for distribution or confirmed cache problems.

Do not run an empty PlayMode test pass. Do not routinely delete `Library`, build output, or Unity caches. Incremental checkpoints deliberately preserve them. Logs and test XML are written to `Logs/Workflow`.

The Windows smoke test is required when updating the Windows launcher. A macOS build produced on Windows validates package construction but cannot execute the macOS application.

## Non-disruptive testing

Run normal code checks headlessly by default. Launcher checkpoints/releases automatically run graphics tests on a separate, non-active Windows desktop. Explicit `-GraphicsTests` uses the same isolation. Never open test windows on the user's active desktop: Unity utility windows can steal focus even in batch mode. If desktop isolation fails, abort; do not fall back to visible testing or silently omit required graphics coverage. Do not call SwitchDesktop or move the user's session. Windows launcher smoke tests remain non-interactive/headless. Validate isolation changes with `Scripts/Test-IsolatedGraphicsProcess.ps1`.
