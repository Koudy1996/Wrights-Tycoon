# Combined 0.6.7 launcher release — 25 October 2026

Reviewed the completed turns in `Verze 0.6.7 2` (thread `01a17376-86ce-7983-83bf-de9bda49f792`). Both chats use this same working directory; no cherry-pick or source copy was necessary. The release includes the current uncommitted and untracked source assets from both chats.

Included changes from that chat: bidirectional production capacity lending; factory capacity 12,600 / 50,400 / 201,600 MD; increased contract margins; removal of legacy maintenance models; warranty-only reputation evaluation; accident comparison with period baseline; remaining-warranty parts forecasts; fixed 50% reference maintenance sales margins; averaged contractual/design retirement life; persisted maintenance revenue and consumed-inventory cost accounting.

Also includes this chat's modular wing profiles, corrected efficiency trade-offs and reference names, research bonus propagation, and weighted family/type technology snapshots.

## Verification and packages

- Canonical command: `Scripts\WrightTycoonWorkflow.cmd -Action Release -Platform All`.
- Headless suite: `Logs/Workflow/editmode-20261025-182112.xml` — 1,001 passed, 54 graphics-dependent skipped, zero failed.
- Additional graphics-enabled full EditMode suite: `Logs/Workflow/release-all-graphics-20261025.xml` — 1,055 passed, zero failed or skipped.
- Build log: `Logs/Workflow/build-release-all-20261025-182112.log`.
- Windows smoke: `Logs/Workflow/windows-smoke-20261025-182112.log` — passed.
- Windows manifest: 2026-10-25 17:22:07 UTC.
- macOS manifest: 2026-10-25 17:22:27 UTC. App and distribution archive constructed successfully; macOS execution not tested on Windows.
- Both builds use Mono because the local IL2CPP C++ compiler / Windows SDK are unavailable. Original Standalone IL2CPP project setting restored after building.

## Player-data reset status

Clean release replaced both platform output directories, including their old runtime data. Verified both release `Player Info/Saves` directories empty. No project-root `Player Info` or app-internal Mac `Player Info` remained at final inspection. Current launchers resolve saves relative to their own build root.

Reset is NOT globally complete: the native removal request for `C:\Users\USER\AppData\LocalLow\DefaultCompany\Wright tycoon` and the two game-specific registry keys was blocked by tool policy. The old AppData `WrightTycoon/saves.json` and game preferences therefore remain; no alternate deletion mechanism was attempted. Historical backups under `Builds/Backups` and `Builds/Windows/Cleared Player Data Backups` were not removed. Source files, Unity Library, and workflow evidence were preserved.
