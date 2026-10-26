# Testing and Launcher Workflow

Use `Scripts/WrightTycoonWorkflow.cmd` as the default test and launcher entry point on Windows. It invokes the PowerShell workflow without requiring a machine-wide execution-policy change, resolves the Unity version from `ProjectSettings/ProjectVersion.txt`, stores logs under `Logs/Workflow`, and stops immediately when a required step fails.

## Daily validation

Run the curated Launcher EditMode suite once after a coherent code change (target 250-400 cases):

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

1. Runs the complete curated Launcher EditMode suite, excluding the NUnit `Extended` category, with graphics on an isolated, non-active Windows desktop.
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

- `-TestSuite Launcher` (default): the curated update gate; newly added tests run unless explicitly categorized `Extended`.
- `-TestSuite Full`: all EditMode tests, including extended matrices and compatibility checks.
- `-Action Test -TestSuite Extended`: only extended checks; cannot be used as the sole gate for a launcher build.
- `-GraphicsTests`: run graphics-enabled tests on a separate hidden Windows desktop, so IMGUI interaction tests execute without stealing focus. Checkpoint and Release enable this automatically. A plain `-Action Test` remains headless unless this switch is supplied.

  Isolation is enforced by `Scripts/IsolatedGraphicsProcess.cs`: Unity and its utility windows belong to a uniquely named desktop that is never activated. The workflow never switches the user's desktop. A process job cleans up descendants on completion, timeout or workflow termination. Isolation failures stop the workflow; there is no visible fallback. Evidence is written to `Logs/Workflow/graphics-isolation-*.log`.

  Verify the isolation helper with `powershell -NoProfile -ExecutionPolicy Bypass -File Scripts/Test-IsolatedGraphicsProcess.ps1`. It creates a real test window on the isolated desktop, checks the input desktop is unchanged, and checks error propagation and timeout cleanup.
- `-SkipTests`: build without repeating the complete Launcher or Full suite that already passed for the same working tree. An Extended-only pass is insufficient.
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

- The normal update gate deliberately excludes broad parameter matrices, old compatibility fixtures, preview geometry, cosmetic formatting, and secondary validations. These are available via Full/Extended rather than deleted merely to reduce the count. See `Docs/TestSuiteScope.md` for scope and the audit baseline.
- The Launcher suite is a risk-based selection, not a promise that all former coverage remains in each update. Do not consolidate unrelated scenarios into a single test or cap the discovered tests to manufacture a lower count.
- Zero executed/passed tests or a non-passing run fails the workflow. An out-of-budget Launcher count produces a review warning, not silent truncation.
- During implementation: open Unity and run relevant EditMode tests.
- Before a normal commit: run `-Action Test`.
- When launchers are explicitly requested: run `-Action Checkpoint -Platform All`.
- Before distributing a release: run `-Action Release -Platform All`.
- A successful macOS build on Windows validates the app package and archive structure, but it does not execute the macOS binary.

Do not manually delete `Library` as a routine troubleshooting step. That discards the most valuable Unity build cache and should be reserved for confirmed cache corruption.
