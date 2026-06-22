# Unity Launch and Build Flow

This repo is a Unity project. Use `Scripts/WrightTycoonWorkflow.cmd` for repeatable tests and launcher builds on Windows. The Unity editor menus remain available for interactive use.

## 1. Open the project

- Open the project folder in Unity Hub with the Unity editor version from `ProjectSettings/ProjectVersion.txt`.

## 2. Prepare and run in the editor

- Run `WrightTycoon -> Prepare Project For Launch`.
- Run `WrightTycoon -> Open Launch Scene`.
- Press Play.

`Prepare Project For Launch` keeps existing launch scenes, copies definition JSON files only when their content changed, and ensures the launch scene is in Build Settings.

## 3. Build launchers

- Use `WrightTycoon -> Build -> Incremental -> All Launchers` for a normal launcher checkpoint.
- Use `WrightTycoon -> Build -> Clean Release -> All Launchers` before distribution.

The build pipeline is the single build authority. It prepares the project, writes `WrightTycoon.build.json`, creates a release-side `Player Info` folder, and moves Windows developer-only backup payloads out of the normal `Release` folder. Incremental builds preserve caches and do not create the macOS distribution archive. See `Docs/TestingAndLauncherWorkflow.md` for the default command-line workflow.

## 4. Runtime data

- The game stores runtime data in `Player Info`.
- Subfolders:
  - `Player Info/Saves`
  - `Player Info/Generated Assets`
  - `Player Info/Logs`
- In the editor, `Player Info` is created in the project root.
- In builds, `Player Info` is created next to the executable/build root.
- It is safe to delete. The game recreates it automatically.

## 5. Release layout

- Windows release: `Builds/Windows/Release`
- Windows developer-only artifacts: `Builds/Windows/Developer`
- macOS release: `Builds/Mac/Release`

## Notes

- Domain modules are organized under `Assets/Modules/M1.Core` through `Assets/Modules/M9.GovernmentContracts`.
- The Unity-facing launch/build layer lives in `Assets/Modules/M8.UI/Game`.
- The launch scene asset stays at `Assets/WrightTycoon/Scenes/WrightTycoonLaunch.unity` for compatibility, but it is managed by the M8 UI pipeline.
