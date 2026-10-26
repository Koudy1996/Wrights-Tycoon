# Factory capacity planner (0.6.7)

## Fixed equipment pricing (2026-10-25)

New aircraft and motor equipment uses the same fixed prices. Small building
prices are $1m / $8m / $25m / $55m / $90m for Hand / Jigs / Mechanized /
Automated / Sophisticated. Medium costs 3x and Large 9x. Neither materials nor
the product's manufacturing MD affect the equipment price; production recipes
and automation productivity bonuses remain unchanged.

Equipment remains product-specific. Its size, original paid price and (for
aircraft) baseline recipe MD survive storage and save/load. The central depot
supports aircraft and motor sets: storage costs 0.01% of the original price per
month, reinstallation costs 3%, and sale recovers 15%. Only empty operational
plants of the same kind and exact building size accept a stored set. Expansion
requires removing equipment first, so an installed Small set cannot silently
become Medium. Motor product replacement retains the previous equipment in the
depot instead of deleting it. Paid WIP blocks removal/replacement.

Existing paid equipment is not repriced. Legacy installed aircraft equipment
inherits its site's size. Legacy stored aircraft equipment without any original
location is assigned Small because the old save contains no recoverable size.
The old labor-cost multiplier exists only to reconstruct legacy recipe MD, not
to price any new purchase.

Implements the user's selected concept C using the existing designer theme, not the
generated mockup as a background. Main entry is the full-width Factory view in
`WrightTycoonLaunchRunner.FactoryPlannerUi.cs`. Page/section headers, selected-row
cards, buttons, labels, input controls and colors reuse `WrightTycoonUiTheme`.

## Layout and interaction

- Aircraft factories automatically replenish the exact-type aircraft stock limit and
  shared family parts goal. There is no player-editable monthly or lifetime batch
  count. Existing monthly/batch values remain readable but do not restrict gameplay.
  A full stock goal stops new work; withdrawals resume it. Zero pauses aircraft
  replenishment; an explicitly unlimited aircraft goal keeps production running.
  Paid WIP, staffing limits, parts allocation and engine-stock requirements still apply.
- Physical building capacity is equal for aircraft and motor buildings:
  16,800 / 67,200 / 268,800 MD/month (Small / Medium / Large). Motor capacity
  was aligned with aircraft capacity on 2026-10-22 without changing construction,
  expansion, equipment or property-upkeep prices.
- Both production panels have a per-plant Parts Production slider (1–100%, default
  20%, also for older saves). Parts/support are processed first, up to that share
  of the staffed MD budget. Once demand and stock goals are met, the unused parts
  share transfers automatically to whole aircraft/engines (including at 100%).
  Whole-machine capacity does not transfer back to parts. Changes apply to the next
  unprocessed month; saved monthly budgets prevent extra work after reloading.
- Aircraft and motor setup share the same five-button automation selection and
  explicit equipment approval. The selection is shown only before equipment is
  installed/configured. After approval, including installation in progress, the
  installed tier is read-only; stock edits, navigation and save/restore do not
  reopen the automation selector. Motor product changes retain the installed tier.
  Empty aircraft plants still choose equipment when making a new installation;
  installed equipment retains its own tier in the depot. Aircraft stock/parts
  controls sit together above compact line controls; economics expand separately.
- Aircraft Stock Limit is a typed, optional limit on completed aircraft per exact
  type, shared by all its plants. Apply accepts 0 through Int32.MaxValue; zero
  pauses new work and Remove Limit restores unlimited stock. Existing saves keep
  unlimited production until configured. Monthly line targets and total production
  goals still apply. Reaching stock limits preserves paid WIP; withdrawals resume
  replenishment. The warehouse shows the stock limit alongside completed units.
- The shared airframe-parts target is now a typed whole-number field with an Apply
  button, matching engine targets and accepting 0 through Int32.MaxValue. Invalid,
  negative or overflowing input cannot alter the target. Compatible aircraft and
  all their plants continue sharing one group reserve; this is not monthly output.
- Fixed top navigation: Aircraft Factories, Engine Factories, Warehouses,
  Equipment Depot, Reports, and an always-available Build Factory action.
  Construction is an explicit separate view with a Back button, never automatically
  expanded for an empty company. A production-kind switch defaults to the current
  factory section. Three cards offer Small, Medium and Large with one working build
  button each, price, duration, capacity, upkeep and the matching supplied artwork.
  Direct construction uses the cumulative existing construction/expansion price
  and duration (aircraft $80m/240m/720m, engines $40m/120m/360m; 18/30/48 months).
