using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;

namespace WrightTycoon.Game.Launch;

public enum FactoryPlannerTypeFilter { All, Aircraft, Engine }
public enum FactoryPlannerStatusFilter { All, Attention, Ready, Building, Expanding, Installing, Idle }
public enum FactoryPlannerSort { Name, Type, Capacity, Utilization }

public sealed record FactoryPlannerAllocation(bool Measured, double NormalizedProductionFraction,
    double SupportFraction, double FreeFraction);

/// <summary>Current building/setup state with explicitly historical, nullable measured work. Never a capacity forecast.</summary>
public sealed record FactoryPlannerRow(string Id, bool IsEngine, string Name, string Product,
    FactoryBuildingSize Size, FactoryAutomationTier? Automation, double PhysicalMD, double AvailableMD,
    double? ProductionMD, double? SupportMD, string Target, string Actual, string Status, bool Attention)
{
    public bool IsBuilding { get; init; }
    public bool IsExpanding { get; init; }
    public bool IsInstalling { get; init; }
    public int? MeasuredMonth { get; init; }
    public bool IsReady => !IsBuilding && !IsExpanding && !IsInstalling && Automation.HasValue && Status != "No workforce";
    public FactoryPlannerAllocation Allocation => FactoryPlannerProjection.NormalizeAllocation(PhysicalMD, ProductionMD, SupportMD);
    public bool HasMeasuredAllocation => Allocation.Measured;
    public double NormalizedProductionFraction => Allocation.NormalizedProductionFraction;
    public double SupportFraction => Allocation.SupportFraction;
    public double FreeFraction => Allocation.FreeFraction;
}

/// <summary>Read-only planner projection and bar/filter rules, independent of Unity and rendering.</summary>
public static class FactoryPlannerProjection
{
    public static FactoryPlannerAllocation NormalizeAllocation(double physicalMD, double? productionMD, double? supportMD)
    {
        if (!Finite(physicalMD) || physicalMD <= 0 || !productionMD.HasValue || !supportMD.HasValue
            || !Finite(productionMD.Value) || !Finite(supportMD.Value)) return new FactoryPlannerAllocation(false, 0, 0, 0);
        var production = System.Math.Min(1d, System.Math.Max(0d, productionMD.Value) / physicalMD);
        var support = System.Math.Min(1d - production, System.Math.Max(0d, supportMD.Value) / physicalMD);
        return new FactoryPlannerAllocation(true, production, support, System.Math.Max(0d, 1d - production - support));
    }

