# Launcher test scope

## Update policy

Normal code validation, checkpoints and clean releases use `Launcher`: **399 discovered cases** after the independent-contract-pricing update. The exhaustive suite contains **1,080 cases**. New pricing, UI-cache and WIP regressions remain in the normal gate; obsolete-price-formula compatibility, old save-version cases and supplementary boundary/size matrices remain available in Extended. No cases were deleted or silently truncated. The fixture inventory below records the earlier audit baseline, not the current discovery totals. New tests run by default. Explicit NUnit `[Category("Extended")]` on a fixture, method or individual test case removes that scope from the normal gate.

- `Scripts\WrightTycoonWorkflow.cmd -Action Test`: Launcher, headless; graphics-dependent tests explicitly skip.
- `Scripts\WrightTycoonWorkflow.cmd -Action Test -GraphicsTests`: Launcher including UI interaction.
- `Scripts\WrightTycoonWorkflow.cmd -Action Test -TestSuite Extended`: secondary checks only.
- `Scripts\WrightTycoonWorkflow.cmd -Action Test -TestSuite Full -GraphicsTests`: exhaustive audit, including UI.
- `Scripts\WrightTycoonWorkflow.cmd -Action Checkpoint -Platform All`: normal update, Launcher gate and required Windows smoke.

Full is available for changes to compatibility, preview meshes, detailed balance tables or the test harness. Extended alone is not sufficient for a launcher build. `-SkipTests` requires an already completed Launcher or Full pass against the same code. Counts outside 250–400 warn for explicit review; the runner must never truncate discovery to fit the budget.

## Audit decisions

Baseline: `Logs/Workflow/release-all-graphics-20261025.xml`, 1,055 cases in 618 methods. There were no failing tests to hide. The reduced default intentionally has less per-update coverage; neither a name nor a high case count proves a test redundant.

1. **Actual deduplication:** merged eight profile-name cases into the existing eight speed-anchor table cases, retaining exact name and inspiration assertions. Full suite becomes **1,047** cases, not 1,055.
2. **Combinatorial repetition:** full speed/orientation/efficiency grids, all size × automation combinations, long tariff horizons and dense calibration tables are Extended. Core scenario tests still cover trade-offs, finite shared budgets, production spillover and physical stock consumption.
3. **Legacy compatibility:** old save migrations, former selectable intervals and legacy paths are Extended. They are not deleted as obsolete while production still implements them. Current payload round trips, pending research and paid WIP remain in Launcher.
4. **Secondary validation:** exhaustive NaN/range rejection combinations are Extended. Main failed purchase, finite-budget, atomic-consumption and contract eligibility paths remain in Launcher. This does not alter production validation.
5. **Presentation detail:** preview geometry, labels, formatting, icon import and layout-only permutations are Extended. Real factory mouse interactions, stock/depot controls, financing and technology tooltips remain in Launcher.
6. **Detailed calibration:** individual legacy profile/engine tuning relationships and direct implementation detail checks are Extended. Representative piston/jet output, altitude, afterburner, precision engineering, modular aerodynamic trade-offs and technology freeze remain in Launcher.

Launcher retains maintenance revenue/cost basis and save replay; exact-version motors and shared-family parts; service debt and grounding; warranty and accident reputation; contract eligibility/pricing and frozen offers; loans and delayed financing; manufacturing WIP, goals and depot equipment; research completion and family/type technology snapshots; runtime save round trips.

## Explicit fixture inventory

Counts are discovered NUnit cases, not assertion counts. `Extended` is retained and opt-in; it is not ignored or disabled in Unity.

