# M3 - Airframe Design System

## Architecture

`WrightTycoon.Core.Airframes` is split into:

- Domain: `AirframeDefinition`, `AirframeParameters`, `Airframe`, `AirframePerformanceReport`, enums.
- Calculator: `AirframeCalculator` (`IAirframeCalculator`).
- Builder: `AirframeBuilder`.
- Data integration: builder can resolve definitions from `IAirframeDefinitionRepository`.

## Key formulas

- Lift:
  - `L = 0.5 * rho * V^2 * S * Cl`
- Drag:
  - `D = 0.5 * rho * V^2 * S * Cd`

`AirframeCalculator` evaluates reference lift/drag at sea level and `100 m/s` so wing area directly affects both forces through `S`.

- `Cd` combines base profile drag + wing/gear/flap/tail/cowling deltas.
- `Cl` combines base lift + wing position/shape + flap deltas.
- Maneuverability is a weighted score from wing loading + geometry/control bonuses, clamped to `1..100`.

## Balancing decisions

- Composite material lowers mass but increases production cost.
- Retractable gear improves drag but costs more and slightly reduces reliability.
- Cowling slider is an explicit cooling-vs-aero tradeoff:
  - high cowling improves drag,
  - low cowling improves reliability modifier.

## Extension points

- Replace static factor tables with ScriptableObject/JSON-driven rule sets.
- Add optional stability/takeoff metrics without changing existing report contracts.
- Add mission-role modifiers (cargo, trainer, bomber) as additional cost/reliability multipliers.