    public static IReadOnlyList<FactoryPlannerRow> Build(AircraftProductionFactory? aircraft,
        EngineManufacturingNetwork? engines, FactoryMonthlyProductionReport? airReport = null,
        EngineManufacturingMonthReport? engineReport = null, Func<string, string>? aircraftName = null,
        Func<string, string>? engineName = null, IReadOnlyDictionary<string, int>? airframePartsTargets = null,
        Func<string, string>? enginePartsGroup = null)
    {
        var result = new List<FactoryPlannerRow>();
        if (aircraft is not null)
        {
            var snapshot = aircraft.CreateSnapshot();
            var budgets = snapshot.MonthlyBudgets.ToDictionary(item => item.FactoryId, item => item.UsedManDays, StringComparer.Ordinal);
            foreach (var site in aircraft.Factories)
            {
                var equipment = aircraft.GetEquipmentForFactory(site.FactoryId);
                var line = airReport?.LineReports.FirstOrDefault(item => item.FactoryId == site.FactoryId);
                // The line report already includes engines; subtract it exactly once from the same month's total budget.
                double? support = line is not null && snapshot.ProductionBudgetMonth == airReport!.MonthIndex
                    ? System.Math.Max(0d, (budgets.TryGetValue(site.FactoryId, out var allocated) ? allocated : 0d) - line.UsedManDays) : null;
                var stockLimit = equipment is null ? null : aircraft.GetAircraftStockTarget(equipment.DesignId);
                var stockFull = stockLimit.HasValue && aircraft.GetWarehouseUnits(equipment!.DesignId) >= stockLimit.Value;
                var requested = aircraft.GetRequestedAircraftUnits(site.FactoryId);
                var partsGroup = equipment is null ? "" : aircraft.ResolveAirframePartsGroup(equipment.DesignId);
                var partsNeeded = airframePartsTargets is not null && airframePartsTargets.TryGetValue(partsGroup, out var partsTarget)
                    && aircraft.GetAirframeSparePartUnits(partsGroup) < partsTarget;
                partsNeeded |= snapshot.SupportWorkOrders.Any(order => order.RemainingUnits > 0
                    && aircraft.ResolveAirframePartsGroup(order.DesignId) == partsGroup);
                var status = Status(site.IsUnderConstruction, site.ExpansionMonthsRemaining > 0,
                    equipment is { IsInstalled: true, IsReady: false }, equipment is not null,
                    site.WorkforceEmployeeLimit == 0 && (requested > 0 || partsNeeded),
                    aircraft.AutomaticStockReplenishment ? requested == 0 && !partsNeeded : (site.Line?.MonthlyProductionTargetUnits ?? 0) == 0 || stockFull);
                result.Add(new FactoryPlannerRow(site.FactoryId, false, site.DisplayName,
                    equipment is null ? "No product" : ResolveName(equipment.DesignId, aircraftName), site.BuildingSize,
                    equipment?.AutomationTier, SafeCapacity(site.BuildingCapacityManDays), SafeCapacity(site.AvailableManDays),
                    line?.UsedManDays, support, aircraft.AutomaticStockReplenishment
                        ? (stockLimit.HasValue ? $"Stock target {stockLimit.Value}" : "Stock target unlimited")
                        : $"{site.Line?.MonthlyProductionTargetUnits ?? 0} aircraft/month" + (stockLimit.HasValue ? $" · stock limit {stockLimit.Value}" : ""),
                    stockLimit.HasValue ? $"Stock {aircraft.GetWarehouseUnits(equipment!.DesignId)} / {stockLimit.Value}" : line is null ? "—" : $"{line.ProducedUnits} aircraft", status, status == "No equipment" || status == "No workforce")
                {
                    IsBuilding = site.IsUnderConstruction, IsExpanding = site.ExpansionMonthsRemaining > 0,
                    IsInstalling = equipment is { IsInstalled: true, IsReady: false }, MeasuredMonth = line is null ? null : airReport!.MonthIndex,
                });
            }
        }
        if (engines is not null)
        {
            var pending = engines.CreateSnapshot().WorkOrders;
            foreach (var site in engines.Sites)
            {
                var installed = !string.IsNullOrWhiteSpace(site.EngineDesignId);
                var partsGroup = installed ? enginePartsGroup?.Invoke(site.EngineDesignId) : null;
                var engineTarget = System.Math.Min(int.MaxValue, engines.Sites.Where(s => s.EngineDesignId == site.EngineDesignId).Sum(s => (long)s.MonthlyEngineTarget));
                var partsTarget = partsGroup is null ? site.PartReserve : System.Math.Min(int.MaxValue,
                    engines.Sites.Where(s => !string.IsNullOrWhiteSpace(s.EngineDesignId)
                        && enginePartsGroup!(s.EngineDesignId) == partsGroup).Sum(s => (long)s.PartReserve));
                var idle = engines.GetEngineUnits(site.EngineDesignId) >= engineTarget
                    && (partsTarget == 0 || partsGroup is not null && engines.GetPartUnits(partsGroup) >= partsTarget)
                    && !pending.Any(w => w.RemainingUnits > 0 && (w.SiteId == site.SiteId
                        || w.SiteId.Length == 0 && (w.IsParts ? w.PartsGroupId == partsGroup : w.EngineDesignId == site.EngineDesignId)));
                var status = Status(!site.IsOperational, site.ExpansionMonthsRemaining > 0, site.InstallationMonthsRemaining > 0,
                    installed, site.WorkforceEmployeeLimit == 0 && (site.MonthlyEngineTarget > 0 || site.PartReserve > 0),
                    idle);
                double? production = engineReport is not null && engineReport.ProductionManDaysBySite.TryGetValue(site.SiteId, out var motorMd) ? motorMd : null;
                double? support = engineReport is not null && engineReport.PartsManDaysBySite.TryGetValue(site.SiteId, out var partMd) ? partMd : null;
                // Only explicit per-site diagnostics may drive the bar; aggregate totals are never distributed.
                result.Add(new FactoryPlannerRow(site.SiteId, true, site.SiteId,
                    installed ? ResolveName(site.EngineDesignId, engineName) : "No product", site.BuildingSize,
                    installed ? site.AutomationTier : null, SafeCapacity(site.BuildingCapacityManDays), SafeCapacity(site.AvailableManDays),
                    production, support, $"Target stock {site.MonthlyEngineTarget} · parts {site.PartReserve}",
                    installed ? $"Stock {engines.GetEngineUnits(site.EngineDesignId)} engines · last month {site.LastProducedEngines} · total built {site.LifetimeProducedEngines}" : "—", status,
                    status == "No equipment" || status == "No workforce")
                { IsBuilding = !site.IsOperational, IsExpanding = site.ExpansionMonthsRemaining > 0, IsInstalling = site.InstallationMonthsRemaining > 0,
                    MeasuredMonth = production.HasValue && support.HasValue ? engineReport!.MonthIndex : null });
            }
        }
        return result.ToArray();
    }

