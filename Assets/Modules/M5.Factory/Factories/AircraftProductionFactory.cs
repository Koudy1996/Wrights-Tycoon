using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Presentation;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Factories;

public interface IExternalAircraftEngineSupply
{
    int AvailableEngines(string aircraftDesignId);
    bool TryConsumeEngines(string aircraftDesignId, int units);
}

public enum FactoryEquipmentLocation
{
    Stored = 0,
    Installing = 1,
    Installed = 2,
}

public sealed class FactoryEquipmentState
{
    internal FactoryEquipmentState(
        string equipmentId,
        string designId,
        FactoryAutomationTier automationTier,
        double purchaseCost,
        string? factoryId,
        string? lineId,
        int installationMonthsRemaining,
        int lifetimeProducedUnits = 0,
        double cumulativeBaselineLaborExpenses = 0d,
        double cumulativeActualLaborExpenses = 0d,
        double cumulativeActiveEquipmentExpenses = 0d)
    {
        EquipmentId = equipmentId;
        DesignId = designId;
        AutomationTier = automationTier;
        PurchaseCost = purchaseCost;
        FactoryId = factoryId;
        LineId = lineId;
        InstallationMonthsRemaining = installationMonthsRemaining;
        LifetimeProducedUnits = lifetimeProducedUnits;
        CumulativeBaselineLaborExpenses = cumulativeBaselineLaborExpenses;
        CumulativeActualLaborExpenses = cumulativeActualLaborExpenses;
        CumulativeActiveEquipmentExpenses = cumulativeActiveEquipmentExpenses;
    }

    public string EquipmentId { get; }
    public string DesignId { get; }
    public FactoryAutomationTier AutomationTier { get; }
    public double PurchaseCost { get; }
    public FactoryBuildingSize BuildingSize { get; internal set; }
    public double BaselineProductionManDays { get; internal set; }
    public string? FactoryId { get; internal set; }
    public string? LineId { get; internal set; }
    public int InstallationMonthsRemaining { get; internal set; }
    public int LifetimeProducedUnits { get; internal set; }
    public double CumulativeBaselineLaborExpenses { get; internal set; }
    public double CumulativeActualLaborExpenses { get; internal set; }
    public double CumulativeActiveEquipmentExpenses { get; internal set; }
    public bool IsInstalled => !string.IsNullOrWhiteSpace(FactoryId);
    public bool IsReady => IsInstalled && InstallationMonthsRemaining == 0;
    public FactoryEquipmentLocation Location => !IsInstalled
        ? FactoryEquipmentLocation.Stored
        : InstallationMonthsRemaining > 0
            ? FactoryEquipmentLocation.Installing
            : FactoryEquipmentLocation.Installed;
    public double CumulativeGrossLaborSavings => System.Math.Max(0d, CumulativeBaselineLaborExpenses - CumulativeActualLaborExpenses);
    public double CumulativeNetSavings => CumulativeGrossLaborSavings - CumulativeActiveEquipmentExpenses;
}

public sealed class FactorySiteState
{
    internal FactorySiteState(
        string factoryId,
        string displayName,
        int constructionMonthsRemaining,
        int operatingMonths,
        AircraftProductionLineState? line)
    {
        FactoryId = factoryId;
        DisplayName = displayName;
        ConstructionMonthsRemaining = constructionMonthsRemaining;
        OperatingMonths = operatingMonths;
        Line = line;
    }

    public string FactoryId { get; }
    public string DisplayName { get; }
    public int ConstructionMonthsRemaining { get; internal set; }
    public int OperatingMonths { get; internal set; }
    public AircraftProductionLineState? Line { get; internal set; }
    public bool IsUnderConstruction => ConstructionMonthsRemaining > 0;
    public bool IsOperational => ConstructionMonthsRemaining == 0 && Line is not null;
    public FactoryBuildingSize BuildingSize { get; internal set; }
    public double LegacyCapacityMd { get; internal set; }
    public bool NeedsLegacyCapacityMigration { get; internal set; }
    public double BaselineAircraftManDays { get; internal set; }
    public int WorkforceEmployeeLimit { get; internal set; } = int.MaxValue;
    public int PartsAllocationPercent { get; internal set; } = 20;
    public int ExpansionMonthsRemaining { get; internal set; }
    public double BuildingCapacityManDays => System.Math.Max(FactoryBalance.GetBuildingCapacityManDays(BuildingSize), LegacyCapacityMd);
    public double AvailableManDays => IsOperational && ExpansionMonthsRemaining == 0
        ? System.Math.Min(BuildingCapacityManDays, WorkforceEmployeeLimit * EmployeeMd) : 0d;
    public double PartsCapacityManDays => AvailableManDays * PartsAllocationPercent / 100d;
    public double ProductionCapacityManDays => AvailableManDays - PartsCapacityManDays;
    private const double EmployeeMd = FactoryBalance.EmployeeManDaysPerMonth;
}

public sealed partial class AircraftProductionFactory
{
    public const double EmployeeManDaysPerMonth = FactoryBalance.EmployeeManDaysPerMonth;

    private readonly List<FactorySiteState> _factories = new();
    private readonly Dictionary<string, FactoryEquipmentState> _equipmentById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _warehouseUnitsByDesign = new(StringComparer.Ordinal);
    private int _nextFactorySequence = 1;
    private int _nextEquipmentSequence = 1;
    private IExternalAircraftEngineSupply? _externalEngineSupply;
    private bool _externalEngineWorkMode;
    public bool UsesExternalEngineSupply => _externalEngineWorkMode;
    public IExternalAircraftEngineSupply? ExternalEngineSupply
    {
        get => _externalEngineSupply;
        set
        {
            if (_externalEngineWorkMode != (value is not null) && Lines.Any(line => line.CarryOverManDays > Numeric.Epsilon))
                throw new InvalidOperationException("Finish existing aircraft work in progress before changing engine supply mode.");
            _externalEngineSupply = value;
            _externalEngineWorkMode = value is not null;
        }
    }

