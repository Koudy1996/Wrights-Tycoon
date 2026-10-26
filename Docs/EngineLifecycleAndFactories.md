# Engine lifecycle and shared parts (0.6.7)

## Single maintenance system (2026-10-25)

The only supported maintenance model is separate airframe parts, shared engine
parts and exact-version replacement engines. Unreleased models 0 and 1 have been
removed from simulation, production, inventory valuation, forecasts and UI. There
are no service kits, whole-aircraft spare parts, exchange pools or overhaul queues.
Maintenance-version metadata remains fixed at 2; unsupported offers are rejected
instead of being reinterpreted or migrated. Existing model-2 delivery terms,
physical stocks, paid work and installed-engine clocks retain their meaning.
All lifecycle-enabled contract comparisons use the mean of required and actual
design life, with hourly costs including both airframe and engine maintenance.


## Warranty reputation and maintenance accounting (2026-10-25)

Warranty dates are reputation windows only: no production privilege, termination
compensation, support lockout or customer financial-risk charge. A month with no
maintenance due earns no warranty points. Unique aircraft successfully maintained
inside warranty contribute +0.25/N; missed maintenance contributes -3/N, where N
is the whole eligible active fleet at month start, including grounded aircraft.
If an aircraft has both success and failure during a month, failure takes precedence.

Safety reputation compares all actual losses with an independent period/category/
profile baseline over actual flight exposure. A rolling 24-month comparison and
five baseline pseudo-losses smooth small samples. Below-baseline losses earn a
positive adjustment, above-baseline losses a negative one; zero exposure earns none.

Operating-life baseline is (contract minimum + frozen design life)/2. Retirement
bins span 90–110% of that mean; the same mean governs excess-life accident risk.
The remaining-warranty UI estimates physical airframe parts, engine parts and
exact-version replacements, including debt, clipped to retirement and assuming
full planned flying and available maintenance, without future accidents/deliveries.

Maintenance revenue and realized profit are stored by aircraft design and month.
Profit deducts the persisted weighted manufacturing cost of consumed stock, including
allocated actual factory/equipment overhead. Producing reserve stock does not itself
create a maintenance sale; cash manufacture spending is booked once by factories.
Idle-capacity overhead stays in period factory costs. Old stock without cost history
receives a one-time opening estimate; new batches preserve their actual cost basis.
History starts with the first processed month after this update, without inventing
past sales or costs. Financial history, stock basis and lifetime policy survive saves.

## Compatibility

All approved aircraft types in the same family share one airframe-parts pool,
regardless of similarity. The first registered anchor still determines the part
recipe and new-offer reference price. Historical split groups merge on refresh;
their old IDs remain aliases, completed stock is transferred exactly once, and
reserve targets are added. Existing delivered quantities/prices and paid work
recipes are not rewritten. Equipment for any approved member can produce the
family's parts, using the anchor recipe for new work. Legacy whole-aircraft parts
retain their model-one rules.

Engine lineages retain the every-pair 10% compatibility rule, preventing cumulative
version drift. Group IDs are persisted anchor design IDs; approved members are
retained rather than deleted. Aircraft similarity remains informational; operational
equipment still contributes at most 3 percentage points to that comparison.
Sharing parts does not authorize substituting an entire engine version.

## Maintenance model 2

New civil contracts and government submissions freeze separate airframe and engine
terms. Airframe parts require 1% of the group anchor's airframe materials/work;
engine parts require 1% of the engine anchor's materials/work. Their mandatory selling prices target a 50% margin over reference Small/HandBuilt
manufacturing cost, including allocated monthly building/equipment overhead at full
utilization. Family/type/engine production research is excluded from that reference.
Whole parts consumed per routine cycle are `1 + ceil(10 * max(0, 1 - reliability))`, with
floating-point boundary protection. Unreleased maintenance models 0 and 1 are no longer supported.

New engines select routine maintenance every 200/500/1000/2000 hours, default 500,
and Durability 0–10, default 0. Each durability point adds 2% reference mass,
3.5% material cost and production MD, and 8% raw engine life. It adds no direct
frontal-area/drag or output bonus. Engine-family similarity includes durability
as a structural component; changing the service schedule alone does not change parts.

Replacement life is `roundTo100(4000 * (0.5 + 0.5 * reliability)
* clamp((500 / interval)^0.2, 0.65, 1.2) * (1 + 0.08 * durability) / stress)`,
minimum 2000 hours. Stress is capped at 2 and includes output above reference
loading, RPM, compression and jet afterburner. Technological power increases are
not automatically treated as longer life. Parts per routine cycle are
`1 + ceil(10 * max(0, 1 - reliability))` (epsilon guards floating-point boundaries).
Since 2026-10-23 this quantity is independent of the service interval, for both
engines and airframes. Longer intervals reduce the frequency of parts consumption;
their effect on replacement life remains unchanged.
Effective reliability can exceed 100% through design/research/company bonuses.
It is a rating, not a probability. Repair/downtime failure fractions stop at zero,
routine service still consumes at least one part, and the existing lifetime-factor
and accident-risk limits remain in force. Saved frozen ratings above 100% remain readable.
Delivered schedules, quantities and installed identities remain frozen. Selling
prices are repriced to the mandatory policy when loaded; retirement endpoints are
migrated once to the mean-life policy without resetting flight or motor hours.
Replacement lives need not be exact multiples of routine intervals.
At replacement, all installed engines on each serviced aircraft require exact-version
physical stock. The engine-hour clock resets; aircraft hours and structural wear do
not. Routine engine service at the replacement threshold is skipped. Replacement
price is twice its unresearched Small/HandBuilt reference manufacturing cost,
including allocated overhead (50% selling margin). No overhaul, returned-core or
used-engine resale loop is introduced. Shortages ground unfulfilled aircraft;
partial servicing cannot consume a fraction of an aircraft's installed-engine set.

