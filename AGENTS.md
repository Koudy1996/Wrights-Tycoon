# Repository Workflow

The canonical testing and launcher procedure is documented in `Docs/TestingAndLauncherWorkflow.md`.

## Required commands

- Validate a normal code change: `Scripts\WrightTycoonWorkflow.cmd -Action Test`
- Update both launchers: `Scripts\WrightTycoonWorkflow.cmd -Action Checkpoint -Platform All`
- Build distributable launchers: `Scripts\WrightTycoonWorkflow.cmd -Action Release -Platform All`

Use `-SkipTests` only when the complete EditMode suite already passed against the same working tree. Use the clean release workflow only for distribution or confirmed cache problems.

Do not run an empty PlayMode test pass. Do not routinely delete `Library`, build output, or Unity caches. Incremental checkpoints deliberately preserve them. Logs and test XML are written to `Logs/Workflow`.

The Windows smoke test is required when updating the Windows launcher. A macOS build produced on Windows validates package construction but cannot execute the macOS application.
