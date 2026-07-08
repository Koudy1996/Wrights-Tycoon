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
  - `T0 = baseJetThrustPerSquareMeter * turbineDiameter^2 * bypassThrustFactor * coolingFactor * boostFactor`
- Turbofan bypass:
  - BPR 1:1 is the baseline.
  - Thrust and SFC use log interpolation over calibrated BPR correction nodes.
  - Thrust correction nodes plateau toward ultra-high-bypass ducted engines:
    - `0.5: -4%`, `1: 0%`, `5: +14%`, `10: +21%`, `15: +24%`, `20: +25%`.
  - SFC correction nodes model stronger fuel efficiency gains with diminishing returns:
    - `0.5: +5%`, `1: 0%`, `5: -11%`, `10: -17%`, `15: -19%`, `20: -20%`.
  - BPR is intentionally not counted again in jet altitude retention; altitude scaling uses compression/cooling and engine family effects.
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