- Every construction card includes FINANCOVAT below Build: exactly 90% of its
  price is a 120-month amortizing loan, with APR two percentage points below the
  standard 120-month offer (minimum zero). Existing credit assessment, 0.5%
  principal origination fee, loan limits and repayment handling still apply.
  Required upfront cash is 10% of the price plus the fee. The quote displays APR,
  monthly payment and fee before clicking. Eligibility is checked again at click
  time; rejected financing creates no loan, plant or accounting transaction.
  New factory loans defer all installments and interest accrual until construction
  completes (18/30/48 months). The first payment is in the completion month, followed
  by the remaining payments of the full 120-month schedule. Principal is unchanged
  during construction. Calendar age for prepayment fees still advances, and due
  dates/origination dates persist across save/load. Financial next-month projections
  exclude payments not yet due. Existing saved loan schedules are not rewritten.
- Loan collateral includes both aircraft and motor buildings, including the
  completed fraction of construction/expansion, and motor production equipment
  at the same 15% recovery share as aircraft equipment. A new building's resale
  value is 90% of construction cost; existing annual depreciation (one percentage
  point per full operating year, minimum 30%) remains. At construction start the
  building has zero completed value; it grows linearly with completed months.
  An expansion preserves the existing building value and adds its completed
  fraction. Motor operating age is now persisted; old saves lacking it start at zero.
- Each production section shows its own plants. Compact horizontal status filters
  show All, Needs attention and Building;
  sort by plant name, type, physical capacity or measured utilization. Columns
  distinguish physical/staffed MD, current targets, previous output and motor stock.
- At widths >=1150, a narrow left plant list and a full-height right configuration
  panel replace the tall overview table. The detail receives most of the width and
  more than half of the available window height. Sorting/status controls remain
  accessible. Smaller widths use a compact 110–170px-high table above the detail.
  The selected header uses a smaller icon; narrow-detail capacity/supply information
  is collapsed behind Show Capacity & Supply rather than displacing production.
  Navigation remains outside both independent scroll views.
- Product selectors use compact previous/next buttons and an expandable bounded
  list instead of rendering every available choice vertically in the main page.
- Engine and shared-parts stock targets use player-typed integer text fields,
  not sliders capped at 1000/10000. Whitespace grouping is accepted; negative,
  fractional, blank and overflowing values disable Apply with an explanation.
  Valid targets remain drafts until Apply Equipment and Stock Targets is clicked.
  Input validation covers the existing nonnegative Int32 storage domain.
- Warehouses has only All, Aircraft and Engines filters. Each type/engine row shows
  completed units, spare parts, parts reserve and shared group. Compatible versions
  display the same shared parts stock, explicitly marked as non-additive. Legacy
  stock is retained as parameters of its corresponding type; orphaned groups are
  retained as unassigned aircraft/engine entries, never separate parts categories.
- Existing equipment purchases, deployment, stock targets, lifetime targets,
  expansion, workforce, support priority and empty-aircraft-factory sale rules are
  reused. The factory layout does not introduce economic or transport mechanics.
- New factories are selected after construction starts. UI caches and drafts reset
  on new-game/load so one company's selection does not leak into another save.

## Data honesty

Allocation bars compare actual recorded new-production and parts/support MD with
the current physical building capacity. They are not forecasts, staffed capacity
or guaranteed output. Missing per-site records are unknown rather than zero.
Engine reports now collect observational per-site production/parts MD at the exact
existing work-allocation point. Engine and parts work are measured against one
shared physical MD budget; the monthly stock-target reconciliation cancels excess
unstarted work without discarding paid fractional units.
Aircraft support work is inferred only from a matching-month shared MD budget.
Motor stock targets remain stock top-ups, not monthly production rates. Support
forecasts in the inspector are for the selected type, not assigned plant workloads.

## Supplied artwork

Original user PNGs `mala.png`, `stredni.png`, `velka.png` are copied without pixel
editing into `Assets/Resources/WT04/FactoryBuildings/{small,medium,large}.png`.
`FactoryBuildingIcons.ResourceKey` maps building size to the artwork. Texture
imports preserve alpha, use clamp wrapping and disable mipmaps for IMGUI icons.

## Verification

