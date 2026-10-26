# Technical cleanup — 26 October 2026

## Scope simplified

Repository: `C:/Wibe coding projekty/Wright tycoon 0.52`, branch `master`. This cleanup is measured against the existing dirty working tree, not against HEAD: earlier game changes were preserved. Scope: unreachable/private UI code, obsolete editor API calls and proven unused visual assets. No launcher rebuild was requested.

## Summary of simplification

- Removed 33 private methods from the obsolete factory UI island and other unreferenced helpers. The main `OnGUI` branch already dispatches Factory to `DrawFactoryPlanner`; its old two-pane factory branches inside the opposite branch were unreachable.
- Removed associated unused private scroll/debug/comparison fields, reset assignments and tooltip constants. Eight production/editor files have a net reduction of 816 lines compared with their pre-cleanup copies.
- Replaced three obsolete `PlayerSettings` scripting-backend overload calls with `NamedBuildTarget.Standalone`, retaining the same backend selection and restoration.
- Removed 9 unused assets and their metadata (23 files, 219,328 bytes): three `Simple Aircraft Pack` demo scenes, two demo-only post-processing profiles, two apron materials, `apronTex.png`, and the unreferenced `_Recovery/0.unity` scene. Empty removed folders' metadata was removed with them.

### Removed private methods

From `WrightTycoonLaunchRunner.cs`: `DrawFactoryPane`, `DrawFactoryTab`, `DrawFactorySubTabs`, `DrawAircraftFactoryConstruction`, `DrawFactoryNetworkView`, `DrawAircraftWarehouseView`, `ResolveEffectiveFactoryLineTarget`, `DrawEngineVersionComparison`, `FindLatestFailedGovernmentContractEvaluation`, `IsFirstYearResearchApprovalWindow`, `BuildResearchTimeBonusFormula`, `TryResolveApprovedYearFromResearchBoard`, `BuildEngineForDate`, `ClampResearchPreviewReliability`, `ApplyTypeResearchModifiersToSavedDesigns`, `ResolveResearchReferenceYear`, `DrawHistoricalCampaignDebugPanel`, `ApplyHistoricalCampaignDebugDateOverride`, `TriggerNextHistoricalCampaignEventForDebug`, `SyncHistoricalCampaignDebugInputsToCurrentDate`, `CalculateContractProfitLoss`, `DrawEngineCountSelector`, `DrawMetricWithBreakdownTooltip`, `RegisterModifierTooltipRegion`, `CombineRects`, `BuildAllowedEngineFamilyTooltipDefinition`.

From partial files: `DrawEngineFactoriesTab`, `DrawEngineFactoryConstruction`, `DrawAirframeSparePartWarehouse`, `DrawAircraftSimilarity`, `DrawPlannerMotorWarehouse`, `DrawFactorySizeOverview`, `DrawFactoryLifecycleForecast`.

## Existing code reused

The existing factory planner, unified warehouse, shared production/equipment controls and reports remain the only active paths. Shared helpers such as `DrawEquipmentDepotView`, `DrawFactoryReport`, `DrawFactoryEquipmentPurchase`, `DrawInstalledFactoryEquipment`, `DrawFactoryLineEconomics`, `DrawWrappedFactoryStatus`, `DrawEngineSupplyModeSwitch` and stock-target controls were retained.

## Existing library support considered

No new dependency. The editor API replacement uses the installed Unity 6000.3.9f1 API and compiles against that editor. Runtime and core assembly boundaries are unchanged.

## Project conventions followed

Used `Scripts/WrightTycoonWorkflow.cmd`. Graphics tests ran on the isolated, non-active Windows desktop. No routine cache deletion, destructive Git reset, player-data cleanup, dependency removal or test coverage reduction was performed.

## Capability reduction

None in the game, its public APIs or supported editor workflows. Removed only unreachable/private helpers and unreferenced demo/recovery content. Development source models and regeneration tools remain available.

## What remained stable

- Core calculations, category balance, contract pricing, manufacturing, accounting and save payloads were not changed.
- All 32 aircraft pictures, 6 engine pictures, 3 factory icons, 2 menu images, 5 music tracks and 14 campaign images remain. `Resources.LoadAll` and title-based campaign-image loading count as uses even without GUID references.
- Source aircraft prefabs/meshes/materials and their six textures remain: the editor importer/validation tools use source paths, and runtime preview materials reference the source textures. The `PackFuselage` fallback remains.
- No public/persisted member, serialization schema, Unity callback, attributed editor entrypoint or reflected test entrypoint was removed.

## Tests run

- Initial and post-cleanup normal Launcher suites: 377 passed, 28 graphics-dependent cases skipped headlessly, 0 failed (405 discovered).
- First Full graphics-enabled audit: 1,085 passed, 1 failed. The existing extended legacy pricing test multiplied a rounded displayed cost instead of the unrounded estimate used by production code, yielding a one-cent mismatch after the prior category rebalance. Corrected only the test oracle: assert both rounded displayed cost and final rounded price against the unrounded estimate. No pricing implementation or tolerance was loosened.
- Final Full graphics-enabled audit: **1,086/1,086 passed, 0 skipped**, `Logs/Workflow/editmode-20261026-130232.xml`.
- Rechecked all removed asset GUIDs against remaining `Assets`, `ProjectSettings`, `Scripts` and `Docs`: no references remain. Removed scenes were not build scenes. Dynamic path loaders and editor tools were separately checked.

## Residual risks

This is a conservative static-reference audit plus the full automated suite, not proof that every remaining file is necessary. Compatibility branches, public APIs, editor utilities, source assets and uncertain candidates were deliberately retained. No claim of a measured FPS improvement is made. Launcher packages still contain the previous code until a future requested update.

## Rollback notes

Exact pre-cleanup copies of the affected launch partials, editor build pipeline and corrected test, plus all removed visual assets and metadata, are preserved under `Logs/Workflow/technical-cleanup-backup-20261026-125842`. Restore individual files from this backup if needed; avoid resetting the dirty working tree. Player saves and settings were not moved or deleted.
