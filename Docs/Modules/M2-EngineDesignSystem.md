# M2 - Engine Design System

## Architecture

`WrightTycoon.Core.Engines` is split into:

- Domain: `EngineDefinition`, `EngineParameters`, `Engine`, `EnginePerformanceReport`, enums.
- Calculator: `EngineCalculator` (`IEngineCalculator`).
- Builder: `EngineBuilder`.
- Data integration: builder can resolve definitions from `IEngineDefinitionRepository`.

This keeps formulas pure/testable and external data JSON-ready.

## Key formulas

### Piston / inline / V / radial

- Sea-level power:
  - `P0 = displacementLiters * basePowerPerLiter * volumetricEfficiency * boostPowerFactor`
- Volumetric efficiency:
  - `VE = clamp(baseVE * compressionFactor * coolingFactor, 0.55, 1.4)`
- Altitude scaling:
  - `P(h) = P0 * densityRatio(h) * optimizationFactor(h)`

### Jet / turbojet / turbofan

- Sea-level thrust:
  - `T0 = baseJetThrustPerSquareMeter * turbineDiameter^2 * bypassModifier * coolingFactor * boostFactor`
- Turbofan bypass:
  - `bypassModifier = 1 + bypassRatio * 0.08`
- Altitude scaling:
  - `T(h) = T0 * densityRatio(h) * optimizationFactor(h)`

### Fuel, reliability, weight

- Fuel flow increases with compression and boost; turbofan high bypass improves fuel efficiency.
- Reliability:
  - starts from `baseReliability`,
  - minus compression penalty,
  - minus boost penalty,
  - minus complexity penalty (`jet > piston`),
  - plus cooling bonus.
- Weight:
  - piston family scales with displacement,
  - jet family scales with turbine diameter squared.

## Balancing decisions

- Boost gives visible performance gains but clear reliability/fuel penalties.
- Turbofans are powerful but expensive/heavy with higher complexity penalties.
- Optimization altitude allows role specialization without adding unstable Mach/compressibility simulation.

## Extension points

- Add new engine archetypes by extending `EngineType` and `EngineCalculator` type mapping.
- Move constants into data definitions for full moddable balance packs.
- Add engine wear state by composing reliability with monthly degradation in M5+ simulation systems.