Pure projection tests cover capacity normalization, unknown data, filtering,
deterministic sorting, mixed network projection and per-site MD reconciliation.
Resource tests load all three user icons and verify UI import settings.
`FactoryPlannerGuiTests` exercises real themed IMGUI layout/repaint at 1280 and
1920 widths. `FactoryPlannerInteractionTests` sends actual MouseDown/MouseUp and
ScrollWheel events to recorded screen-space button bounds; it does not invoke
action handlers. Cases cover navigation, all three construction sizes, construction/back, capital
payments exactly once, disabled low-cash controls, filters, sorting, plant and
detail selection, aircraft/engine expansion, product pickers, engine equipment,
aircraft equipment purchase/storage/reinstallation/sale, warehouse filters and
90% financing for every size/kind, including deposit-only liquidity and no duplicate loan on repaint.
Approved test designs are built from local definitions, never from player saves.
`FactoryWarehouseTests` verifies separate exact engine/aircraft stock, parts as
product parameters, retained legacy inventory, non-mutating reads and all three
direct-construction sizes across save/restore and construction completion.

The graphics-enabled filtered run passed 78/78:
`Logs/Workflow/contract-service-minimum-20261021.xml` (includes contract minimum
service-interval presentation/save compatibility, projection, warehouse,
financing and collateral tests, actual deferred financing clicks, construction
completion alignment, grace-period save/restore, full 120-payment closure,
calendar prepayment age, old-loan compatibility, engine service interval clicks
and majority-width/full-height horizontal detail viewport assertions).
Thirty-two graphics-only tests skip explicitly in the headless workflow.
This checks real controls and rendering, not pixel-by-pixel visual approval.

Windows checkpoint test results: `Logs/Workflow/editmode-20261021-185419.xml`
reports 677 passed, zero failed, 32 explicitly skipped graphics-only cases.
Build/startup logs use the matching `20261021-185419` timestamp.

### Earlier capacity-only checkpoint (2026-10-21)

The final complete EditMode suite in `Logs/Workflow/editmode-20261021-194828.xml`
passed 708 tests with zero failures; 35 graphics-only cases skipped explicitly.
The separate graphics-enabled run `factory-parts-ui-verified-20261021.xml` passed
83/83. This includes typing airframe targets beyond the old slider cap, invalid
and overflowing input rejection, dragging both allocation sliders to 1% and 100%,
and retaining an installed aircraft line's target when 100% parts pauses production.
Domain regressions cover all support kinds, staffing, strict no-spillover quotas,
saved/frozen mid-month budgets, high aggregated targets, and paid WIP preservation.
Windows incremental checkpoint and startup smoke passed with matching log suffix
`20261021-194828`; the packaged game assembly was updated at 19:49:09. The SHA-256
of the existing Windows save file was identical before and after the checkpoint.

### Stock limits and automatic capacity reuse (2026-10-21)

`factory-stock-spill-ui-20261021.xml` passed 85/85 graphics-enabled checks,
including real typing/apply/clear for stock limits and the assembly-engine forecast
not double-counting one shared limit across two factories. The earlier headless
run `editmode-20261021-214740.xml` passed 727 with zero failures and 37 explicit
graphics-only skips. The final Windows checkpoint `editmode-20261021-214920.xml`
also passed 727 with zero failures and 37 explicit skips. The incremental Windows
build and startup smoke passed (matching log suffix `20261021-214920`); the game
assembly was updated at 21:50:01. Existing save-file SHA-256 was unchanged.

Parts reserve reductions, including zero/omitted requests, cancel untouched
orders; a paid fractional part is retained and may finish one last unit. Parts
are processed within their reserved share before whole machines borrow its unused
capacity. The legacy network-wide priority selector is hidden because the
per-factory slider now controls reservation and automatic release. Existing saved
priority values remain loadable; all three settings respect the same stock and
physical-capacity limits. Legacy saves without aircraft stock limits stay unlimited.

### Unified equipment approval UI (2026-10-22)

`factory-approval-ui-20261022.xml` passed 89/89 graphics-enabled checks.
New approval cases click Mechanized for aircraft and motors at 1280 and 1920
requested widths, approve equipment, check all five tier choices are absent,
edit stock targets without another equipment payment, and verify the selector
stays hidden after navigation and snapshot restore. The additional
`factory-economics-ui-20261022.xml` click case passed 1/1, opening/closing the
economics panel before storing/reinstalling/selling aircraft equipment.
The initial complete EditMode run `editmode-20261022-175432.xml` passed 748,
zero failed, 41 graphics-only cases explicitly skipped.
The final Windows checkpoint repeated those results in
`editmode-20261022-175719.xml`; the matching incremental build and startup smoke
passed. The existing Windows save-file SHA-256 was unchanged across the checkpoint.
