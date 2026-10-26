using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using UnityEngine;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private EngineManufacturingNetwork _engineManufacturing = new();
    private EngineManufacturingMonthReport? _lastEngineManufacturingReport;
    private bool _separateEngineProduction = true;
    private int _enginePlantSelection;
    private int _enginePlantDesignSelection;
    private int _enginePlantTierSelection;
    private string _enginePlantMessage = string.Empty;
    private readonly Dictionary<string, EnginePlantDraft> _enginePlantDrafts = new(StringComparer.Ordinal);
    private sealed class EnginePlantDraft
    {
        public string DesignId = string.Empty;
        public int Tier;
        public int StockTarget;
        public int PartReserve;
        public string StockTargetText = "0";
        public string PartReserveText = "0";
        public string SourceSignature = string.Empty;
    }
    private static string EnginePlantDraftSignature(EngineManufacturingSite site) =>
        $"{site.EngineDesignId}|{(int)site.AutomationTier}|{site.MonthlyEngineTarget}|{site.PartReserve}";

    private sealed class ManufacturedAircraftEngineSupply : IExternalAircraftEngineSupply
    {
        private readonly WrightTycoonLaunchRunner _owner;
        public ManufacturedAircraftEngineSupply(WrightTycoonLaunchRunner owner) => _owner = owner;
        private string? EngineId(string aircraftId) => _owner._savedAircraftDesigns.FirstOrDefault(d => d.CatalogItem.DesignId == aircraftId)?.EngineResearchId;
        public int AvailableEngines(string aircraftId)
        {
            if (EngineId(aircraftId) is not { } id) return 0;
            // Fleet maintenance has first claim on replacement motors regardless of warranty dates.
            var reserved = _owner._fleet.GetEngineServiceDemand(_owner._currentDate)
                .Where(d => d.EngineDesignId == id).Sum(d => d.ReplacementEngines);
            return System.Math.Max(0, _owner._engineManufacturing.GetEngineUnits(id) - reserved);
        }
        public bool TryConsumeEngines(string aircraftId, int units) => units > 0 && units <= AvailableEngines(aircraftId)
            && EngineId(aircraftId) is { } id && _owner._engineManufacturing.TryConsumeEngineUnits(id, units);
    }

    private void ConfigureAircraftEngineSupply()
    {
        if (_productionFactory is not null)
        {
            _productionFactory.AutomaticStockReplenishment = true;
            _productionFactory.ExternalEngineSupply = _separateEngineProduction ? new ManufacturedAircraftEngineSupply(this) : null;
        }
    }

    private IEnumerable<EngineManufacturingDemand> BuildEngineManufacturingDemand()
    {
        foreach (var due in _fleet.GetEngineServiceDemand(_currentDate))
            yield return new EngineManufacturingDemand(due.EngineDesignId, due.PartsGroupId, due.ReplacementEngines,
                due.EngineParts, due.Guaranteed);
        if (!_separateEngineProduction || _productionFactory is null) yield break;
        var plannedByType = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var site in _productionFactory.Factories)
        {
            var equipment = _productionFactory.GetEquipmentForFactory(site.FactoryId);
            var designId = equipment?.DesignId;
            var design = _savedAircraftDesigns.FirstOrDefault(d => d.CatalogItem.DesignId == designId);
            if (design is null || design.IsDecommissioned || site.Line is null || !site.IsOperational
                || site.ExpansionMonthsRemaining > 0 || equipment is not { IsReady: true }
                || !IsAircraftDesignReadyForProduction(design)) continue;
            var group = _enginePartsGroups.GetGroupId(design.EngineResearchId) ?? design.EngineResearchId;
            var remaining = _productionFactory.GetRequestedAircraftUnits(site.FactoryId);
            var aircraftModel = design.CatalogItem.Aircraft;
            var baselineAirframeMd = aircraftModel.Performance.AirframeProductionManDays > .000001d
                ? aircraftModel.Performance.AirframeProductionManDays : aircraftModel.Airframe.Performance.ProductionManDays;
            var workPerAircraft = System.Math.Max(.000001d, baselineAirframeMd * FactoryBalance.GetProductionManDayMultiplier(equipment.AutomationTier));
            var capacity = (int)System.Math.Min(int.MaxValue, System.Math.Floor((site.Line.CarryOverManDays + site.AvailableManDays) / workPerAircraft));
            var aircraft = System.Math.Min(remaining, capacity);
            var aircraftId = design.CatalogItem.DesignId;
            if (_productionFactory.GetAircraftStockTarget(aircraftId) is { } stockTarget)
            {
                var planned = plannedByType.TryGetValue(aircraftId, out var units) ? units : 0;
                aircraft = (int)System.Math.Min(aircraft, System.Math.Max(0L, (long)stockTarget - _productionFactory.GetWarehouseUnits(aircraftId) - planned));
                plannedByType[aircraftId] = planned + aircraft;
            }
            var count = checked(aircraft * design.CatalogItem.Aircraft.Airframe.Parameters.EngineCount);
            if (count <= 0) continue;
            yield return new EngineManufacturingDemand(design.EngineResearchId, group, count, 0, false, true);
        }
    }



    private void DrawEngineSupplyModeSwitch()
    {
        if (!_separateEngineProduction)
        {
            DrawWrappedFactoryStatus("Legacy save: airplane factories still fabricate installed engines internally. Motor factories can already supply engine maintenance. Switch installed-engine production only after airplane WIP is zero and a motor plant is ready.");
            if (PlannerButton("engine.mode", "Switch Aircraft Assembly to Manufactured Engine Stock"))
            {
                if (!_engineManufacturing.Sites.Any(s => s.IsReady) || (_productionFactory?.Factories.Any(s => (s.Line?.CarryOverManDays ?? 0d) > .000001d) ?? false))
                    _enginePlantMessage = "Wait for a ready motor plant and finish airplane work in progress first. No paid work is discarded.";
                else { _separateEngineProduction = true; ConfigureAircraftEngineSupply(); _enginePlantMessage = "Assembly now consumes engine stock; integrated engine fabrication and charges are disabled."; }
            }
        }
        if (!string.IsNullOrWhiteSpace(_enginePlantMessage)) DrawWrappedFactoryStatus(_enginePlantMessage);
    }

    private void DrawEnginePlantBuilding(EngineManufacturingSite plant)
    {
        DrawWrappedFactoryStatus($"Building {plant.ConstructionMonthsRemaining} mo · expansion {plant.ExpansionMonthsRemaining} mo · equipment {plant.InstallationMonthsRemaining} mo");
        DrawWrappedFactoryStatus($"Physical {FormatUiNumber(plant.BuildingCapacityManDays, "0")} MD; available {FormatUiNumber(plant.AvailableManDays, "0")} MD. Engines and parts share this budget; automation reduces work, not building size.");
        var maximum = (int)(plant.BuildingCapacityManDays / FactoryBalance.EmployeeManDaysPerMonth);
        var employeeLimit = System.Math.Min(plant.WorkforceEmployeeLimit, maximum);
        var workforce = DrawIntSlider("Maximum Employees", employeeLimit, 0, maximum, useLinearScale: true);
        if (workforce != employeeLimit) _engineManufacturing.SetWorkforceEmployeeLimit(plant.SiteId, workforce);
        var expansionEnabled = GUI.enabled;
        var expansionCost = plant.BuildingSize == FactoryBuildingSize.Large ? 0d : EngineManufacturingNetwork.ExpansionCost(plant.BuildingSize);
        var hasEquipment = !string.IsNullOrWhiteSpace(plant.EngineDesignId);
        if (hasEquipment) GUILayout.Label("Move equipment to the depot before expansion. Equipment only fits its original building size.");
        GUI.enabled = expansionEnabled && !hasEquipment && plant.ExpansionMonthsRemaining == 0 && _accountingSystem is not null && _accountingSystem.Cash >= expansionCost;
        if (plant.IsOperational && plant.BuildingSize != FactoryBuildingSize.Large && PlannerPrimaryButton("engine.expand", "Expand Motor Factory — " + FormatCurrency(expansionCost)) && _accountingSystem is not null)
        {
            if (_engineManufacturing.TryExpand(plant.SiteId, _accountingSystem.Cash, out var cost, out var message))
                _accountingSystem.RegisterCapitalExpense(_elapsedMonths, _currentDate, cost, "Motor factory expansion.");
            _enginePlantMessage = message;
            _factoryPlannerRefreshAt = 0;
        }
        GUI.enabled = expansionEnabled;
        if (plant.IsOperational && plant.BuildingSize != FactoryBuildingSize.Large && _accountingSystem is not null && _accountingSystem.Cash < expansionCost)
            GUILayout.Label("Additional cash needed: " + FormatCurrency(expansionCost - _accountingSystem.Cash));
    }

    private void DrawEnginePlantConfiguration(EngineManufacturingSite plant)
    {
        var percent = DrawFactoryPartsAllocation("engine.parts-share", plant.PartsAllocationPercent, plant.AvailableManDays);
        if (percent != plant.PartsAllocationPercent)
        {
            _engineManufacturing.SetPartsAllocationPercent(plant.SiteId, percent);
            _factoryPlannerRefreshAt = 0;
        }
        var catalog = BuildEngineManufacturingCatalog();
        var choices = catalog.Values.OrderBy(e => e.EngineDesignId, StringComparer.Ordinal).ToArray();
        if (choices.Length > 0 && plant.IsOperational)
        {
            var sourceSignature = EnginePlantDraftSignature(plant);
            if (!_enginePlantDrafts.TryGetValue(plant.SiteId, out var draft) || draft.SourceSignature != sourceSignature)
            {
                draft = new EnginePlantDraft { DesignId = plant.EngineDesignId, Tier = (int)plant.AutomationTier,
                    StockTarget = plant.MonthlyEngineTarget, PartReserve = plant.PartReserve, SourceSignature = sourceSignature,
                    StockTargetText = plant.MonthlyEngineTarget.ToString(CultureInfo.InvariantCulture),
                    PartReserveText = plant.PartReserve.ToString(CultureInfo.InvariantCulture) };
                _enginePlantDrafts[plant.SiteId] = draft;
            }
            var selectedIndex = Array.FindIndex(choices, choice => choice.EngineDesignId == draft.DesignId);
            _enginePlantDesignSelection = DrawFactoryChoice("engine.product", "Engine", Mathf.Clamp(selectedIndex, 0, choices.Length - 1),
                choices.Select(e => _savedEngines.FirstOrDefault(s => s.ResearchId == e.EngineDesignId)?.Name ?? e.EngineDesignId).ToArray());
            draft.DesignId = choices[_enginePlantDesignSelection].EngineDesignId;
            var equipmentConfirmed = !string.IsNullOrWhiteSpace(plant.EngineDesignId);
            _enginePlantTierSelection = DrawFactoryAutomationChoice("engine.automation",
                equipmentConfirmed ? (int)plant.AutomationTier : draft.Tier, equipmentConfirmed);
            draft.Tier = _enginePlantTierSelection;
            draft.StockTargetText = DrawEngineStockTargetInput("engine.stock-target", "Engine Stock Target", plant.SiteId, draft.StockTargetText);
            draft.PartReserveText = DrawEngineStockTargetInput("engine.parts-target", "Shared Parts Stock Target", plant.SiteId, draft.PartReserveText);
            var validEngines = TryParseEngineStockTarget(draft.StockTargetText, out var stockTarget);
            var validParts = TryParseEngineStockTarget(draft.PartReserveText, out var partReserve);
            if (validEngines) draft.StockTarget = stockTarget;
            if (validParts) draft.PartReserve = partReserve;
            if (!validEngines || !validParts) DrawError("Stock targets must be whole numbers from 0 to 2 147 483 647.");
            DrawWrappedFactoryStatus("Targets are warehouse quantities, not monthly production. Each month the network fills shortages and stops when targets are met. Motors and parts consume the SAME MD budget.");
            var selected = choices[_enginePlantDesignSelection];
            var tier = (FactoryAutomationTier)_enginePlantTierSelection;
            var equipmentCost = plant.EngineDesignId == selected.EngineDesignId && plant.AutomationTier == tier ? 0d
                : FactoryBalance.GetEquipmentPurchaseCost(tier, plant.BuildingSize);
            DrawWrappedFactoryStatus($"Equipment {FormatCurrency(equipmentCost)} · setup {FactoryBalance.GetInitialInstallationMonths(tier)} mo · +{FormatUiNumber((1d / FactoryBalance.GetEngineProductionManDayMultiplier(tier) - 1d) * 100d, "0")}% speed");
            if (!equipmentConfirmed || plant.EngineDesignId != selected.EngineDesignId)
            {
                DrawModernInfoRow("Estimated monthly output", $"Up to {FormatUiNumber(GetEnginePlantMonthlyCapacity(plant, selected, tier), "0")} engines/month");
                RecordFactoryControl("engine.output-preview");
                DrawWrappedFactoryStatus("Capacity after construction and equipment installation, before parts allocation and stock targets. Updates with the selected engine and automation level.");
            }
            var applyEnabled = GUI.enabled;
            GUI.enabled = applyEnabled && validEngines && validParts;
            if (PlannerPrimaryButton("engine.apply", equipmentConfirmed ? "Apply Product and Stock Targets" : "Approve Equipment and Stock Targets") && _accountingSystem is not null)
            {
                if (_engineManufacturing.Configure(plant.SiteId, selected, tier, draft.StockTarget, draft.PartReserve, _accountingSystem.Cash, out var cost, out var message))
                {
                    if (cost > 0d) _accountingSystem.RegisterCapitalExpense(_elapsedMonths, _currentDate, cost, "Motor production equipment.");
                    draft.SourceSignature = EnginePlantDraftSignature(plant);
                }
                _enginePlantMessage = message;
            }
            GUI.enabled = applyEnabled;
            GUILayout.Label($"Selected version: {_engineManufacturing.GetEngineUnits(selected.EngineDesignId)} engines in stock · {_engineManufacturing.GetPartUnits(selected.PartsGroupId)} shared parts");
        }
        else if (choices.Length == 0) GUILayout.Label("Approve an engine to install its production equipment.");
        if (!string.IsNullOrWhiteSpace(plant.EngineDesignId))
            DrawWrappedFactoryStatus("Moving equipment discards paid unfinished work without refund. Finished stock is kept.");
        if (!string.IsNullOrWhiteSpace(plant.EngineDesignId)
            && PlannerButton("engine.store", "Move Equipment to Central Depot (free)"))
            _engineManufacturing.TryStoreEquipment(plant.SiteId, out _enginePlantMessage);
        if (_lastEngineManufacturingReport is { } report)
            DrawWrappedFactoryStatus($"Motor network last month (all plants): {report.CompletedEngines} engines, {report.CompletedParts} parts · {FormatUiNumber(report.UsedManDays, "0.0")} MD · expenses {FormatCurrency(report.TotalExpenses)}");
    }

    private void DrawEnginePlantProductionSummary(EngineManufacturingSite plant, FactoryPlannerRow row)
    {
        DrawSection("Production Line");
        var badge = row.IsBuilding ? $"BUILDING — {plant.ConstructionMonthsRemaining} MONTH(S)"
            : row.IsExpanding ? $"EXPANDING — {plant.ExpansionMonthsRemaining} MONTH(S)"
            : row.IsInstalling ? $"INSTALLING — {plant.InstallationMonthsRemaining} MONTH(S)"
            : row.Status == "Idle" ? "WAITING — STOCK TARGETS MET"
            : row.Status == "No workforce" ? "WAITING — NO WORKFORCE"
            : row.Status == "No equipment" ? "WAITING — NO EQUIPMENT" : "READY — REPLENISHING";
        DrawModernBadge(badge, row.IsReady && row.Status != "Idle" ? ModernUiTone.Success : ModernUiTone.Warning);
        var catalog = BuildEngineManufacturingCatalog();
        if (catalog.TryGetValue(plant.EngineDesignId, out var item))
        {
            var maximum = GetEnginePlantMonthlyCapacity(plant, item, plant.AutomationTier);
            DrawWrappedFactoryStatus($"Automatic replenishment · capacity up to {FormatUiNumber(maximum, "0")} engines/month before parts allocation and stock targets.");
        }
        DrawWrappedFactoryStatus("Engines and parts share capacity. Full stock targets pause production; withdrawals restart it.");
        DrawModernInfoRow("Recorded total built", $"{plant.LifetimeProducedEngines:N0} engines · {plant.LifetimeProducedParts:N0} parts");
        if (plant.LastProductionMonth >= 0)
            DrawModernInfoRow("Last processed month", $"{plant.LastProducedEngines} engines · {plant.LastProducedParts} parts");
        else DrawWrappedFactoryStatus("No recorded production month yet. Earlier per-plant output is not reconstructed from warehouse stock.");
    }

    private static double GetEnginePlantMonthlyCapacity(EngineManufacturingSite plant, EngineManufacturingCatalogItem engine, FactoryAutomationTier tier)
    {
        // AvailableManDays is zero until equipment is ready; previews must instead
        // use the building/workforce budget that will exist after installation.
        var capacity = System.Math.Min(plant.BuildingCapacityManDays,
            (double)plant.WorkforceEmployeeLimit * FactoryBalance.EmployeeManDaysPerMonth);
        var unitWork = engine.ManDays * FactoryBalance.GetEngineProductionManDayMultiplier(tier);
        return unitWork > .000001d ? System.Math.Floor(System.Math.Max(0d, capacity) / unitWork) : 0d;
    }

    private string DrawEngineStockTargetInput(string id, string label, string siteId, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(235f));
        var next = DrawDragSelectTextField(BuildControlName("engine-stock", id + ":" + siteId), value, GUILayout.Width(180f));
        RecordFactoryControl(id);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        return next;
    }

    private void DrawMotorEquipmentDepot()
    {
        GUILayout.Label($"Stored motor sets: {_engineManufacturing.StoredEquipment.Count} | storage: {FormatCurrency(_engineManufacturing.MonthlyEquipmentStorageCost)}/month (0.01%)");
        foreach (var item in _engineManufacturing.StoredEquipment.ToArray())
        {
            GUILayout.BeginVertical(GUI.skin.box);
            var name = _savedEngines.FirstOrDefault(e => e.ResearchId == item.EngineDesignId)?.Name ?? item.EngineDesignId;
            GUILayout.Label($"{item.EquipmentId} | {name} | {item.AutomationTier} | {item.BuildingSize}");
            var targets = _engineManufacturing.Sites.Where(s => s.IsOperational && s.ExpansionMonthsRemaining == 0
                && string.IsNullOrWhiteSpace(s.EngineDesignId) && s.BuildingSize == item.BuildingSize).ToArray();
            foreach (var target in targets)
            {
                var fee = item.PurchaseCost * FactoryBalance.EquipmentReinstallationCostShare;
                if (PlannerPrimaryButton("depot.motor.install:" + item.EquipmentId + ":" + target.SiteId,
                        $"Install in {target.SiteId} — {FormatCurrency(fee)} / {FactoryBalance.GetReinstallationMonths(item.AutomationTier)} months")
                    && _accountingSystem is not null)
                {
                    if (_engineManufacturing.TryReinstallEquipment(item.EquipmentId, target.SiteId, _accountingSystem.Cash, out var cost, out var message))
                        _accountingSystem.RegisterCapitalExpense(_elapsedMonths, _currentDate, cost, "Motor equipment reinstallation.");
                    _factoryActionMessage = message;
                    break;
                }
            }
            if (targets.Length == 0) GUILayout.Label($"No empty operational {item.BuildingSize} motor plant is available.");
            if (PlannerButton("depot.motor.sell:" + item.EquipmentId, "Sell Stored Equipment (15%)"))
            {
                if (_engineManufacturing.TryScrapStoredEquipment(item.EquipmentId, out var income))
                {
                    var inventory = CalculateInventoryWithSupport(_productionFactory!.WarehouseUnitsByDesign, BuildProductionCatalog(includeOnlyResearched: false));
                    _accountingSystem?.RegisterContractIncome(_elapsedMonths, _currentDate, income, inventory, "Stored motor equipment sale.");
                    _factoryActionMessage = "Stored motor equipment sold.";
                }
                else _factoryActionMessage = "Stored motor equipment could not be sold.";
            }
            GUILayout.EndVertical();
        }
    }

    private static bool TryParseEngineStockTarget(string text, out int value) => int.TryParse(
        string.Concat((text ?? string.Empty).Where(c => !char.IsWhiteSpace(c))), NumberStyles.None,
        CultureInfo.InvariantCulture, out value);
}
