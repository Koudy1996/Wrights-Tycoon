# Simulation Tuning Quick Guide

Main v0.2 balance files:

- `Assets/Modules/M1.Core/balance/CoreBalance.cs` (ISA / atmosphere)
- `Assets/Modules/M2.EngineDesigner/balance/EngineDesignerBalance.cs` (M2 engines)
- `Assets/Modules/M3.PlaneDesigner/balance/PlaneDesignerBalance.cs` (M3 airframes/aircraft/categories)
- `Assets/Modules/M5.Factory/balance/FactoryBalance.cs` (M5 factory throughput)
- `Assets/Modules/M6.Contracts/balance/ContractBalance.cs` (M6 contract generation)

Legacy compatibility file still exists for UI binding:

- `Assets/Modules/M1.Core/balance/LegacyBalance/WrightTycoonTuning.cs`

## What You Can Tune Quickly

- Atmosphere model constants
  - `CoreBalance.Atmosphere`
- Engine limits and key modifiers
  - `EngineDesignerBalance.Engine`
- Airframe weight scaling (including big-wing/big-plane mass growth)
  - `PlaneDesignerBalance.Airframe`
- Core aircraft performance coefficients
  - `PlaneDesignerBalance.Performance`
- Category requirements and modifiers (contracts/compliance/game balance)
  - `PlaneDesignerBalance.CategoryEntries`

## Common Edits

Increase weight growth for big planes:

1. Open `PlaneDesignerBalance.Airframe`
2. Increase:
   - `WingMassExponent`
   - `WingMassCoefficient`
   - `WingReinforcementMassBaseShare`

Change Narrow-body requirements:

1. Open `PlaneDesignerBalance.CategoryEntries`
2. Find entry:
   - `AircraftCategory.MediumTransportNarrowBody`
3. Edit:
   - `MinMaximumWeightKilograms`, `MaxMaximumWeightKilograms`
   - speed/range/payload limits
   - `WeightModifierKilograms`, `FuelConsumptionMultiplier`, etc.

After edits, recompile in Unity (or run any existing build/test pass).