    public IReadOnlyList<FactorySiteState> Factories => _factories.OrderBy(factory => factory.FactoryId, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<AircraftProductionLineState> Lines => _factories.Where(factory => factory.Line is not null).Select(factory => factory.Line!).ToArray();
    public IReadOnlyList<FactoryEquipmentState> Equipment => _equipmentById.Values.OrderBy(item => item.EquipmentId, StringComparer.Ordinal).ToArray();
    public IReadOnlyDictionary<string, int> WarehouseUnitsByDesign => new Dictionary<string, int>(_warehouseUnitsByDesign, StringComparer.Ordinal);
    public int OperationalFactoryCount => _factories.Count(factory => factory.IsOperational);
    public int FactoriesUnderConstructionCount => _factories.Count(factory => factory.IsUnderConstruction);
    /// <summary>Stock-driven gameplay policy; legacy monthly/batch API remains readable for old integrations.</summary>
    public bool AutomaticStockReplenishment { get; set; }

    public int GetRequestedAircraftUnits(string factoryId)
    {
        var site = GetFactory(factoryId);
        if (site?.Line is null) return 0;
        var equipment = GetEquipmentForFactory(factoryId);
        if (equipment is null) return 0;
        var requested = AutomaticStockReplenishment ? FactoryBalance.MaximumAircraftPerLinePerMonth : site.Line.MonthlyProductionTargetUnits;
        if (!AutomaticStockReplenishment && site.Line.TotalProductionTargetUnits > 0)
            requested = System.Math.Min(requested, System.Math.Max(0, site.Line.TotalProductionTargetUnits - site.Line.LifetimeProducedUnits));
        if (GetAircraftStockTarget(equipment.DesignId) is { } target)
            requested = System.Math.Min(requested, System.Math.Max(0, target - GetWarehouseUnits(equipment.DesignId)));
        return requested;
    }

    public bool TryStartFactoryConstruction(double availableCash, out double cost, out string message)
        => TryStartFactoryConstruction(FactoryBuildingSize.Small, availableCash, out cost, out message);

    public bool TryStartFactoryConstruction(FactoryBuildingSize size, double availableCash, out double cost, out string message)
    {
        cost = FactoryBalance.GetConstructionCost(size);
        if (availableCash < cost)
        {
            message = $"Factory construction requires {FinancialValueFormatter.FormatNumber(cost)} cash.";
            return false;
        }

        var sequence = _nextFactorySequence++;
        var factoryId = $"factory.{sequence:D3}";
        _factories.Add(new FactorySiteState(
            factoryId,
            $"Factory #{sequence}",
            FactoryBalance.GetConstructionMonths(size),
            0,
            null) { BuildingSize = size });
        message = $"{size} factory #{sequence} construction started. Completion in {FactoryBalance.GetConstructionMonths(size)} months.";
        return true;
    }

    public void AdvanceMonth()
    {
        foreach (var factory in _factories)
        {
            if (factory.ExpansionMonthsRemaining > 0)
            {
                factory.ExpansionMonthsRemaining--;
                if (factory.ExpansionMonthsRemaining == 0) factory.BuildingSize++;
            }
            if (factory.IsUnderConstruction)
            {
                factory.ConstructionMonthsRemaining--;
                if (factory.ConstructionMonthsRemaining == 0)
                {
                    factory.Line = new AircraftProductionLineState(BuildLineId(factory.FactoryId));
                }
            }
            else if (factory.IsOperational)
            {
                factory.OperatingMonths++;
            }
        }

        foreach (var equipment in _equipmentById.Values)
        {
            if (equipment.IsInstalled && equipment.InstallationMonthsRemaining > 0)
            {
                equipment.InstallationMonthsRemaining--;
            }
        }
    }

    public FactorySiteState? GetFactory(string factoryId)
    {
        if (string.IsNullOrWhiteSpace(factoryId)) return null;
        return _factories.FirstOrDefault(factory => string.Equals(factory.FactoryId, factoryId.Trim(), StringComparison.Ordinal));
    }

    public FactoryEquipmentState? GetEquipmentForFactory(string factoryId)
    {
        if (string.IsNullOrWhiteSpace(factoryId)) return null;
        return _equipmentById.Values.FirstOrDefault(item => string.Equals(item.FactoryId, factoryId.Trim(), StringComparison.Ordinal));
    }

    public bool TryPurchaseAndInstallEquipment(
        string factoryId,
        string designId,
        FactoryAutomationTier tier,
        double baselineTotalProductionManDays,
        double availableCash,
        out double cost,
        out string message)
    {
        ValidateDesignId(designId);
        Numeric.ThrowIfNotFinite(baselineTotalProductionManDays, nameof(baselineTotalProductionManDays));
        var factory = GetFactory(factoryId);
        if (factory is null || !factory.IsOperational || factory.Line is null || factory.ExpansionMonthsRemaining > 0)
        {
            cost = 0d;
            message = "The selected factory must be operational before equipment can be purchased.";
            return false;
        }
        if (GetEquipmentForFactory(factory.FactoryId) is not null)
        {
            cost = 0d;
            message = "Move the currently installed equipment to the Central Equipment Depot first.";
            return false;
        }

        cost = FactoryBalance.GetEquipmentPurchaseCost(tier, factory.BuildingSize);
        if (availableCash < cost)
        {
            message = $"Equipment purchase requires {FinancialValueFormatter.FormatNumber(cost)} cash.";
            return false;
        }

        var equipmentId = $"equipment.{_nextEquipmentSequence++:D4}";
        var normalizedDesignId = designId.Trim();
        var equipment = new FactoryEquipmentState(
            equipmentId,
            normalizedDesignId,
            tier,
            cost,
            factory.FactoryId,
            factory.Line.LineId,
            FactoryBalance.GetInitialInstallationMonths(tier));
        _equipmentById.Add(equipmentId, equipment);
        equipment.BuildingSize = factory.BuildingSize;
        equipment.BaselineProductionManDays = System.Math.Max(baselineTotalProductionManDays, Numeric.Epsilon);
        factory.BaselineAircraftManDays = System.Math.Max(baselineTotalProductionManDays, Numeric.Epsilon);
        AssignLineDesign(factory.Line, normalizedDesignId, 0);
        message = equipment.InstallationMonthsRemaining == 0
            ? $"Equipment {equipmentId} purchased and ready in {factory.DisplayName}."
            : $"Equipment {equipmentId} purchased. Installation in {factory.DisplayName} takes {equipment.InstallationMonthsRemaining} months.";
        return true;
    }

    public bool TryStoreInstalledEquipment(string factoryId, out string message)
    {
        var factory = GetFactory(factoryId);
        var equipment = factory is null ? null : GetEquipmentForFactory(factory.FactoryId);
        if (factory?.Line is null || equipment is null)
        {
            message = "No equipment is installed in the selected factory.";
            return false;
        }
        if (!equipment.IsReady)
        {
            message = "Equipment cannot be moved while installation is in progress.";
            return false;
        }
        // Carried labor and partial spare-part materials/work have already been charged.
        // Cancel their unfinished output without a refund or any additional production expense.
        _supportOrders.RemoveAll(order => order.FactoryId == factory.FactoryId);

        equipment.FactoryId = null;
        equipment.LineId = null;
        equipment.InstallationMonthsRemaining = 0;
        AssignLineDesign(factory.Line, null, 0);
        message = $"Equipment moved from {factory.DisplayName} to the Central Equipment Depot at no cost; paid work in progress was discarded without refund.";
        return true;
    }

    public bool TryReinstallEquipment(
        string equipmentId,
        string factoryId,
        double availableCash,
        out double cost,
        out string message)
    {
        cost = 0d;
        if (!_equipmentById.TryGetValue(equipmentId?.Trim() ?? string.Empty, out var equipment) || equipment.IsInstalled)
        {
            message = "Stored equipment was not found in the Central Equipment Depot.";
            return false;
        }
        var factory = GetFactory(factoryId);
        if (factory is null || !factory.IsOperational || factory.Line is null)
        {
            message = "The selected factory is not operational.";
            return false;
        }
        if (GetEquipmentForFactory(factory.FactoryId) is not null)
        {
            message = "The selected factory line already contains equipment.";
            return false;
        }
        if (factory.ExpansionMonthsRemaining > 0 || factory.BuildingSize != equipment.BuildingSize)
        {
            message = $"Equipment requires an available {equipment.BuildingSize} factory of the same size.";
            return false;
        }

        cost = equipment.PurchaseCost * FactoryBalance.EquipmentReinstallationCostShare;
        if (availableCash < cost)
        {
            message = $"Reinstallation requires {FinancialValueFormatter.FormatNumber(cost)} cash.";
            return false;
        }

        equipment.FactoryId = factory.FactoryId;
        equipment.LineId = factory.Line.LineId;
        equipment.InstallationMonthsRemaining = FactoryBalance.GetReinstallationMonths(equipment.AutomationTier);
        AssignLineDesign(factory.Line, equipment.DesignId, equipment.LifetimeProducedUnits);
        factory.BaselineAircraftManDays = equipment.BaselineProductionManDays;
        message = $"Equipment reinstallation started in {factory.DisplayName} ({equipment.InstallationMonthsRemaining} months).";
        return true;
    }

    public bool TryScrapStoredEquipment(string equipmentId, out double income, out string message)
    {
        income = 0d;
        if (!_equipmentById.TryGetValue(equipmentId?.Trim() ?? string.Empty, out var equipment) || equipment.IsInstalled)
        {
            message = "Only equipment stored in the Central Equipment Depot can be sold.";
            return false;
        }
        income = equipment.PurchaseCost * FactoryBalance.EquipmentScrapRecoveryShare;
        _equipmentById.Remove(equipment.EquipmentId);
        message = "Stored equipment sold.";
        return true;
    }

    public bool TrySellFactory(string factoryId, out double income, out string message)
    {
        income = 0d;
        var factory = GetFactory(factoryId);
        if (factory is null || !factory.IsOperational)
        {
            message = "Only a completed factory can be sold.";
            return false;
        }
        if (GetEquipmentForFactory(factory.FactoryId) is not null)
        {
            message = "Move the installed equipment to the Central Equipment Depot before selling this factory.";
            return false;
        }

        income = GetFactoryResaleValue(factory.FactoryId);
        _factories.Remove(factory);
        message = $"{factory.DisplayName} sold.";
        return true;
    }

    public double GetFactoryResaleValue(string factoryId)
    {
        var factory = GetFactory(factoryId);
        return factory is null ? 0d : FactoryBalance.GetBuildingResaleValue(factory.BuildingSize,
            factory.ConstructionMonthsRemaining, factory.ExpansionMonthsRemaining, factory.OperatingMonths);
    }

    public double GetMonthlyFactoryMaintenance() => _factories.Where(item => item.IsOperational)
        .Sum(item => FactoryBalance.GetBuildingMonthlyMaintenance(item.BuildingSize));

    public double GetMonthlyActiveEquipmentCost()
    {
        return _equipmentById.Values.Sum(equipment =>
        {
            if (!equipment.IsReady || string.IsNullOrWhiteSpace(equipment.FactoryId)) return 0d;
            var factory = GetFactory(equipment.FactoryId);
            return factory?.Line is not null && factory.Line.MonthlyProductionTargetUnits > 0
                ? GetEquipmentMonthlyActiveCost(equipment)
                : 0d;
        });
    }

    public double GetMonthlyEquipmentStorageCost() => _equipmentById.Values.Where(e => !e.IsInstalled)
        .Sum(e => e.PurchaseCost * FactoryBalance.EquipmentMonthlyStorageCostShare);

    public int GetMaximumMonthlyOutput(string factoryId)
    {
        var site = GetFactory(factoryId);
        var equipment = GetEquipmentForFactory(factoryId);
        if (equipment is null || site is null || site.BaselineAircraftManDays <= Numeric.Epsilon) return 0;
        return System.Math.Max(1, (int)System.Math.Min(FactoryBalance.MaximumAircraftPerLinePerMonth,
            System.Math.Ceiling(site.BuildingCapacityManDays / (site.BaselineAircraftManDays
                * FactoryBalance.GetProductionManDayMultiplier(equipment.AutomationTier)))));
    }

    public bool TrySetMonthlyProductionTargetUnits(string factoryId, int requestedUnits, out int appliedUnits, out string message)
    {
        var factory = GetFactory(factoryId);
        if (factory?.Line is null)
        {
            appliedUnits = 0;
            message = "The selected factory does not have an operational line.";
            return false;
        }
        var equipment = GetEquipmentForFactory(factory.FactoryId);
        if (equipment is null)
        {
            appliedUnits = 0;
            factory.Line.SetMonthlyProductionTargetUnits(0);
            message = "Install type-specific equipment before setting a production target.";
            return false;
        }

        var maximum = GetMaximumMonthlyOutput(factory.FactoryId);
        appliedUnits = System.Math.Clamp(requestedUnits, 0, maximum);
        factory.Line.SetMonthlyProductionTargetUnits(appliedUnits);
        message = appliedUnits == requestedUnits
            ? $"Production target set to {appliedUnits} aircraft per month."
            : $"Production target reduced to the {equipment.AutomationTier} line capacity of {maximum} aircraft per month.";
        return true;
    }

    public void SetMonthlyProductionTargetUnits(string lineId, int targetUnits)
    {
        var factory = GetFactoryForLine(lineId) ?? throw new KeyNotFoundException($"Line '{lineId}' was not found.");
        TrySetMonthlyProductionTargetUnits(factory.FactoryId, targetUnits, out _, out _);
    }

    public void SetTotalProductionTargetUnits(string lineId, int targetUnits) => GetLine(lineId).SetTotalProductionTargetUnits(targetUnits);

    public int StopProductionForDesign(string designId)
    {
        ValidateDesignId(designId);
        var normalizedDesignId = designId.Trim();
        var stoppedLines = 0;
        for (var index = 0; index < _factories.Count; index++)
        {
            var line = _factories[index].Line;
            if (line is null
                || !string.Equals(line.AssignedDesignId, normalizedDesignId, StringComparison.Ordinal))
            {
                continue;
            }

            line.SetMonthlyProductionTargetUnits(0);
            line.SetTotalProductionTargetUnits(0);
            line.SetAutoAssignedEmployees(0);
            stoppedLines++;
        }

        if (AutomaticStockReplenishment && stoppedLines > 0) SetAircraftStockTarget(normalizedDesignId, 0);
        return stoppedLines;
    }

    public int GetWarehouseUnits(string designId)
    {
        if (string.IsNullOrWhiteSpace(designId)) return 0;
        return _warehouseUnitsByDesign.TryGetValue(designId.Trim(), out var units) ? units : 0;
    }

    public void AddWarehouseUnits(string designId, int units)
    {
        ValidateDesignId(designId);
        if (units < 0) throw new ArgumentOutOfRangeException(nameof(units));
        if (units == 0) return;
        var key = designId.Trim();
        _warehouseUnitsByDesign[key] = checked(GetWarehouseUnits(key) + units);
    }

    public bool TryWithdrawFromWarehouse(string designId, int units, out string message)
    {
        ValidateDesignId(designId);
        if (units <= 0) throw new ArgumentOutOfRangeException(nameof(units));
        var key = designId.Trim();
        var available = GetWarehouseUnits(key);
        if (available < units)
        {
            message = $"Warehouse has {available} units, but {units} are required.";
            return false;
        }
        var remaining = available - units;
        if (remaining == 0) _warehouseUnitsByDesign.Remove(key); else _warehouseUnitsByDesign[key] = remaining;
        message = $"Withdrew {units} units. Remaining stock: {remaining}.";
        return true;
    }

    public FactoryMonthlyProductionReport SimulateMonth(
        IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog,
        int monthIndex,
        SimulationDate date)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        if (monthIndex < 0) throw new ArgumentOutOfRangeException(nameof(monthIndex));
        if (_lastAircraftProductionMonth == monthIndex) throw new InvalidOperationException("Aircraft production for this month is already closed.");
        foreach (var item in catalog.Values) item.Validate();
        PrepareProductionMonth(monthIndex, catalog);
        _supportCatalogForMonth ??= catalog;
        // Even voluntary reserves get their reserved share before any unused share is lent to aircraft.
        RunSupportQueue(_supportCatalogForMonth ?? catalog, true);
        RunSupportQueue(_supportCatalogForMonth ?? catalog, false);

        var lineReports = new List<LineProductionReport>();
        var engineReports = new List<EngineProductionReport>();
        var totalAvailableManDays = 0d;
        var totalUsedManDays = 0d;
        var totalProducedAircraft = 0;
        var totalActiveEquipmentExpenses = GetMonthlyEquipmentStorageCost();

        foreach (var factory in _factories.Where(item => item.IsOperational))
        {
            var line = factory.Line!;
            var equipment = GetEquipmentForFactory(factory.FactoryId);
            var configuredTarget = AutomaticStockReplenishment ? GetRequestedAircraftUnits(factory.FactoryId) : line.MonthlyProductionTargetUnits;
            var carryIn = line.CarryOverManDays;
            var availableManDays = 0d;
            var producedUnits = 0;
            var usedManDays = 0d;
            var requiredEngines = 0;
            var engineEmployees = 0;
            var engineDiscarded = 0;
            var capacityUnits = 0;
            var blocked = false;
            var status = "Idle";
            var designName = "Unassigned";
            var baselineLaborExpenses = 0d;
            var actualLaborExpenses = 0d;
            var activeEquipmentExpenses = 0d;
            line.SetAutoAssignedEmployees(0);

            if (equipment is { IsReady: true } && configuredTarget > 0)
            {
                activeEquipmentExpenses = ChargeMonthlyEquipment(equipment);
                totalActiveEquipmentExpenses += activeEquipmentExpenses;
            }

            if (equipment is null || string.IsNullOrWhiteSpace(line.AssignedDesignId))
            {
                blocked = true;
                status = "No type-specific equipment installed.";
                line.SetCarryOverWithoutProduction(0d);
            }
            else if (!equipment.IsReady)
            {
                blocked = true;
                status = AutomaticStockReplenishment
                    ? $"Equipment installation in progress ({equipment.InstallationMonthsRemaining} months remaining). Stock replenishment starts when ready."
                    : $"Equipment installation in progress ({equipment.InstallationMonthsRemaining} months remaining). Planned output {configuredTarget}/{FactoryBalance.GetMaximumMonthlyOutput(equipment.AutomationTier)} is retained.";
                line.SetCarryOverWithoutProduction(0d);
            }
            else if (!catalog.TryGetValue(equipment.DesignId, out var catalogItem))
            {
                blocked = true;
                status = $"Aircraft type '{equipment.DesignId}' is not production-ready.";
                line.SetCarryOverWithoutProduction(carryIn);
            }
            else
            {
                designName = catalogItem.DisplayName;
                var multiplier = FactoryBalance.GetProductionManDayMultiplier(equipment.AutomationTier);
                var baselineAirframeMd = System.Math.Max(
                    catalogItem.Aircraft.Performance.AirframeProductionManDays > Numeric.Epsilon
                        ? catalogItem.Aircraft.Performance.AirframeProductionManDays
                        : catalogItem.Aircraft.Airframe.Performance.ProductionManDays,
                    Numeric.Epsilon);
                var baselineEngineMd = ExternalEngineSupply is null ? System.Math.Max(catalogItem.Aircraft.Performance.EnginesProductionManDays, 0d) : 0d;
                var combinedMd = (baselineAirframeMd + baselineEngineMd) * multiplier;
                var remaining = !AutomaticStockReplenishment && line.TotalProductionTargetUnits > 0
                    ? System.Math.Max(line.TotalProductionTargetUnits - line.LifetimeProducedUnits, 0)
                    : int.MaxValue;
                var monthTarget = configuredTarget;
                monthTarget = System.Math.Min(monthTarget, remaining);
                if (GetAircraftStockTarget(equipment.DesignId) is { } stockTarget)
                    monthTarget = System.Math.Min(monthTarget, System.Math.Max(0, stockTarget - GetWarehouseUnits(equipment.DesignId)));

                if (remaining <= 0)
                {
                    status = "Total target reached. Line stopped; equipment remains installed.";
                    line.SetMonthlyProductionTargetUnits(0);
                    line.SetCarryOverWithoutProduction(0d);
                }
                else if (monthTarget <= 0)
                {
                    status = GetAircraftStockTarget(equipment.DesignId).HasValue ? "Aircraft stock target reached; paid work retained." : "No production target set.";
                    line.SetCarryOverWithoutProduction(carryIn);
                }
                else
                {
                    var requiredMd = System.Math.Max(0d, monthTarget * combinedMd - carryIn);
                    availableManDays = RemainingManDays(factory, false);
                    usedManDays = System.Math.Min(requiredMd, availableManDays);
                    var engineLimitedUnits = monthTarget;
                    if (ExternalEngineSupply is not null)
                    {
                        var enginesPerAircraft = System.Math.Max(1, catalogItem.Aircraft.Airframe.Parameters.EngineCount);
                        engineLimitedUnits = System.Math.Min(engineLimitedUnits,
                            System.Math.Max(0, ExternalEngineSupply.AvailableEngines(equipment.DesignId)) / enginesPerAircraft);
                    }
                    var completableUnits = System.Math.Min(engineLimitedUnits,
                        (int)System.Math.Floor((carryIn + usedManDays + Numeric.Epsilon) / combinedMd));
                    // Only one physical factory-month may remain unfinished. Completed units free
                    // room immediately; reserve/pay only work that can actually be performed.
                    // Preserve already-paid legacy excess, but never add to it.
                    var workBeforeLimit = System.Math.Max(0d,
                        factory.BuildingCapacityManDays + completableUnits * combinedMd - carryIn);
                    usedManDays = System.Math.Min(usedManDays, workBeforeLimit);
                    if (carryIn > factory.BuildingCapacityManDays + Numeric.Epsilon)
                        usedManDays = 0d;
                    ReserveManDays(factory, usedManDays);
                    var employees = (int)System.Math.Ceiling(usedManDays / EmployeeManDaysPerMonth);
                    var engineShare = baselineEngineMd / (baselineAirframeMd + baselineEngineMd);
                    engineEmployees = (int)System.Math.Floor(employees * engineShare);
                    line.SetAutoAssignedEmployees(employees - engineEmployees);
                    capacityUnits = (int)System.Math.Floor((carryIn + usedManDays + Numeric.Epsilon) / combinedMd);
                    producedUnits = System.Math.Min(monthTarget, capacityUnits);
                    if (ExternalEngineSupply is not null)
                    {
                        var engineCount = System.Math.Max(1, catalogItem.Aircraft.Airframe.Parameters.EngineCount);
                        producedUnits = System.Math.Min(producedUnits, System.Math.Max(0, ExternalEngineSupply.AvailableEngines(equipment.DesignId)) / engineCount);
                        if (producedUnits > 0 && !ExternalEngineSupply.TryConsumeEngines(equipment.DesignId, checked(producedUnits * engineCount)))
                            throw new InvalidOperationException("External engine stock changed while completing aircraft.");
                    }
                    line.ApplyMonthResult(producedUnits, System.Math.Max(0d, carryIn + usedManDays - producedUnits * combinedMd));
                    equipment.LifetimeProducedUnits = line.LifetimeProducedUnits;
                    AddWarehouseUnits(equipment.DesignId, producedUnits);
                    status = $"Produced {producedUnits} aircraft using {equipment.AutomationTier}.";
                    if (line.CarryOverManDays >= factory.BuildingCapacityManDays - Numeric.Epsilon
                        && usedManDays < System.Math.Min(requiredMd, availableManDays) - Numeric.Epsilon)
                        status += " Unfinished-work limit reached (one factory-month); further work paused.";
                    if (!AutomaticStockReplenishment && line.TotalProductionTargetUnits > 0 && line.LifetimeProducedUnits >= line.TotalProductionTargetUnits)
                    {
                        line.SetMonthlyProductionTargetUnits(0);
                        status += " Total target reached; line stopped.";
                    }

                    requiredEngines = ExternalEngineSupply is null ? producedUnits * catalogItem.Aircraft.Airframe.Parameters.EngineCount : 0;
                    var baselineEngineMdTotal = baselineEngineMd * producedUnits;
                    if (baselineEngineMd > 0d && usedManDays > 0d)
                    {
                        var engineMd = System.Math.Max(
                            catalogItem.Aircraft.Performance.EnginesProductionManDays /
                            System.Math.Max(catalogItem.Aircraft.Airframe.Parameters.EngineCount, 1),
                            Numeric.Epsilon) * multiplier;
                        engineDiscarded = 0;
                        engineReports.Add(new EngineProductionReport(
                            line.LineId,
                            catalogItem.Aircraft.Engine.Definition.DisplayName,
                            requiredEngines,
                            requiredEngines,
                            engineDiscarded,
                            engineEmployees,
                            usedManDays * engineShare,
                            availableManDays * engineShare));
                    }

                    var baselinePlaneEmployees = (int)System.Math.Ceiling(producedUnits * baselineAirframeMd / EmployeeManDaysPerMonth);
                    var baselineEngineEmployees = (int)System.Math.Ceiling(baselineEngineMdTotal / EmployeeManDaysPerMonth);
                    baselineLaborExpenses = (baselinePlaneEmployees + baselineEngineEmployees) * FactoryBalance.PlaneStaffMonthlyCostPerEmployee;
                    actualLaborExpenses = (line.Employees + engineEmployees) * FactoryBalance.PlaneStaffMonthlyCostPerEmployee;
                    equipment.CumulativeBaselineLaborExpenses += baselineLaborExpenses;
                    equipment.CumulativeActualLaborExpenses += actualLaborExpenses;
                }
            }

            totalAvailableManDays += availableManDays;
            totalUsedManDays += usedManDays;
            totalProducedAircraft += producedUnits;
            lineReports.Add(new LineProductionReport(
                factory.FactoryId,
                factory.DisplayName,
                line.LineId,
                line.AssignedDesignId,
                designName,
                line.Employees,
                configuredTarget,
                line.TotalProductionTargetUnits,
                line.LifetimeProducedUnits,
                producedUnits,
                availableManDays,
                usedManDays,
                line.CarryOverManDays,
                capacityUnits,
                blocked,
                status,
                requiredEngines,
                engineEmployees,
                engineDiscarded,
                string.IsNullOrWhiteSpace(line.AssignedDesignId) ? 0 : GetWarehouseUnits(line.AssignedDesignId),
                baselineLaborExpenses,
                actualLaborExpenses,
                System.Math.Max(0d, baselineLaborExpenses - actualLaborExpenses),
                activeEquipmentExpenses));
        }

        // Reuse capacity left after aircraft reach their actual monthly/stock target.
        RunSupportQueue(_supportCatalogForMonth ?? catalog, true, borrowUnusedProduction: true);
        RunSupportQueue(_supportCatalogForMonth ?? catalog, false, borrowUnusedProduction: true);
        var engineFactoryReport = new EngineFactoryProductionReport(
            engineReports,
            engineReports.Sum(item => item.RequiredEngines),
            engineReports.Sum(item => item.ProducedEngines),
            engineReports.Sum(item => item.DiscardedEngines),
            engineReports.Sum(item => item.AutoAssignedEmployees),
            engineReports.Sum(item => item.RequiredManDays),
            engineReports.Sum(item => item.CapacityManDays));
        var warehouse = new Dictionary<string, int>(_warehouseUnitsByDesign, StringComparer.Ordinal);
        _lastAircraftProductionMonth = monthIndex;
        var physicalCapacity = _factories.Where(item => item.IsOperational && item.ExpansionMonthsRemaining == 0).Sum(item => item.BuildingCapacityManDays);
        return new FactoryMonthlyProductionReport(
            monthIndex,
            date,
            lineReports,
            engineFactoryReport,
            totalAvailableManDays,
            totalUsedManDays,
            totalAvailableManDays <= Numeric.Epsilon ? 0d : Numeric.Clamp(totalUsedManDays / totalAvailableManDays, 0d, 1d),
            totalProducedAircraft,
            warehouse,
            warehouse.Values.Sum(),
            GetMonthlyFactoryMaintenance(),
            totalActiveEquipmentExpenses)
        {
            TotalPhysicalCapacityManDays = physicalCapacity,
            UsesExternalEngineSupply = ExternalEngineSupply is not null,
            TotalStaffCapacityManDays = _factories.Sum(item => item.AvailableManDays),
            SupportProductionManDays = LastSupportReport.UsedManDays,
            PhysicalCapacityUtilization = physicalCapacity <= Numeric.Epsilon ? 0d
                : Numeric.Clamp((totalUsedManDays + LastSupportReport.UsedManDays) / physicalCapacity, 0d, 1d),
        };
    }