    public static IReadOnlyList<FactoryPlannerRow> FilterAndSort(IEnumerable<FactoryPlannerRow> rows,
        FactoryPlannerTypeFilter type = FactoryPlannerTypeFilter.All, FactoryPlannerStatusFilter status = FactoryPlannerStatusFilter.All,
        FactoryPlannerSort sort = FactoryPlannerSort.Name, bool descending = false)
    {
        if (rows is null) throw new ArgumentNullException(nameof(rows));
        var selected = rows.Where(row => type == FactoryPlannerTypeFilter.All
                || (type == FactoryPlannerTypeFilter.Engine ? row.IsEngine : !row.IsEngine))
            .Where(row => status switch
            {
                FactoryPlannerStatusFilter.Attention => row.Attention,
                FactoryPlannerStatusFilter.Ready => row.IsReady,
                FactoryPlannerStatusFilter.Building => row.IsBuilding,
                FactoryPlannerStatusFilter.Expanding => row.IsExpanding,
                FactoryPlannerStatusFilter.Installing => row.IsInstalling,
                FactoryPlannerStatusFilter.Idle => row.Status == "Idle",
                _ => true,
            });
        IOrderedEnumerable<FactoryPlannerRow> ordered;
        if (sort == FactoryPlannerSort.Utilization)
        {
            var measuredFirst = selected.OrderBy(row => !row.HasMeasuredAllocation);
            ordered = descending ? measuredFirst.ThenByDescending(row => Utilization(row)) : measuredFirst.ThenBy(row => Utilization(row));
        }
        else if (sort == FactoryPlannerSort.Capacity)
            ordered = descending ? selected.OrderByDescending(row => SafeCapacity(row.PhysicalMD)) : selected.OrderBy(row => SafeCapacity(row.PhysicalMD));
        else if (sort == FactoryPlannerSort.Type)
            ordered = descending ? selected.OrderByDescending(row => row.IsEngine) : selected.OrderBy(row => row.IsEngine);
        else ordered = descending ? selected.OrderByDescending(row => row.Name, StringComparer.OrdinalIgnoreCase) : selected.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase);
        return ordered.ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Id, StringComparer.Ordinal).ToArray();
    }

    private static double Utilization(FactoryPlannerRow row) => row.NormalizedProductionFraction + row.SupportFraction;
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static double SafeCapacity(double value) => Finite(value) ? System.Math.Max(0d, value) : 0d;
    private static string ResolveName(string id, Func<string, string>? name) => name?.Invoke(id) is { } result && !string.IsNullOrWhiteSpace(result) ? result : id;
    private static string Status(bool building, bool expanding, bool installing, bool equipped, bool understaffed, bool idle) =>
        building ? "Building" : expanding ? "Expanding" : installing ? "Installing" : !equipped ? "No equipment" : understaffed ? "No workforce" : idle ? "Idle" : "Ready";
}
