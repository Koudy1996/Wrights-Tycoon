# Wright Tycoon v0.2 Architecture

## Module Layout (`Assets/Modules`)

- `M1.Core`
  - Pure domain fundamentals: ISA atmosphere, physics utilities, units, deterministic monthly simulation, shared data loading.
  - Balance: `M1.Core/balance/CoreBalance.cs`.
- `M2.EngineDesigner`
  - Engine domain model and calculations (piston + jet).
  - Balance: `M2.EngineDesigner/balance/EngineDesignerBalance.cs`.
- `M3.PlaneDesigner`
  - Airframe, family/type aggregate, aircraft performance, category compliance.
  - Balance: `M3.PlaneDesigner/balance/PlaneDesignerBalance.cs`.
- `M5.Factory`
  - Monthly production simulation and warehouse.
  - Balance: `M5.Factory/balance/FactoryBalance.cs`.
- `M6.Contracts`
  - Contract generation/evaluation/delivery.
  - Balance: `M6.Contracts/balance/ContractBalance.cs`.
- `M8.UI`
  - Unity-facing launch/editor UI code (presentation layer only).

## Dependency Direction

- `M8.UI` -> domain modules (`M1/M2/M3/M5/M6`) -> module `balance` data.
- Domain code remains Unity-free and deterministic.

## Unified Engine Thrust

- Jets output thrust directly.
- Pistons now also compute thrust:
  - `T = (P * eta_prop) / V_ref`
  - where `P` is piston power in watts, `eta_prop` is propulsive efficiency, `V_ref` is a tuned reference speed.
- This allows one shared aircraft speed formula for both engine branches.

## Aircraft Performance Core Formulas

- Max speed at altitude:
  - `Vmax = sqrt(2T / (rho * S * Cd))`
- Cruise speed (lift equals max weight):
  - `L = 0.5 * rho * V^2 * S * Cl`
- Maximum takeoff weight:
  - `m_max = sqrt((s * rho * S * Cl_max * T) / g)`
- Useful weight:
  - `Useful = MaximumWeight - WetWeight`
- Range:
  - `Range = (Fuel / CruiseBurn) * CruiseSpeed`

## v0.2 Rule Change

- Aircraft type development `cost` and `MD` are intentionally ignored in gameplay calculations (`DevelopmentCostCalculator` returns 0 cost and 0 MD).

