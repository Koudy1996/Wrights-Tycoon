# Wright Tycoon Documentation (User + Technical)

This document is intentionally short. For deeper internal notes, see the rest of the `Docs/` folder.

## User Guide

### Launch

- Windows: run `Builds/Windows/Release/WrightTycoon.exe`
- macOS: open `Builds/Mac/Release/WrightTycoon.app`

### Main Menu

- **Continue**: loads the most recently played save (only enabled if a save exists).
- **New Game**: starts fresh with default definitions.
- **Load Game**: pick a manual save or autosave from a list.
- **Options**: resolution selection and apply.

### In-Game UI Basics

The game is a UI-driven prototype. Most interaction is mouse-based, with text fields and buttons.

Top bar:

- **Date** and **Company**
- **Next Month**: advances simulation by 1 month. This processes research, factory production/costs, and generates new contracts.
- **Save and Exit**: creates a manual save, then returns to the main menu.
- **Cash**: current cash balance

Tabs (left panel = inputs/actions, right panel = report/preview):

- **Engine Designer**: edit parameters, preview stats, and **Save Engine** for use in aircraft designs.
- **Airframe + Type Designer**: configure a family/type (airframe parameters, category compliance, etc.), then **Save Family** and **Save Aircraft Design**.
- **Research**: monthly research progression and approvals.
- **Factory**: assign saved aircraft designs to production lines and run month-by-month production; outputs go to warehouse stock.
- **Contracts**: contracts are generated when you press **Next Month**; select how to fulfill them (typically from warehouse stock) and deliver.
- **Government Contracts**: separate contract flow for government inventory/delivery.
- **Finance**: summary of income/expenses and per-month details once months have been processed.

### Saves, Logs, and “Player Info”

At runtime, Wright Tycoon creates a `Player Info/` folder:

- In the Unity Editor: created in the project root.
- In builds: created next to the executable (Windows) or next to the `.app` bundle (macOS).

Key subfolders:

- `Player Info/Saves/` (save database file `saves.json`)
- `Player Info/Logs/` (session logs `wrighttycoon-YYYYMMDD-HHMMSS.log`)
- `Player Info/Generated Assets/` (generated runtime artifacts)

It is safe to delete `Player Info/`. The game recreates it automatically (deleting it resets saves and rebuilds generated outputs).

### Troubleshooting

- If the game fails to load or behaves strangely after changes: delete `Player Info/` and relaunch.
- For errors, check the newest log file in `Player Info/Logs/`.

## Technical Guide (For Developers)

### Requirements

- Unity version: `6000.3.9f1` (from `ProjectSettings/ProjectVersion.txt`)
- Packages of note (from `Packages/manifest.json`): `com.unity.inputsystem`, `com.unity.test-framework`, `com.unity.nuget.newtonsoft-json`

### Editor Run Flow (Supported Path)

Unity editor menus are the supported launch entry points:

1. Open this project in Unity Hub with the correct Unity version.
2. Run `WrightTycoon -> Prepare Project For Launch`.
3. Run `WrightTycoon -> Open Launch Scene`.
4. Press Play.

What “Prepare Project For Launch” does (high level):

- refreshes example definition JSON files in `Assets/StreamingAssets/Data/Examples`
- ensures the launch scene is in Build Settings

### Build Releases (Supported Path)

Use the build pipeline menu (single build authority):

- `WrightTycoon -> Build -> Windows Release` -> outputs to `Builds/Windows/Release`
- `WrightTycoon -> Build -> macOS Release` -> outputs to `Builds/Mac/Release`

The pipeline prepares the project, writes `WrightTycoon.build.json`, and creates a build-side `Player Info/` layout.

### Project Structure (High Level)

- Domain modules (Unity-free, deterministic): `Assets/Modules/M1.Core`, `M2.EngineDesigner`, `M3.PlaneDesigner`, `M5.Factory`, `M6.Contracts`, etc.
- Unity-facing UI/bootstrap layer: `Assets/Modules/M8.UI/Game`

Dependency direction:

- `M8.UI` depends on domain modules.
- Domain modules do not depend on Unity.

Entry scenes:

- Editor/compat launch scene: `Assets/WrightTycoon/Scenes/WrightTycoonLaunch.unity`
- Build launch scene: `Assets/Modules/M8.UI/Game/Scenes/WrightTycoonBuildLaunch.unity`

Example definition data (used by launch flow and loading):

- `Assets/StreamingAssets/Data/Examples/engine-definitions.json`
- `Assets/StreamingAssets/Data/Examples/airframe-definitions.json`

### Tests

EditMode tests are under `Assets/WrightTycoon/Tests/EditMode/` and can be run via Unity’s Test Runner.

### Tuning and Balance

Main balance files:

- `Assets/Modules/M1.Core/balance/CoreBalance.cs`
- `Assets/Modules/M2.EngineDesigner/balance/EngineDesignerBalance.cs`
- `Assets/Modules/M3.PlaneDesigner/balance/PlaneDesignerBalance.cs`
- `Assets/Modules/M5.Factory/balance/FactoryBalance.cs`
- `Assets/Modules/M6.Contracts/balance/ContractBalance.cs`

Legacy compatibility binding (UI-facing):

- `Assets/Modules/M1.Core/balance/LegacyBalance/WrightTycoonTuning.cs`