| Fixture | Launcher | Extended |
| --- | ---: | ---: |
| LandingGearTests | 2 | 2 |
| LandingGearUiTests | 0 | 2 |
| SliderAndMotorFactoryUiTests | 0 | 2 |
| AccountingSystemTests | 5 | 0 |
| AerodynamicsCalculatorTests | 2 | 0 |
| AircraftCategoryCatalogTests | 0 | 4 |
| AircraftFamilyInstalledEngineTests | 0 | 3 |
| AircraftIntegrationTests | 3 | 37 |
| AircraftOperationCostTests | 3 | 13 |
| AircraftPreviewModuleFactoryTests | 0 | 49 |
| AircraftPreviewModuleTests | 0 | 15 |
| AircraftProductionFactoryTests | 11 | 18 |
| AircraftSparePartEconomyTests | 4 | 16 |
| AircraftSparePartsProductionTests | 15 | 0 |
| AirframeCalculatorTests | 0 | 20 |
| AutomaticAircraftStockTests | 2 | 0 |
| CategoryComplianceEvaluatorTests | 11 | 7 |
| CompanyReputationTests | 6 | 0 |
| CompanyRunStatisticsTests | 3 | 0 |
| ContractLifecycleTextTests | 0 | 11 |
| ContractOperationalCostFuelTests | 2 | 0 |
| ContractSupportOwnershipTests | 5 | 2 |
| ContractSystemTests | 36 | 3 |
| DemandNotebookTextTests | 0 | 7 |
| DesignerFamilyAutoSaveTests | 2 | 10 |
| DesignerTooltipTests | 0 | 6 |
| EngineAfterburnerAvailabilityTests | 2 | 0 |
| EngineCalculatorTests | 5 | 52 |
| EngineLifecycleDesignTests | 4 | 10 |
| EngineManufacturingNetworkTests | 33 | 37 |
| EngineOwnershipCostCalculatorTests | 2 | 20 |
| EngineStockTargetInputTests | 0 | 10 |
| FactoryBuildingIconsTests | 0 | 3 |
| FactoryFinancingTests | 10 | 10 |
| FactoryLifecycleProductionTests | 9 | 35 |
| FactoryPlannerGuiTests | 0 | 4 |
| FactoryPlannerInteractionTests | 23 | 16 |
| FactoryPlannerPresentationTests | 0 | 11 |
| FactoryWarehouseTests | 7 | 0 |
| FinanceTimelineChartTests | 0 | 7 |
| FinancialValueFormatterTests | 0 | 6 |
| FleetAircraftSparePartsTests | 6 | 0 |
| FleetEngineMaintenanceTests | 14 | 8 |
| FleetLifecycleTests | 20 | 1 |
| FleetProductionIntegrationTests | 2 | 0 |
| FleetProfitabilityAuditTests | 0 | 3 |
| FleetRuntimeEngineProviderTests | 3 | 0 |
| FleetSavePayloadTests | 5 | 2 |
| FleetServiceAccountingTests | 2 | 0 |
| FleetServiceBalanceTests | 4 | 0 |
| GovernmentContractModuleTests | 22 | 1 |
| GovernmentContractNotebookWindowTests | 0 | 2 |
| HeadquartersStateTests | 8 | 1 |
| HistoricalAviationFuelPriceTests | 2 | 13 |
| HistoricalCampaignServiceTests | 6 | 7 |
| HistoricalCampaignTechnologyProgressionTests | 4 | 1 |
| IsaAtmosphereModelTests | 2 | 1 |
| LifecycleDomainTests | 10 | 28 |
| LoanPortfolioTests | 7 | 0 |
| MachineSimilarityTests | 9 | 1 |
| MachineStatisticsLedgerTests | 0 | 7 |
| MaintenanceFinancialLedgerTests | 2 | 0 |
| MaintenancePresentationTests | 2 | 4 |
| ModularWingPerformanceTests | 6 | 55 |
| ModularWingProfileGuiTests | 0 | 3 |
| ModularWingProfileTests | 7 | 20 |
| MonthlyCompletionGuiTests | 0 | 3 |
| MonthlyCompletionSummaryTests | 5 | 0 |
| MonthlySimulationEngineTests | 1 | 0 |
| ProductionEconomyBalanceTests | 0 | 1 |
| ResearchApprovalUiTests | 0 | 7 |
| ResearchBoardTests | 6 | 0 |
| ResearchBonusRuntimeTests | 13 | 0 |
| ResearchEditPolicyTests | 3 | 1 |
| ResearchVersionLineageTests | 1 | 2 |
| ServiceIntervalRequirementTests | 0 | 13 |
| SmallAircraftWeightModifierTests | 0 | 2 |
| TechnologyEffectsTests | 7 | 5 |
| TechnologyFreezeRuntimeTests | 5 | 10 |
| TechnologyFreezeUiTests | 7 | 4 |
| WingAreaPayloadOptimizerTests | 2 | 1 |

## Limits

Unity startup/import still contributes to elapsed time even after reducing cases. Headless execution does not validate GUI rendering; use GraphicsTests for UI work. Windows smoke remains a readiness check, not an interactive playthrough. No launcher rebuild or player-data reset is part of this test-policy change.

## Verification

- Default workflow: `Logs/Workflow/editmode-20261025-183202.xml`, 370 passed, 28 explicitly ignored graphics cases, zero failed; NUnit execution 1.62 seconds (excluding Unity startup).
- Full graphics audit: 1,047 passed, zero failed or skipped; NUnit execution 22.44 seconds. This one-off audit validates the changed test harness; it is not the new default update procedure.
- Extended-only launcher build rejected before starting Unity. PowerShell syntax and scoped diff whitespace checks passed.

