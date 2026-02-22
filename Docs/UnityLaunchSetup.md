# Unity Pairing and Launch Setup

This repo is now prepared as a Unity project folder (has `Assets`, `Packages`, `ProjectSettings`).

## 1. Confirm Unity Editor install

Unity Hub alone is not enough. You need a Unity Editor version installed (Unity 6 LTS recommended).

## 2. Open the project

- Open Unity Hub.
- Add project folder:
  - `C:\Wibe coding projekty\Wright tycoon`
- Open it with your Unity 6 LTS editor.

## 3. Auto-prepare launch scene

In Unity Editor menu:

- `WrightTycoon -> Prepare Project For Launch`

This will:

- create `Assets/WrightTycoon/Scenes/WrightTycoonLaunch.unity`,
- add it to Build Settings,
- ensure JSON definitions are available in `Assets/StreamingAssets/Data/Examples`.

## 4. Run in Editor

- Open `Assets/WrightTycoon/Scenes/WrightTycoonLaunch.unity`.
- Press Play.
- A launch report (single vs twin engine comparison) appears on screen and in Console.

## 5. Build executable (Windows)

- `File -> Build Settings`
- Platform: `Windows`
- Scene list should include `Assets/WrightTycoon/Scenes/WrightTycoonLaunch.unity`
- Click Build.

## Notes

- Domain modules are in `Assets/Modules/M1.Core`, `Assets/Modules/M2.EngineDesigner`, `Assets/Modules/M3.PlaneDesigner`, `Assets/Modules/M5.Factory`, `Assets/Modules/M6.Contracts`.
- Unity-facing launch layer is in `Assets/Modules/M8.UI/Game`.