    public FactoryStateSnapshot CreateSnapshot()
    {
        var snapshot = new FactoryStateSnapshot
        {
            NextFactorySequence = _nextFactorySequence,
            NextEquipmentSequence = _nextEquipmentSequence,
            UsesExternalEngineSupply = UsesExternalEngineSupply,
            AutomaticStockReplenishment = AutomaticStockReplenishment,
        };
        WriteSupportSnapshot(snapshot);
        foreach (var factory in _factories)
        {
            snapshot.Factories.Add(new FactorySiteSnapshot
            {
                FactoryId = factory.FactoryId,
                DisplayName = factory.DisplayName,
                ConstructionMonthsRemaining = factory.ConstructionMonthsRemaining,
                OperatingMonths = factory.OperatingMonths,
                BuildingSize = (int)factory.BuildingSize,
                CapacitySchemaVersion = 2,
                LegacyCapacityMd = factory.LegacyCapacityMd,
                NeedsLegacyCapacityMigration = factory.NeedsLegacyCapacityMigration,
                BaselineAircraftManDays = factory.BaselineAircraftManDays,
                WorkforceEmployeeLimit = factory.WorkforceEmployeeLimit,
                PartsAllocationPercent = factory.PartsAllocationPercent,
                ExpansionMonthsRemaining = factory.ExpansionMonthsRemaining,
                Line = factory.Line is null ? null : CreateLineSnapshot(factory.Line),
            });
        }
        foreach (var entry in _warehouseUnitsByDesign)
        {
            snapshot.WarehouseEntries.Add(new FactoryWarehouseEntrySnapshot { DesignId = entry.Key, Units = entry.Value });
        }
        foreach (var equipment in _equipmentById.Values)
        {
            snapshot.Equipment.Add(new FactoryEquipmentSnapshot
            {
                EquipmentId = equipment.EquipmentId,
                DesignId = equipment.DesignId,
                AutomationTier = (int)equipment.AutomationTier,
                PurchaseCost = equipment.PurchaseCost,
                BuildingSize = (int)equipment.BuildingSize,
                BaselineProductionManDays = equipment.BaselineProductionManDays,
                FactoryId = equipment.FactoryId,
                LineId = equipment.LineId,
                InstallationMonthsRemaining = equipment.InstallationMonthsRemaining,
                LifetimeProducedUnits = equipment.LifetimeProducedUnits,
                CumulativeBaselineLaborExpenses = equipment.CumulativeBaselineLaborExpenses,
                CumulativeActualLaborExpenses = equipment.CumulativeActualLaborExpenses,
                CumulativeActiveEquipmentExpenses = equipment.CumulativeActiveEquipmentExpenses,
                IsInstalled = equipment.IsInstalled,
            });
        }
        return snapshot;
    }

