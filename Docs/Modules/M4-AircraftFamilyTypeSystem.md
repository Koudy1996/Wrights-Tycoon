# M4 - Aircraft Family and Type System

## Architecture

`WrightTycoon.Core.Aircraft` composes M2 + M3 modules:

- Family/type definitions:
  - `AircraftFamilyDefinition` (locked parameters and base prototypes),
  - `AircraftTypeDefinition` (delta changes).
- Delta models:
  - `EngineParameterDelta`,
  - `AirframeParameterDelta`.
- Calculators:
  - `AircraftPerformanceCalculator`,
  - `DevelopmentCostCalculator`.
- Aggregate builder:
  - `AircraftFactory`.

## Family/type rules

- Family locks:
  - engine type,
  - structure type,
  - material type,
  - engine count.
- Type applies deltas to family base parameters.
- `AircraftFactory` applies deltas, then enforces family locks before building engine/airframe.

## Key formulas

### Max speed by altitude (0 / 5000 / 10000 m)

Uses M1 atmosphere density at each altitude.

- Thrust-limited (jet):
  - `Vmax = sqrt((2 * T) / (rho * S * Cd))`
- Power-limited (prop):
  - `Vmax = cbrt((2 * P) / (rho * S * Cd))`

### Range

- Cruise speed is a fraction of `Vmax@5000m`.
- Endurance from fuel mass and cruise fuel flow.
- `Range = cruiseSpeed * endurance`.

### Payload

- Compute available lift at takeoff condition, convert to supported mass, subtract empty mass.

### Reliability and production

- Overall reliability = engine reliability (with multi-engine penalty) * airframe reliability modifier.
- Total production cost/man-days = airframe values + engine values * engine count.

### Development delta cost

Development scales with:

- number of changed parameters,
- normalized magnitude of numeric changes,
- discrete enum changes.

## Extension points

- Introduce family-level allowed ranges for deltas (hard constraints).
- Add per-country industrial modifiers to development and production effort.
- Add payload-role performance models (cargo/passenger/military) on top of the existing aggregate.
