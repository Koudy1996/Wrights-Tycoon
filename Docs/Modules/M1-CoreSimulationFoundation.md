# M1 - Core Simulation Foundation

## Architectural overview

`WrightTycoon.Core` is a pure C# domain assembly with `noEngineReferences` enabled, so simulation logic can be tested without Unity runtime dependencies.

The module is split into four isolated domains:

- `WrightTycoon.Core.Math`: numeric guards and reusable math helpers.
- `WrightTycoon.Core.Units`: lightweight value types (`Mass`, `Force`, `Area`, `Density`, `Speed`) for safer formulas.
- `WrightTycoon.Core.Physics`: atmosphere and aerodynamic calculations.
- `WrightTycoon.Core.Simulation`: monthly turn engine and system pipeline.

## Folder structure

```text
Assets/
  WrightTycoon/
    Core/
      WrightTycoon.Core.asmdef
      Math/
      Units/
      Physics/
      Simulation/
    Tests/
      EditMode/
        WrightTycoon.Core.Tests.asmdef
        Physics/
        Simulation/
Docs/
  Modules/
    M1-CoreSimulationFoundation.md
```

## Domain model

- `SimulationDate`: immutable year-month value object with deterministic month stepping.
- `MonthlySimulationEngine<TState>`: deterministic, ordered monthly system executor.
- `MonthlySimulationContext<TState>`: per-month mutable execution context with event logging and scratchpad.
- `IAtmosphereModel`: abstraction for atmosphere implementations.
- `IsaAtmosphereModel`: simplified ISA density model:
  - `rho(h) = rho0 * (1 - 0.0000225577 * h)^4.256`
  - `rho0 = 1.225 kg/m^3`
  - design samples at `0`, `5000`, `10000` meters.
- `IAerodynamicsCalculator` and `AerodynamicsCalculator`:
  - Lift: `L = 0.5 * rho * V^2 * S * Cl`
  - Drag: `D = 0.5 * rho * V^2 * S * Cd`
- `PropulsionScaling`: engine output scaling by air density ratio.

## Interfaces

- `IAtmosphereModel`
- `IAerodynamicsCalculator`
- `IMonthlySimulationSystem<TState>`

These interfaces are intentionally narrow to make later modules (engines, airframes, factories) plug in without tight coupling.

## Implementation notes

- All formulas validate finite and non-negative inputs where physically required.
- `IsaAtmosphereModel` clamps altitude below sea level to `0m` for stable gameplay behavior.
- `MonthlySimulationEngine<TState>`:
  - sorts systems by `ExecutionOrder` then `SystemId`,
  - rejects duplicate IDs,
  - records executed systems and events for each simulated month.

## Example test scenarios

- `IsaAtmosphereModelTests`:
  - validates density at `0m`, `5000m`, `10000m`.
  - verifies monotonic density ratio decrease with altitude.
- `AerodynamicsCalculatorTests`:
  - validates lift and drag against known values.
  - validates propulsion output scaling by density ratio.
- `MonthlySimulationEngineTests`:
  - validates system execution order and month progression.
  - validates deterministic state updates over multiple months.

## Extension points

- Replace `IAtmosphereModel` for weather or custom map atmospheres.
- Add new `IMonthlySimulationSystem<TState>` implementations per subsystem (R&D, contracts, finance, production).
- Extend `Units` with additional types (`Power`, `Torque`, `FuelMass`) without changing existing APIs.
- Use `MonthlySimulationContext<TState>.Scratchpad` to pass transient data between systems in a single month without hard dependencies.
