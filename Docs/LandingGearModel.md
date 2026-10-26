# Landing gear model

Implemented as a whole-aircraft component because sizing needs the installed engine mass. AirframePerformanceReport supplies the neutral reference structure and frozen technology indices; AircraftPerformanceReport.LandingGear owns the complete gear breakdown. Airframe-only mass and fuselage geometry intentionally exclude the separately installed gear.

## Reference and mass

`E_ref` consists of neutral structure, wings, wing systems, engine installation, engine-associated systems, actual frozen engines, and the base/per-engine aircraft systems. It excludes physical crew, crew-dependent systems, fuel and fuel-dependent support/tanks, gear, calendar airframe technology, precision, durability and speed mass scaling. Structure/material/construction/wing-profile choices still affect reference sizing. No selected runway, current fuel quantity, category payload minimum or calculated MTOW feeds back into sizing.

`M_ref = 1.60 * E_ref`. This is a game sizing proxy, not a certified load rating or displayed MTOW.

`gearMass = M_ref * fraction * precisionWeightMultiplier * (1 + .02 * durability) / systemsTechnology`.

| Type | Mass fraction | Cruise drag-area coefficient at 1,000 kg | Existing materials surcharge | Existing work surcharge | Existing reliability multiplier |
| --- | ---: | ---: | ---: | ---: | ---: |
| Fixed | .032 | .050 m² | 0 | 0 | 1.03 |
| SemiRetractable | .036 | .025 m² | .05 | .08 | 1.01 |
| Retractable | .042 | .004 m² | .12 | .18 | .97 |

Systems technology uses the existing 20% family / 80% type structural index. Gear is added AFTER the aircraft speed-weight multiplier, without another material/precision/technology multiplier. Other existing mass channels retain their formulas. Every option now has explicit nonzero gear mass; compared with the former model this can reduce payload even for fixed gear.

## Drag and production

`gearDragArea = coefficient * (M_ref / 1000)^(2/3) * cleanlinessDragFactor * precisionDragMultiplier / bodyTechnology`.

Body technology uses the existing 50% family / 50% type aerodynamic index. Cruise uses the selected coefficient. Extended drag area always uses .050, including retractable gear. The latter is exposed for future takeoff/landing calculations; the existing idealized takeoff calculation does not yet use drag.

In modular aircraft, gear area is added to body drag area, outside the wing overspeed/delta-wing corrections. Legacy coefficient calculations receive the equivalent gear area divided by their wing reference area, also outside their wing corrections. Removed the former fixed body coefficient gear deltas (.025/.010/.003), plus unused fixed-dollar and fixed-MD variables. No duplicate production surcharge was added: existing airframe addon rates, technology and research calculations remain in place; engines are not repriced by the gear component.

Fuselage geometry is calculated before adding gear mass, so gear does not make the fuselage artificially larger. Existing delivered-fleet terms remain frozen; no fleet/service snapshot rewrite was added. The selected RunwayType and previously diagnosed takeoff-limit clamp are NOT changed by this feature.

## Presentation and verification

Designer shows gear mass, additional mass over the fixed alternative, cruise/extended drag areas, and material/work surcharges. The empty-weight tooltip accounts for gear as a separate included-once contribution; selector tooltips mention weight tradeoffs.

Rebuilt the January 1946 C1/V8-16 draft from its save, without writing to the save:

| Gear | Gear kg | Empty aircraft kg | Payload kg (500 m, 200 L) | Maximum km/h |
| --- | ---: | ---: | ---: | ---: |
| Fixed | 184.44 | 3985.64 | 1283.56 | 369.82 |
| SemiRetractable | 207.50 | 4011.22 | 1256.71 | 374.27 |
| Retractable | 242.08 | 4047.91 | 1218.18 | 378.00 |

The gear-only retractable/fixed mass difference is 57.64 kg; total empty-weight difference also includes the pre-existing aircraft speed-weight scaling on the rest of the aircraft.

- Normal workflow: 372 passed, 28 graphics-dependent skipped, 400 total (`editmode-20261025-193918.xml`).
- Final Full graphics workflow: 1,053 passed, zero failed/skipped (`editmode-20261025-194500.xml`), including gear UI layout/repaint at 1280 and 1920 widths and the legacy drag separation. Exactly 400 cases belong to the normal Launcher gate.
- Main new tests cover exact coefficients, single application of modifiers, real aircraft mass integration and runway/fuel/crew independence. Extended tests cover JSON/history of technology and invalid/overflow inputs.
- No Windows/macOS launcher build, player-data deletion or backend setting change was performed.
