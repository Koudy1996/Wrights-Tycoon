# Wright Tycoon 0.3 - Free 3D Asset Suggestions (Unity)

This list focuses on **free downloadable 3D assets** with licenses suitable for Unity workflows.

## Engine Assets (3D)

### Inline piston
- Low-poly animated engines (includes normal 4-cylinder / flat 4 / single piston)
- https://sketchfab.com/3d-models/low-poly-animated-engines-243837fb3a40440485c8f8c39b6742ba
- License shown: CC Attribution

### V piston
- Animated Engine V8
- https://sketchfab.com/3d-models/animated-engine-v8-b0dbf778b81e4afba4edf11336e2a099
- License shown: CC Attribution

### Radial piston
- Radial Engine Assembly
- https://sketchfab.com/3d-models/radial-engine-assembly-6aa69c9e022248948c108d6bafe21857
- License shown: CC Attribution

### Turbojet
- Jet Engine / plane engine (low poly)
- https://sketchfab.com/3d-models/jet-engine-plane-engine-cc9f4eb20b5749c89c574b301c67af04
- License shown: CC Attribution

### Low-bypass turbofan
- Engine CFM56-7B
- https://sketchfab.com/3d-models/engine-cfm56-7b-2cf2255cba12432cb144abe5b87390e6
- License shown: CC Attribution

### High-bypass turbofan
- General Electric GE90 Model
- https://sketchfab.com/3d-models/general-electric-ge90-model-3b1f17149aa443e2814ecd7a59bc738c
- License shown: CC Attribution

## Aircraft Assets (3D)

### Cartoon / stylized aircraft visual
- Stylized Airplane
- https://sketchfab.com/3d-models/stylized-airplane-7cf44b40887848379112ad0c9b1baa8e
- License shown: CC Attribution

### Modular aircraft parts (for component-driven customization)
- Aircraft_kit_1.1 (kitbash parts: engines, wheels, interior details, etc.)
- https://sketchfab.com/3d-models/aircraft-kit-11-cc0-789f53a908204d3ba7d2b5fd21090f3a
- License shown: CC Attribution (description mentions CC0 usage intent)

### Unity Asset Store free aircraft pack
- Generic Aircraft Models - Free
- https://assetstore.unity.com/packages/3d/vehicles/air/generic-aircraft-models-free-302789

## Engine Preview Texture Mapping Used By UI

The Engine Designer panel will auto-load textures from Resources (if present):

- `Assets/Resources/WT03/Previews/Engines/inline_piston.png`
- `Assets/Resources/WT03/Previews/Engines/v_piston.png`
- `Assets/Resources/WT03/Previews/Engines/radial_piston.png`
- `Assets/Resources/WT03/Previews/Engines/turbojet.png`
- `Assets/Resources/WT03/Previews/Engines/turbofan_low_bypass.png`
- `Assets/Resources/WT03/Previews/Engines/turbofan_high_bypass.png`

If these textures are missing, the game uses a built-in stylized fallback preview painter.

## Notes

- Confirm each asset's current license on the source page before shipping.
- Keep attribution records for CC BY assets.
- For consistent cartoon look, use flat-shaded materials and reduced texture detail.

## Simple Aircraft Pack Integration (your requested pack)

Asset page:
- https://assetstore.unity.com/packages/3d/vehicles/air/simple-aircraft-pack-288110

How to connect it to preview:
1. Import the package in Unity (`Window -> Package Manager -> My Assets`).
2. Pick 3-8 aircraft prefabs from the package.
3. Place those prefabs under:
- `Assets/Resources/WT03/Previews/AircraftPack/`
4. No code changes needed after this.

Runtime behavior:
- The preview renderer will auto-select best matching prefab by current parameters (jet/prop, single/twin, etc.).
- Current gameplay options still drive tail/engine/gear/wing hints on top of the pack model.
- If no prefab is found in this path, renderer falls back to procedural model.