    public void RestoreSnapshot(FactoryStateSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        _factories.Clear();
        _equipmentById.Clear();
        _warehouseUnitsByDesign.Clear();
        AutomaticStockReplenishment = snapshot.AutomaticStockReplenishment;
        RestoreSupportSnapshot(snapshot);
        _nextFactorySequence = System.Math.Max(snapshot.NextFactorySequence, 1);
        _nextEquipmentSequence = System.Math.Max(snapshot.NextEquipmentSequence, 1);

        var hasNetworkSnapshot = snapshot.Factories is { Count: > 0 };
        if (hasNetworkSnapshot)
        {
            foreach (var item in snapshot.Factories)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.FactoryId)) continue;
                var constructionMonths = System.Math.Max(item.ConstructionMonthsRemaining, 0);
                var line = constructionMonths == 0
                    ? RestoreLine(item.Line, BuildLineId(item.FactoryId))
                    : null;
                _factories.Add(new FactorySiteState(
                    item.FactoryId.Trim(),
                    string.IsNullOrWhiteSpace(item.DisplayName) ? item.FactoryId.Trim() : item.DisplayName.Trim(),
                    constructionMonths,
                    System.Math.Max(item.OperatingMonths, 0),
                    line));
                var restoredSite = _factories[_factories.Count - 1];
                restoredSite.BuildingSize = (FactoryBuildingSize)System.Math.Clamp(item.BuildingSize, 0, 2);
                // Apply the capacity rebalance to migrated buildings once, without changing paid WIP.
                restoredSite.LegacyCapacityMd = System.Math.Max(0d, item.LegacyCapacityMd)
                    * (item.CapacitySchemaVersion < 2 ? FactoryBalance.CapacityReductionMultiplier : 1d);
                restoredSite.NeedsLegacyCapacityMigration = item.CapacitySchemaVersion == 0 || item.NeedsLegacyCapacityMigration;
                if (item.CapacitySchemaVersion == 0 && line is not null) line.SetCarryOverWithoutProduction(0d);
                restoredSite.BaselineAircraftManDays = System.Math.Max(0d, item.BaselineAircraftManDays);
                restoredSite.WorkforceEmployeeLimit = item.CapacitySchemaVersion == 0 ? int.MaxValue : System.Math.Max(0, item.WorkforceEmployeeLimit);
                restoredSite.PartsAllocationPercent = item.PartsAllocationPercent == 0 ? 20 : System.Math.Clamp(item.PartsAllocationPercent, 1, 100);
                restoredSite.ExpansionMonthsRemaining = System.Math.Max(0, item.ExpansionMonthsRemaining);
            }
        }
        else if (snapshot.HasFactoryAsset)
        {
            const string legacyFactoryId = "factory.001";
            var constructionMonths = System.Math.Max(snapshot.FactoryConstructionMonthsRemaining, 0);
            var line = constructionMonths == 0
                ? RestoreLine(snapshot.Lines?.FirstOrDefault(), BuildLineId(legacyFactoryId))
                : null;
            _factories.Add(new FactorySiteState(
                legacyFactoryId,
                "Factory #1",
                constructionMonths,
                System.Math.Max(snapshot.FactoryOperatingMonths, 0),
                line));
            _nextFactorySequence = System.Math.Max(_nextFactorySequence, 2);
            _factories[_factories.Count - 1].NeedsLegacyCapacityMigration = true;
            if (line is not null) line.SetCarryOverWithoutProduction(0d);
        }

        if (snapshot.Equipment is not null)
        {
            foreach (var item in snapshot.Equipment)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.DesignId)) continue;
                var tier = Enum.IsDefined(typeof(FactoryAutomationTier), item.AutomationTier)
                    ? (FactoryAutomationTier)item.AutomationTier
                    : FactoryAutomationTier.HandBuilt;
                var equipmentId = string.IsNullOrWhiteSpace(item.EquipmentId)
                    ? $"equipment.{_nextEquipmentSequence++:D4}"
                    : item.EquipmentId.Trim();
                if (_equipmentById.ContainsKey(equipmentId)) equipmentId = $"equipment.{_nextEquipmentSequence++:D4}";

                string? factoryId = string.IsNullOrWhiteSpace(item.FactoryId) ? null : item.FactoryId.Trim();
                if (factoryId is null && item.IsInstalled && _factories.Count > 0) factoryId = _factories[0].FactoryId;
                var factory = factoryId is null ? null : GetFactory(factoryId);
                if (factory?.Line is null || GetEquipmentForFactory(factory.FactoryId) is not null) factoryId = null;
                var lineId = factoryId is null ? null : factory!.Line!.LineId;
                var equipment = new FactoryEquipmentState(
                    equipmentId,
                    item.DesignId.Trim(),
                    tier,
                    System.Math.Max(item.PurchaseCost, 0d),
                    factoryId,
                    lineId,
                    factoryId is null ? 0 : System.Math.Max(item.InstallationMonthsRemaining, 0),
                    System.Math.Max(item.LifetimeProducedUnits, 0),
                    System.Math.Max(item.CumulativeBaselineLaborExpenses, 0d),
                    System.Math.Max(item.CumulativeActualLaborExpenses, 0d),
                    System.Math.Max(item.CumulativeActiveEquipmentExpenses, 0d));
                _equipmentById.Add(equipmentId, equipment);
                // Old stored equipment has no recoverable factory location; treat it as Small.
                equipment.BuildingSize = item.BuildingSize.HasValue
                    ? (FactoryBuildingSize)System.Math.Clamp(item.BuildingSize.Value, 0, 2)
                    : factory?.BuildingSize ?? FactoryBuildingSize.Small;
                equipment.BaselineProductionManDays = item.BaselineProductionManDays > 0d
                    ? item.BaselineProductionManDays : factory?.BaselineAircraftManDays > 0d
                    ? factory.BaselineAircraftManDays : equipment.PurchaseCost / FactoryBalance.GetPurchaseCostMultiplier(tier)
                        / ProductionEconomyBalance.LaborCostPerManDay;
                if (factory?.Line is not null)
                {
                    factory.Line.AssignDesign(equipment.DesignId);
                    if (factory.BaselineAircraftManDays <= Numeric.Epsilon)
                        factory.BaselineAircraftManDays = equipment.BaselineProductionManDays;
                }
            }
        }

        if (snapshot.WarehouseEntries is not null)
        {
            foreach (var item in snapshot.WarehouseEntries)
            {
                if (item is not null && !string.IsNullOrWhiteSpace(item.DesignId) && item.Units > 0)
                {
                    _warehouseUnitsByDesign[item.DesignId.Trim()] = item.Units;
                }
            }
        }
    }

    private static FactoryLineSnapshot CreateLineSnapshot(AircraftProductionLineState line) => new()
    {
        LineId = line.LineId,
        AssignedDesignId = line.AssignedDesignId,
        MonthlyProductionTargetUnits = line.MonthlyProductionTargetUnits,
        TotalProductionTargetUnits = line.TotalProductionTargetUnits,
        LifetimeProducedUnits = line.LifetimeProducedUnits,
        Employees = line.Employees,
        CarryOverManDays = line.CarryOverManDays,
    };

    private static AircraftProductionLineState RestoreLine(FactoryLineSnapshot? snapshot, string fallbackLineId)
    {
        var line = new AircraftProductionLineState(fallbackLineId);
        if (snapshot is null) return line;
        line.AssignDesign(snapshot.AssignedDesignId);
        line.SetMonthlyProductionTargetUnits(System.Math.Clamp(snapshot.MonthlyProductionTargetUnits, 0, FactoryBalance.MaximumAircraftPerLinePerMonth));
        line.SetTotalProductionTargetUnits(System.Math.Max(snapshot.TotalProductionTargetUnits, 0));
        line.SetLifetimeProducedUnits(System.Math.Max(snapshot.LifetimeProducedUnits, 0));
        line.SetAutoAssignedEmployees(System.Math.Max(snapshot.Employees, 0));
        line.SetCarryOverWithoutProduction(System.Math.Max(snapshot.CarryOverManDays, 0d));
        return line;
    }

    private static string BuildLineId(string factoryId) => $"{factoryId}.line.1";

    private static double GetEquipmentMonthlyActiveCost(FactoryEquipmentState equipment) =>
        equipment.PurchaseCost * FactoryBalance.GetMonthlyActiveEquipmentCostShare(equipment.AutomationTier);

    private static void AssignLineDesign(AircraftProductionLineState line, string? designId, int lifetimeProducedUnits)
    {
        line.AssignDesign(designId);
        line.SetMonthlyProductionTargetUnits(0);
        line.SetTotalProductionTargetUnits(0);
        line.SetLifetimeProducedUnits(System.Math.Max(lifetimeProducedUnits, 0));
        line.SetCarryOverWithoutProduction(0d);
        line.SetAutoAssignedEmployees(0);
    }

    private FactorySiteState? GetFactoryForLine(string lineId)
    {
        if (string.IsNullOrWhiteSpace(lineId)) return null;
        return _factories.FirstOrDefault(factory => factory.Line is not null && string.Equals(factory.Line.LineId, lineId.Trim(), StringComparison.Ordinal));
    }

    private AircraftProductionLineState GetLine(string lineId) =>
        GetFactoryForLine(lineId)?.Line ?? throw new KeyNotFoundException($"Line '{lineId}' was not found.");

    private static void ValidateDesignId(string designId)
    {
        if (string.IsNullOrWhiteSpace(designId)) throw new ArgumentException("Design id cannot be empty.", nameof(designId));
    }
}