Ownership quotes include airframe parts, routine engine parts and subsequent engine
replacements. Original engines are included once in the aircraft purchase price.
No maintenance/replacement is quoted at the exact end of the priced ownership period.
Civil eligibility, its comparison UI and delivered shared-group tariffs use the
same canonical offer source. Government submissions persist their complete offer.

## Contract/Demand UI correction (2026-10-20)

New lifecycle-version-2 civil and government requests expose **Operational Costs
per flight hour**. This is purchase depreciation over the offered aircraft's actual
expected life plus fuel, airframe/engine parts and subsequent engine replacements.
The hourly metric excludes separate repair, downtime and support-risk allowances.
Eligibility and state bonuses compare actual $/h with the request's hourly cap;
the persisted `MaximumOperatingCostPerUnit` remains a reference total whose divisor
is `MinimumExpectedLifeHours`. It must not be compared directly with a quote total
covering a different aircraft life. Version 0/1 saved contracts keep their original
rules; version-1 UI explicitly identifies the legacy total-cost equivalent.

Live v2 civil eligibility/comparison uses the current monthly aviation-fuel quote;
government submission metrics capture that price and accepted quotations remain
frozen. Reference budgets are stable category estimates (70% airframe, 30% installed
engines, two engines, 80% reliability), not historical fuel-price forecasts.

Demand and its pinned notebook show the same lifecycle policy used by generation:
minimum life, selectable minimum service intervals, contract support durations,
utilization, operating-cost reference and purchase/operations preference mix.
Year availability and three-year performance forecasts remain distinct from fixed
lifecycle policy and uncertain future fuel prices.

## Service interval requirement correction (2026-10-21)

Civil and government service-interval requirements mean the MINIMUM time between
services: an aircraft requiring service every 500 hours satisfies a 200-hour
requirement, while a 200-hour aircraft fails a 500-hour requirement. Contract cards,
notebooks and Demand now show >= / minimum. Expected-life and operating-cost
checks remain independent. The legacy serialized MaximumServiceIntervalHours
field is retained for save compatibility; MinimumServiceIntervalHours exposes its
correct meaning in live eligibility and UI. Existing open saved offers therefore
work without rewriting saves. Already accepted government bids are not rejected
retroactively by the corrected interval comparison during final evaluation; other
compliance checks and frozen economics still apply.

Factory sub-tabs are above the left scroll view. Each tab draws only its own
content. Aircraft and motor construction buttons are at the top of their respective
panels, work even before the first plant exists, and explain insufficient funds.
New plants start Small; existing building-size expansion rules remain unchanged.

Verification of this correction: full Windows checkpoint on 2026-10-20 passed
593/593 EditMode tests, packaged the launcher, and passed the built-player startup
smoke test. Logs: `Logs/Workflow/editmode-20261020-120023.xml`,
`build-checkpoint-windows-20261020-120023.log`,
`windows-smoke-20261020-120023.log`. SHA-256 of the Windows save file was unchanged.
This is automated verification; no interactive visual QA sign-off is implied.

## Motor factories

| Building | Physical MD/month | Monthly property upkeep |
| --- | ---: | ---: |
| Small | 16,800 | $300,000 |
| Medium | 67,200 | $900,000 |
| Large | 268,800 | $2,700,000 |

A Small plant costs $40m and takes 18 months. Expansion costs $80m / 12 months
to Medium and $240m / 18 months to Large, pausing production while expanding.
Motor buildings now have the same physical capacity as aircraft buildings;
their existing half-price construction and property upkeep remain unchanged.
All five existing automation tiers retain their equipment costs and installation
times. Motor work multipliers are 1 / .88 / .74 / .59 / .45; aircraft work remains
1 / .90 / .78 / .65 / .50. Automation reduces MD
per unit; it does not enlarge the building. Workforce limits cap usable MD.

### Shared size / automation balance (2026-10-23)

Small / Medium / Large provide 16,800 / 67,200 / 268,800 physical MD/month,
respectively (1x / 4x / 16x Small). All five tiers are available in every size.
The 2026-10-23 update doubles both aircraft and engine building capacity without
changing construction prices, property upkeep, equipment prices/upkeep or automation
work multipliers. Existing buildings receive the new capacity automatically; saved
workforce limits, stock goals and paid work remain unchanged.

