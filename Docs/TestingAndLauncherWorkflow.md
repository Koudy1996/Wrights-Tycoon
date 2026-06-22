# Testing and Launcher Workflow

Use `Scripts/WrightTycoonWorkflow.cmd` as the default test and launcher entry point on Windows. It invokes the PowerShell workflow without requiring a machine-wide execution-policy change, resolves the Unity version from `ProjectSettings/ProjectVersion.txt`, stores logs under `Logs/Workflow`, and stops immediately when a required step fails.

## Daily validation

Run the complete EditMode suite once after a coherent code change:

```powershell
.\Scripts\WrightTycoonWorkflow.cmd -Action Test
```

The project currently has no PlayMode tests. Do not start a separate PlayMode Unity process until such tests exist.

During active development, keep the Unity editor open and use its Test Runner for repeated or filtered checks. Test execution is much faster than starting a new batch-mode editor.

## Launcher checkpoint

Use an incremental checkpoint when launchers need to reflect the current code:

```powershell
.\Scripts\WrightTycoonWorkflow.cmd -Action Checkpoint -Platform All
```

This workflow:

1. Runs the complete EditMode suite.
2. Builds Windows and macOS in one Unity process.
3. Reuses Unity and player build caches.
4. Validates packaged historical campaign assets.
5. Runs the Windows launcher until campaign data and the launch runner report readiness.

The incremental macOS build updates `Builds/Mac/Release/WrightTycoon.app` but deliberately removes any older distribution archive so it cannot be mistaken for the current build.

When a previous player exists and all non-C# inputs are unchanged, the pipeline uses Unity's scripts-only build. The input fingerprint covers `Assets` except `.cs` contents, `Packages`, and `ProjectSettings`. Any asset or configuration change forces a normal incremental build. If Unity rejects a scripts-only build, the pipeline automatically retries with the normal incremental mode.

Build only one platform when appropriate:

```powershell
.\Scripts\WrightTycoonWorkflow.cmd -Action Checkpoint -Platform Windows
.\Scripts\WrightTycoonWorkflow.cmd -Action Checkpoint -Platform Mac
```

## Clean release

Use a clean release before distributing launcher files or when build caches may be invalid:

```powershell
.\Scripts\WrightTycoonWorkflow.cmd -Action Release -Platform All
```

Clean release mode deletes the previous platform output, requests `BuildOptions.CleanBuildCache`, and creates `Builds/Mac/WrightTycoon-macOS-Release.tar.gz`. It is intentionally slower and should not be used for every code iteration.

## Useful options

- `-SkipTests`: build without repeating a test suite that already passed for the same working tree.
- `-SkipSmoke`: skip the Windows built-player readiness check.
- `-UnityPath <path>`: use an editor outside the standard Unity Hub installation path.

Examples:

```powershell
.\Scripts\WrightTycoonWorkflow.cmd -Action Checkpoint -Platform All -SkipTests
.\Scripts\WrightTycoonWorkflow.cmd -Action Release -Platform Mac -SkipSmoke
```

## Unity editor menus

The equivalent editor entries are grouped under:

- `WrightTycoon -> Build -> Incremental`
- `WrightTycoon -> Build -> Clean Release`

Prefer `All Launchers` when both platforms are required. It prepares the project once and avoids a second Unity startup.

## Workflow policy

- During implementation: open Unity and run relevant EditMode tests.
- Before a normal commit: run `-Action Test`.
- When launchers are explicitly requested: run `-Action Checkpoint -Platform All`.
- Before distributing a release: run `-Action Release -Platform All`.
- A successful macOS build on Windows validates the app package and archive structure, but it does not execute the macOS binary.

Do not manually delete `Library` as a routine troubleshooting step. That discards the most valuable Unity build cache and should be reserved for confirmed cache corruption.
