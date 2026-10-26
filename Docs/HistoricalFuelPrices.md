# Historical aviation fuel prices and Operation Costs

Snapshot imported 19 October 2026. The game runs offline; it never requests internet prices during play.

## Primary sources

- [EIA US kerosene jet fuel refiner sales to end users, monthly USD/US gallon](https://www.eia.gov/dnav/pet/hist/LeafHandler.ashx?n=PET&s=EMA_EPJK_PTG_NUS_DPG&f=M): July1975–March2022.
- [EIA US aviation gasoline refiner sales to end users, monthly USD/US gallon](https://www.eia.gov/dnav/pet/hist/LeafHandler.ashx?n=PET&s=EMA_EPPV_PTG_NUS_DPG&f=M): January1983–March2022, with missing/withheld months.
- [EIA US Gulf Coast jet spot price, monthly USD/US gallon](https://www.eia.gov/dnav/pet/hist/LeafHandler.ashx?n=PET&s=EER_EPJK_PF4_RGC_DPG&f=M): used after March2022; imported observations through September2026.
- [EIA US crude first-purchase price, annual USD/barrel](https://www.eia.gov/dnav/pet/hist/LeafHandler.ashx?n=PET&s=F000000__3&f=A): input proxy for the early game years, not an aviation-fuel observation.

The EIA refiner series excludes taxes and represents US reference prices, not a historical Czech airport retail tariff. Nominal dollars are used, without inflation conversion. Spot and refiner end-user prices are different market stages; they are not spliced as equivalent observations.

## Explicitly estimated periods

- Before July1975, jet price is interpolated annual crude/42 plus a fixed refining premium calibrated to the first July1975 jet price. Annual crude input is linearly interpolated at calendar-month boundaries. This is a game estimate, not a claim that EIA measured jet fuel in1946.
- After March2022, jet spot movements are scaled to the last refiner observation using the March2022 spot/refiner ratio. These values are spot-linked estimates, not measured refiner prices.
- Missing/early avgas values use jet movements with the most recent available observed avgas/jet ratio; before the first avgas observation use the first January1983 calibration. Withheld values are never treated as zero.
- Beyond the final available monthly observation, prices are held at the last available month. No invented future shocks, random daily changes or live market feed.

Every quote carries its month, provenance and estimate flag. The Operation Costs tooltip identifies estimates. A `MonthlyAviationFuelMarket` refreshes both fuels together only when the simulation year/month changes, including restoring another date from a save. Repeated designer edits in one month reuse the same quotes.

Conversion: 1USgal =3.785411784L. Existing game density assumptions are .80kg/L for jet fuel and .72kg/L for avgas. Thus USD/kg =USD/USgal divided by (3.785411784×density), matching cruise fuel burn in kg/h. These densities are fixed game assumptions, not temperature-dependent source observations.

## Displayed Operation Costs

`USD/h = reference purchase price / ExpectedLife + maintenance cycles before retirement × parts per cycle × selling price per part / ExpectedLife + cruise kg/h × monthly USD/kg`.

Reference purchase price is complete aircraft production cost multiplied by the existing standard small-order sales markup, margin-improvement multiplier and company reputation multiplier. No contract is selected in the designer, so this is a documented reference retail price, not an asserted contract invoice. The tooltip exposes the price and breakdown. Exact-endpoint maintenance is excluded: a machine retires at ExpectedLife before another service. No downtime, support-risk surcharge, workshop fee, manufacturer profit or separate engine overhaul enters these three components. Current-month fuel price is held across the reference lifetime for this estimate; this is not a prediction of future fuel prices.

This changes the designer/current type ownership metric. Previously signed lifecycle quotes and their frozen fuel-price assumptions are not retroactively rewritten. Customer contract lifecycle estimates that additionally model downtime and support risk remain a distinct metric.

Type designer: the former service detail block is removed. Expected Life and Operation Costs appear as standard parameter rows immediately below Cruise Fuel Burn, with details in the existing hover tooltip. Aircraft catalogue cards reuse the two-row metric. Factory/operating-fleet part inventory remains available in its dedicated views.

Selectable maintenance intervals:200/500/1000/2000/4000h.8000h remains valid only for legacy save/offer compatibility. New contract generators use the shared selectable list. Opening a legacy8000h type in the editable designer normalizes the proposed interval to4000h; existing stored types and delivered cohorts are not rewritten in place.

## Verification

Canonical `Scripts\WrightTycoonWorkflow.cmd -Action Test`:513/513 passed after the calculator/data/UI integration. Added three editor-normalization/removal regressions, then canonical `-Action Checkpoint -Platform Windows`:516/516 passed, incremental Windows build and required startup smoke passed. Logs: `Logs/Workflow/editmode-20261019-190157.xml`, `build-checkpoint-windows-20261019-190157.log`, `windows-smoke-20261019-190157.log`.

Tests check observed EIA monthly values, USgal-to-kg conversion, missing/withheld/early/late estimates, positive finite prices for every month1946–2030 for both fuels, cache identity within one month, month changes and date restoration, all ownership cost components and retirement endpoints, both seeded generators and legacy interval compatibility. Windows save checksum is identical before and after the checkpoint. Player version remains0.6.7/Mono; project IL2CPP configuration restored. macOS was not rebuilt. Automated suite and startup smoke do not constitute visual verification of every game screen.