| Equipment | Aircraft work | Engine work | Engine throughput gain | Initial installation | Purchase multiplier | Monthly active cost |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Hand | 100% | 100% | 0% | 0 months | 5x | 0.10% |
| Jigs | 90% | 88% | +13.64% | 2 months | 45x | 0.15% |
| Mechanized | 78% | 74% | +35.14% | 4 months | 140x | 0.20% |
| Automated | 65% | 59% | +69.49% | 7 months | 300x | 0.25% |
| Sophisticated | 50% | 45% | +122.22% | 10 months | 455x | 0.35% |

These engine-only multipliers also apply to engine spare parts. Paid WIP keeps
its frozen recipe; untouched/new units use the new multiplier. Aircraft work,
building capacity, material costs and equipment purchase/upkeep remain unchanged.
Engine-only automation validation: complete EditMode suite
`Logs/Workflow/editmode-20261022-171236.xml`, 748 passed, zero failed, 37 explicit
graphics-only skips. Includes fixed separate aircraft/engine coefficients,
15 size/tier combinations, actual engine/parts work and frozen old WIP followed
by newly tuned production. No launcher checkpoint performed for this change.

Purchase cost = base product MD * labor cost per MD * purchase multiplier.
Monthly active cost is the percentage of equipment purchase cost; materials per
unit are not reduced by automation. Throughput is an upper bound, assuming full
staffing, no parts allocation, sufficient supply and unsatisfied stock targets.

The 2026-10-22 capacity-alignment change passed the complete EditMode suite:
`Logs/Workflow/editmode-20261022-170746.xml` (742 passed, zero failed, 37 explicit
graphics-only skips). This includes all 15 size/automation combinations and
unchanged construction prices/upkeep. No launcher checkpoint was requested or
performed for this change; existing saves use the equalized capacity when loaded
by a newly built player, with explicit workforce limits preserved.

Engines and parts compete for the same site budget. Guaranteed fleet obligations
have priority, then installed engines for aircraft assembly, then optional/reserve
work. Guaranteed replacement motors are reserved against assembly consumption.
Stock targets top up inventory rather than unconditionally adding that quantity
each month. Every month the desired exact-engine/group stock is the larger of
the sum of plant targets and current real demand. Current stock and pending work
are counted once across the network. Lower targets cancel excess untouched units;
each already-paid fractional unit is preserved and completed, even if that leaves
one excess unit. Obsolete priorities are reset and current obligations reprioritize
work without losing paid MD. Parts and whole engines share one site MD accumulator.
Each site's Parts Production slider reserves up to 1–100% of available MD for
parts before whole engines. The default is 20%, including older saves.
At a fully staffed Small plant this reserves up to 1,680 MD for parts and at least
6,720 MD for engines. After parts needs and stock targets are met, unused reserved
MD transfers to engines, even at a 100% parts setting. Engines cannot lend their
share back to parts. Obligations retain priority within each kind.
Changes apply to the next unprocessed month and paid WIP remains intact.
Concrete WIP, inventory and closed-month markers survive save/load.
Manufacturing costs are booked once; fleet invoices do not charge production again.

New games require separate motor supply for aircraft assembly. Old saves retain
integrated installed-engine manufacturing until the player explicitly switches in
Motor Factories with a ready motor plant and zero airplane WIP. This avoids
discarding/reinterpreting already-paid work. Restored external-mode WIP safely
reattaches its supply callback. Motor plants may support new engine maintenance
before the old-save assembly mode is switched. No free plants or retroactive fleet
are created. Overall profit neutrality is not asserted by this initial tuning.

Run the complete Unity suite through `Scripts/WrightTycoonWorkflow.cmd -Action Test`.

Verification on 2026-10-19: the complete EditMode suite passed 578/578 with zero
failures (`Logs/Workflow/editmode-20261019-195438.xml`). Coverage includes pairwise
compatibility/group persistence, all plant sizes and automation tiers, separate
aircraft engine supply and preserved external-mode WIP, multi-engine atomic
maintenance, exact-version replacements, end-of-life quotation boundaries, civil
canonical tariffs, frozen government terms and actual save-payload JSON fields.
Launchers were not rebuilt in this implementation step; interactive visual QA
and overall long-run profit-neutrality tuning are not certified by EditMode tests.

### Equipment removal during automatic production (2026-10-26)

Ready aircraft and engine equipment can be moved to the central depot even when automatic stock targets are unmet or the plant has unfinished work. Installation must still finish first. Removal immediately stops that line, discards its carried aircraft labor and site-owned engine/parts work orders, and clears line-local targets. Already completed aircraft, engines and parts remain in their warehouses; other plants' work is untouched. Aircraft stock targets remain company-wide and can be served by other equipped plants.

Unfinished aircraft labor was already included in monthly production expenses. Engine and spare-part labor and proportional materials were already charged as work occurred. Discarding WIP neither refunds those expenses nor bills them again; its inventory valuation disappears with the unfinished orders. Reinstallation starts without the discarded progress. The UI explains the no-refund disposal before removal. Existing same-size, reinstallation-fee and installation-time rules remain unchanged.
