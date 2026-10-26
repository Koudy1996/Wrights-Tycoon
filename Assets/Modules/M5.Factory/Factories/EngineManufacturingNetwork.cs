using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Core.Factories;

public sealed record EngineManufacturingCatalogItem(string EngineDesignId, string PartsGroupId, double MaterialCost, double ManDays)
{
    public bool IsPartsGroupAnchor { get; init; }
    public EngineManufacturingCatalogItem(string id, string group, Engine engine)
        : this(id, group, engine.Performance.Cost, engine.Performance.ProductionManDays) { }
}

public sealed record EngineManufacturingDemand(string EngineDesignId, string PartsGroupId, int Engines, int Parts,
    bool Guaranteed = true, bool ForAircraftProduction = false);

public sealed class EngineManufacturingSite
{
    public string SiteId { get; set; } = string.Empty;
    public FactoryBuildingSize BuildingSize { get; set; }
    public int ConstructionMonthsRemaining { get; set; }
    public int OperatingMonths { get; set; }
    public int ExpansionMonthsRemaining { get; set; }
    public int InstallationMonthsRemaining { get; set; }
    public string EngineDesignId { get; set; } = string.Empty;
    public FactoryAutomationTier AutomationTier { get; set; }
    public int MonthlyEngineTarget { get; set; }
    public int PartReserve { get; set; }
    public long LifetimeProducedEngines { get; set; }
    public long LifetimeProducedParts { get; set; }
    public int LastProductionMonth { get; set; } = -1;
    public int LastProducedEngines { get; set; }
    public int LastProducedParts { get; set; }
    private int partsAllocationPercent = 20;
    public int PartsAllocationPercent
    {
        get => partsAllocationPercent;
        set => partsAllocationPercent = System.Math.Max(1, System.Math.Min(100, value));
    }
    public int WorkforceEmployeeLimit { get; set; } = int.MaxValue;
    public double EquipmentCost { get; set; }
    public double BuildingResaleValue => FactoryBalance.GetBuildingResaleValue(BuildingSize, ConstructionMonthsRemaining, ExpansionMonthsRemaining, OperatingMonths) * .5d;
    public bool IsOperational => ConstructionMonthsRemaining == 0;
    public bool IsReady => IsOperational && ExpansionMonthsRemaining == 0 && InstallationMonthsRemaining == 0 && EngineDesignId.Length > 0;
    public double BuildingCapacityManDays => FactoryBalance.GetBuildingCapacityManDays(BuildingSize);
    public double AvailableManDays => IsReady ? System.Math.Min(BuildingCapacityManDays, WorkforceEmployeeLimit * 21d) : 0d;
}

public sealed class EngineManufacturingWorkSnapshot
{
    public string EngineDesignId { get; set; } = string.Empty;
    public string PartsGroupId { get; set; } = string.Empty;
    public bool IsParts { get; set; }
    public int Priority { get; set; }
    public int RemainingUnits { get; set; }
    public string SiteId { get; set; } = string.Empty;
    public double CompletedManDays { get; set; }
    public double UnitManDays { get; set; }
    public double UnitMaterialCost { get; set; }
    public double UnitOverheadCost { get; set; }
}

public sealed class StoredEngineEquipment
{
    public string EquipmentId { get; set; } = string.Empty;
    public string EngineDesignId { get; set; } = string.Empty;
    public FactoryAutomationTier AutomationTier { get; set; }
    public FactoryBuildingSize BuildingSize { get; set; }
    public double PurchaseCost { get; set; }
}

public sealed class EngineManufacturingSnapshot
{
    public int NextEquipmentSequence { get; set; } = 1;
    public List<StoredEngineEquipment> StoredEquipment { get; set; } = new();
    public int NextSiteSequence { get; set; } = 1;
    public int LastProcessedMonth { get; set; } = -1;
    public List<EngineManufacturingSite> Sites { get; set; } = new();
    public Dictionary<string, int> EngineStock { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> PartStock { get; set; } = new(StringComparer.Ordinal);
    public List<EngineManufacturingWorkSnapshot> WorkOrders { get; set; } = new();
    public Dictionary<string, double>? EngineStockCostBasis { get; set; }
    public Dictionary<string, double>? PartStockCostBasis { get; set; }
}

public sealed class EngineManufacturingMonthReport
{
    public int MonthIndex { get; internal set; }
    public double UsedManDays { get; internal set; }
    public double MaterialExpenses { get; internal set; }
    public double LaborExpenses { get; internal set; }
    public double MaintenanceExpenses { get; internal set; }
    public double EquipmentExpenses { get; internal set; }
    public int CompletedEngines { get; internal set; }
    public int CompletedParts { get; internal set; }
    public double CompletedEngineCostBasis { get; internal set; }
    public double CompletedPartCostBasis { get; internal set; }
    public Dictionary<string, int> ProducedEnginesByDesign { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> ProductionManDaysBySite { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> PartsManDaysBySite { get; } = new(StringComparer.Ordinal);
    public double TotalExpenses => MaterialExpenses + LaborExpenses + MaintenanceExpenses + EquipmentExpenses;
}

/// <summary>Independent motor plants. All engines, shared parts and their concrete WIP spend one finite plant budget.</summary>
public sealed class EngineManufacturingNetwork
{
    public const double ConstructionCost = 40_000_000d;
    private readonly List<EngineManufacturingSite> sites = new();
    private readonly List<EngineManufacturingWorkSnapshot> work = new();
    private readonly Dictionary<string, int> engines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> parts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> engineCostBasis = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> partCostBasis = new(StringComparer.Ordinal);
    private int nextSite = 1;
    private int nextEquipment = 1;
    private readonly List<StoredEngineEquipment> storedEquipment = new();
    public IReadOnlyList<StoredEngineEquipment> StoredEquipment => storedEquipment.AsReadOnly();
    public double MonthlyEquipmentStorageCost => storedEquipment.Sum(e => e.PurchaseCost * FactoryBalance.EquipmentMonthlyStorageCostShare);
    private int lastMonth = -1;
    public IReadOnlyList<EngineManufacturingSite> Sites => sites.AsReadOnly();
    public int GetEngineUnits(string id) => engines.TryGetValue(id, out var units) ? units : 0;
    public int GetPartUnits(string groupId) => parts.TryGetValue(groupId, out var units) ? units : 0;
    public bool TryConsumeEngineUnits(string id, int units) => TryConsumeEngineUnits(id, units, out _);
    public bool TryConsumeEngineUnits(string id, int units, out double consumedCost) => Consume(engines, engineCostBasis, id, units, out consumedCost);
    public bool TryConsumePartUnits(string groupId, int units) => TryConsumePartUnits(groupId, units, out _);
    public bool TryConsumePartUnits(string groupId, int units, out double consumedCost) => Consume(parts, partCostBasis, groupId, units, out consumedCost);

    public void InitializeInventoryCostBasis(IReadOnlyDictionary<string, EngineManufacturingCatalogItem> catalog)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        var overheadRate = (Maintenance(FactoryBuildingSize.Small)
            + FactoryBalance.GetEquipmentPurchaseCost(FactoryAutomationTier.HandBuilt, FactoryBuildingSize.Small)
                * FactoryBalance.GetMonthlyActiveEquipmentCostShare(FactoryAutomationTier.HandBuilt))
            / FactoryBalance.GetBuildingCapacityManDays(FactoryBuildingSize.Small);
        foreach (var entry in engines.Where(entry => !engineCostBasis.ContainsKey(entry.Key)))
            if (catalog.TryGetValue(entry.Key, out var item))
                engineCostBasis[entry.Key] = entry.Value * (item.MaterialCost + item.ManDays * (ProductionEconomyBalance.LaborCostPerManDay + overheadRate));
        foreach (var entry in parts.Where(entry => !partCostBasis.ContainsKey(entry.Key)))
        {
            var item = Anchor(catalog, entry.Key);
            if (item is not null) partCostBasis[entry.Key] = entry.Value * .01d
                * (item.MaterialCost + item.ManDays * (ProductionEconomyBalance.LaborCostPerManDay + overheadRate));
        }
    }
    public static double Maintenance(FactoryBuildingSize size) => FactoryBalance.GetBuildingMonthlyMaintenance(size) * .5d;
    public static double ExpansionCost(FactoryBuildingSize size) => FactoryBalance.GetExpansionCost(size) * .5d;

    public bool TryConstruct(double cash, out double cost, out string message)
        => TryConstruct(FactoryBuildingSize.Small, cash, out cost, out message);

    public bool TryConstruct(FactoryBuildingSize size, double cash, out double cost, out string message)
    {
        cost = FactoryBalance.GetConstructionCost(size) * .5d;
        if (cash < cost) { message = "Insufficient cash for motor plant."; return false; }
        sites.Add(new EngineManufacturingSite { SiteId = $"engine-plant.{nextSite++:D3}", BuildingSize = size, ConstructionMonthsRemaining = FactoryBalance.GetConstructionMonths(size) });
        message = $"{size} motor plant construction started ({FactoryBalance.GetConstructionMonths(size)} months)."; return true;
    }
    public bool TryExpand(string siteId, double cash, out double cost, out string message)
    {
        cost = 0; var site = Find(siteId);
        if (!site.IsOperational || site.ExpansionMonthsRemaining > 0 || site.BuildingSize == FactoryBuildingSize.Large)
        { message = "Plant cannot be expanded now."; return false; }
        if (!string.IsNullOrWhiteSpace(site.EngineDesignId))
        { message = "Move equipment to the depot before expanding the motor plant."; return false; }
        cost = ExpansionCost(site.BuildingSize);
        if (cash < cost) { message = "Insufficient cash for motor plant expansion."; return false; }
        site.ExpansionMonthsRemaining = FactoryBalance.GetExpansionMonths(site.BuildingSize);
        message = "Motor plant expansion started; manufacturing pauses until completion."; return true;
    }
    public bool Configure(string siteId, EngineManufacturingCatalogItem item, FactoryAutomationTier tier,
        int monthlyEngineTarget, int partReserve, double cash, out double cost, out string message)
    {
        Validate(item); var site = Find(siteId); cost = 0;
        if (monthlyEngineTarget < 0 || partReserve < 0) throw new ArgumentOutOfRangeException(nameof(monthlyEngineTarget));
        if (!site.IsOperational || site.ExpansionMonthsRemaining > 0 || site.InstallationMonthsRemaining > 0)
        { message = "Wait for construction or equipment installation."; return false; }
        if (site.EngineDesignId != item.EngineDesignId || site.AutomationTier != tier)
        {
            if (work.Any(order => order.SiteId == siteId && order.CompletedManDays > 0))
            { message = "Complete manufacturing WIP before replacing equipment."; return false; }
            cost = FactoryBalance.GetEquipmentPurchaseCost(tier, site.BuildingSize);
            if (cash < cost) { message = "Insufficient cash for motor equipment."; return false; }
            if (!string.IsNullOrWhiteSpace(site.EngineDesignId)) StoreEquipment(site);
            site.EquipmentCost = cost;
            site.EngineDesignId = item.EngineDesignId; site.AutomationTier = tier;
            site.InstallationMonthsRemaining = FactoryBalance.GetInitialInstallationMonths(tier);
        }
        site.MonthlyEngineTarget = monthlyEngineTarget; site.PartReserve = partReserve;
        message = "Motor stock targets and equipment configured."; return true;
    }

    public bool TryStoreEquipment(string siteId, out string message)
    {
        var site = Find(siteId);
        if (!site.IsReady)
        { message = "Complete installation before storing equipment."; return false; }
        StoreEquipment(site);
        message = "Motor equipment moved to the Central Equipment Depot; paid work in progress was discarded without refund.";
        return true;
    }

    private void StoreEquipment(EngineManufacturingSite site)
    {
        storedEquipment.Add(new StoredEngineEquipment { EquipmentId = $"motor-equipment.{nextEquipment++:D4}",
            EngineDesignId = site.EngineDesignId, AutomationTier = site.AutomationTier,
            BuildingSize = site.BuildingSize, PurchaseCost = site.EquipmentCost });
        // Work and proportional materials were charged in ProcessMonth, not on completion.
        // Removing WIP neither refunds paid inputs nor charges them a second time.
        work.RemoveAll(o => o.SiteId == site.SiteId);
        site.EngineDesignId = string.Empty;
        site.EquipmentCost = 0d;
        site.MonthlyEngineTarget = 0;
        site.PartReserve = 0;
    }

    public bool TryReinstallEquipment(string equipmentId, string siteId, double cash, out double cost, out string message)
    {
        cost = 0d;
        var item = storedEquipment.FirstOrDefault(e => e.EquipmentId == equipmentId);
        var site = Find(siteId);
        if (item is null || !site.IsOperational || site.ExpansionMonthsRemaining > 0
            || !string.IsNullOrWhiteSpace(site.EngineDesignId) || site.BuildingSize != item.BuildingSize)
        { message = "Select an empty operational motor plant of the same size as the equipment."; return false; }
        cost = item.PurchaseCost * FactoryBalance.EquipmentReinstallationCostShare;
        if (cash < cost) { message = "Insufficient cash for equipment reinstallation."; return false; }
        site.EngineDesignId = item.EngineDesignId;
        site.AutomationTier = item.AutomationTier;
        site.EquipmentCost = item.PurchaseCost;
        site.InstallationMonthsRemaining = FactoryBalance.GetReinstallationMonths(item.AutomationTier);
        storedEquipment.Remove(item);
        message = "Motor equipment reinstallation started.";
        return true;
    }

    public bool TryScrapStoredEquipment(string equipmentId, out double income)
    {
        var item = storedEquipment.FirstOrDefault(e => e.EquipmentId == equipmentId);
        income = item is null ? 0d : item.PurchaseCost * FactoryBalance.EquipmentScrapRecoveryShare;
        return item is not null && storedEquipment.Remove(item);
    }
    public void SetWorkforceEmployeeLimit(string id, int employees)
    { if (employees < 0) throw new ArgumentOutOfRangeException(nameof(employees)); Find(id).WorkforceEmployeeLimit = employees; }
    public void SetPartsAllocationPercent(string siteId, int percent) => Find(siteId).PartsAllocationPercent = percent;
    public void AdvanceMonth()
    {
        foreach (var site in sites)
        {
            if (site.IsOperational) site.OperatingMonths++;
            if (site.ConstructionMonthsRemaining > 0) site.ConstructionMonthsRemaining--;
            if (site.InstallationMonthsRemaining > 0) site.InstallationMonthsRemaining--;
            if (site.ExpansionMonthsRemaining > 0 && --site.ExpansionMonthsRemaining == 0) site.BuildingSize++;
        }
    }

    public EngineManufacturingMonthReport ProcessMonth(int monthIndex,
        IReadOnlyDictionary<string, EngineManufacturingCatalogItem> catalog, IEnumerable<EngineManufacturingDemand> demands)
    {
        if (monthIndex < 0 || monthIndex <= lastMonth) throw new InvalidOperationException("Motor manufacturing month already processed or out of order.");
        if (catalog is null || demands is null) throw new ArgumentNullException(nameof(catalog));
        foreach (var item in catalog.Values) Validate(item);
        InitializeInventoryCostBasis(catalog);
        var requested = demands.ToList();
        ReconcileMonthlyTargets(catalog, requested);
        var report = new EngineManufacturingMonthReport { MonthIndex = monthIndex, MaintenanceExpenses = sites.Where(s => s.IsOperational).Sum(s => Maintenance(s.BuildingSize)), EquipmentExpenses = MonthlyEquipmentStorageCost };
        foreach (var site in sites)
        {
            site.LastProductionMonth = monthIndex;
            site.LastProducedEngines = site.LastProducedParts = 0;
        }
        foreach (var group in requested.GroupBy(d => d.EngineDesignId, StringComparer.Ordinal))
        {
            if (!catalog.TryGetValue(group.Key, out var item)) continue;
            var total = 0;
            for (var priority = 0; priority < 3; priority++)
            {
                total = SaturateTarget((long)total + SumTargets(group.Where(d => Priority(d) == priority).Select(d => d.Engines)));
                EnsureTarget(item, false, total, priority);
            }
        }
        foreach (var group in requested.GroupBy(d => d.PartsGroupId, StringComparer.Ordinal))
        {
            var anchor = Anchor(catalog, group.Key); if (anchor is null) continue;
            var total = 0;
            for (var priority = 0; priority < 3; priority++)
            {
                total = SaturateTarget((long)total + SumTargets(group.Where(d => Priority(d) == priority).Select(d => d.Parts)));
                EnsureTarget(anchor, true, total, priority);
            }
        }
        foreach (var group in sites.Where(s => catalog.ContainsKey(s.EngineDesignId)).GroupBy(s => s.EngineDesignId))
        {
            var item = catalog[group.Key];
            EnsureTarget(item, false, System.Math.Max(SumTargets(group.Select(s => s.MonthlyEngineTarget)), SumTargets(requested.Where(d => d.EngineDesignId == group.Key).Select(d => d.Engines))), 2);
        }
        foreach (var group in sites.Where(s => catalog.ContainsKey(s.EngineDesignId)).GroupBy(s => catalog[s.EngineDesignId].PartsGroupId))
        {
            var anchor = Anchor(catalog, group.Key); if (anchor is not null)
                EnsureTarget(anchor, true, System.Math.Max(SumTargets(group.Select(s => s.PartReserve)), SumTargets(requested.Where(d => d.PartsGroupId == group.Key).Select(d => d.Parts))), 2);
        }
        var used = sites.ToDictionary(s => s.SiteId, _ => 0d, StringComparer.Ordinal);
        var charged = new HashSet<string>(StringComparer.Ordinal);
        // Reserve the configured parts share, fulfil engines, then lend any remainder back to parts.
        // Maintenance/assembly/reserve priorities are independent of warranty status.
        for (var pass = 0; pass < 2; pass++)
        foreach (var order in work.Where(w => pass == 0 || w.IsParts).OrderByDescending(w => w.IsParts).ThenBy(w => w.Priority).ToArray())
        {
            if (!catalog.TryGetValue(order.EngineDesignId, out var recipe)) continue;
            foreach (var site in sites.Where(s => s.IsReady && catalog.ContainsKey(s.EngineDesignId)
                && (order.IsParts ? catalog[s.EngineDesignId].PartsGroupId == order.PartsGroupId : s.EngineDesignId == order.EngineDesignId))
                .OrderBy(s => s.SiteId, StringComparer.Ordinal))
            {
                if (order.SiteId.Length > 0 && order.SiteId != site.SiteId && order.CompletedManDays > 0) continue;
                if (order.CompletedManDays <= 0)
                {
                    order.SiteId = site.SiteId;
                    order.UnitManDays = recipe.ManDays * (order.IsParts ? .01d : 1d) * FactoryBalance.GetEngineProductionManDayMultiplier(site.AutomationTier);
                    order.UnitMaterialCost = recipe.MaterialCost * (order.IsParts ? .01d : 1d);
                    order.UnitOverheadCost = order.UnitManDays * (Maintenance(site.BuildingSize)
                        + site.EquipmentCost * FactoryBalance.GetMonthlyActiveEquipmentCostShare(site.AutomationTier)) / site.BuildingCapacityManDays;
                }
                var partsQuota = site.AvailableManDays * site.PartsAllocationPercent / 100d;
                var actualPartsWork = report.PartsManDaysBySite.TryGetValue(site.SiteId, out var partsWorked) ? partsWorked : 0d;
                var categoryQuota = order.IsParts ? (pass == 0 ? partsQuota : site.AvailableManDays) : site.AvailableManDays - actualPartsWork;
                var categoryWork = order.IsParts ? report.PartsManDaysBySite : report.ProductionManDaysBySite;
                var usedCategory = categoryWork.TryGetValue(site.SiteId, out var allocated) ? allocated : 0d;
                var remainingCategory = System.Math.Max(0d, categoryQuota - usedCategory);
                var worked = System.Math.Min(System.Math.Min(remainingCategory,
                    System.Math.Max(0, site.AvailableManDays - used[site.SiteId])), order.RemainingUnits * order.UnitManDays - order.CompletedManDays);
                if (worked <= 0) continue;
                used[site.SiteId] += worked; report.UsedManDays += worked;
                if (!report.ProductionManDaysBySite.ContainsKey(site.SiteId)) report.ProductionManDaysBySite[site.SiteId] = 0d;
                if (!report.PartsManDaysBySite.ContainsKey(site.SiteId)) report.PartsManDaysBySite[site.SiteId] = 0d;
                if (order.IsParts) report.PartsManDaysBySite[site.SiteId] += worked;
                else report.ProductionManDaysBySite[site.SiteId] += worked;
                report.LaborExpenses += worked * ProductionEconomyBalance.LaborCostPerManDay;
                report.MaterialExpenses += worked / order.UnitManDays * order.UnitMaterialCost;
                if (charged.Add(site.SiteId)) report.EquipmentExpenses += site.EquipmentCost * FactoryBalance.GetMonthlyActiveEquipmentCostShare(site.AutomationTier);
                order.CompletedManDays += worked;
                var completed = System.Math.Min(order.RemainingUnits, (int)System.Math.Floor((order.CompletedManDays + 1e-7) / order.UnitManDays));
                order.CompletedManDays = System.Math.Max(0, order.CompletedManDays - completed * order.UnitManDays); order.RemainingUnits -= completed;
                var stock = order.IsParts ? parts : engines; var key = order.IsParts ? order.PartsGroupId : order.EngineDesignId;
                var basis = order.IsParts ? partCostBasis : engineCostBasis;
                var previousUnits = stock.TryGetValue(key, out var current) ? current : 0;
                if (previousUnits > 0 && !basis.ContainsKey(key)) throw new InvalidOperationException("Initialize motor inventory cost basis before production.");
                var completedCost = completed * (order.UnitMaterialCost + order.UnitManDays * ProductionEconomyBalance.LaborCostPerManDay + order.UnitOverheadCost);
                basis[key] = (basis.TryGetValue(key, out var previousCost) ? previousCost : 0d) + completedCost;
                stock[key] = checked(previousUnits + completed);
                if (order.IsParts) report.CompletedPartCostBasis += completedCost;
                else report.CompletedEngineCostBasis += completedCost;
                if (order.IsParts)
                {
                    report.CompletedParts += completed;
                    site.LastProducedParts = checked(site.LastProducedParts + completed);
                    site.LifetimeProducedParts = checked(site.LifetimeProducedParts + completed);
                }
                else
                {
                    report.CompletedEngines += completed;
                    site.LastProducedEngines = checked(site.LastProducedEngines + completed);
                    site.LifetimeProducedEngines = checked(site.LifetimeProducedEngines + completed);
                    report.ProducedEnginesByDesign[order.EngineDesignId] = checked(
                        (report.ProducedEnginesByDesign.TryGetValue(order.EngineDesignId, out var previous) ? previous : 0) + completed);
                }
                if (order.RemainingUnits == 0) { work.Remove(order); break; }
            }
        }
        lastMonth = monthIndex; return report;
    }

    // Targets describe current shared stock goals, not irrevocable orders. Only an already-paid first unit survives
    // cancellation; untouched units are replanned from the current goals and current real demand each month.
    private void ReconcileMonthlyTargets(IReadOnlyDictionary<string, EngineManufacturingCatalogItem> catalog,
        IReadOnlyList<EngineManufacturingDemand> requested)
    {
        var engineTargets = sites.Where(site => catalog.ContainsKey(site.EngineDesignId))
            .GroupBy(site => site.EngineDesignId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => SumTargets(group.Select(site => site.MonthlyEngineTarget)), StringComparer.Ordinal);
        var partTargets = sites.Where(site => catalog.ContainsKey(site.EngineDesignId))
            .GroupBy(site => catalog[site.EngineDesignId].PartsGroupId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => SumTargets(group.Select(site => site.PartReserve)), StringComparer.Ordinal);
        foreach (var group in requested.Where(demand => catalog.ContainsKey(demand.EngineDesignId)).GroupBy(demand => demand.EngineDesignId, StringComparer.Ordinal))
            engineTargets[group.Key] = System.Math.Max(engineTargets.TryGetValue(group.Key, out var target) ? target : 0,
                SumTargets(group.Select(demand => demand.Engines)));
        foreach (var group in requested.Where(demand => Anchor(catalog, demand.PartsGroupId) is not null).GroupBy(demand => demand.PartsGroupId, StringComparer.Ordinal))
            partTargets[group.Key] = System.Math.Max(partTargets.TryGetValue(group.Key, out var target) ? target : 0,
                SumTargets(group.Select(demand => demand.Parts)));

        foreach (var group in work.GroupBy(order => new { order.IsParts, Key = order.IsParts ? order.PartsGroupId : order.EngineDesignId }).ToArray())
        {
            var targets = group.Key.IsParts ? partTargets : engineTargets;
            var stock = group.Key.IsParts ? GetPartUnits(group.Key.Key) : GetEngineUnits(group.Key.Key);
            var desired = System.Math.Max(0, (targets.TryGetValue(group.Key.Key, out var target) ? target : 0) - stock);
            var orders = group.ToArray();
            var paidUnits = orders.Count(order => order.CompletedManDays > 0d);
            var unstartedToKeep = System.Math.Max(0, desired - paidUnits);
            foreach (var order in orders)
            {
                var started = order.CompletedManDays > 0d ? 1 : 0;
                var unstarted = System.Math.Min(order.RemainingUnits - started, unstartedToKeep);
                unstartedToKeep -= unstarted;
                order.RemainingUnits = started + unstarted;
                // Priorities reflect this month's obligations, not a demand which may already have disappeared.
                order.Priority = 2;
                if (order.RemainingUnits == 0) work.Remove(order);
            }
        }
    }

    private void EnsureTarget(EngineManufacturingCatalogItem item, bool isParts, int desired, int priority)
    {
        var pending = work.Where(w => w.IsParts == isParts && (isParts ? w.PartsGroupId == item.PartsGroupId : w.EngineDesignId == item.EngineDesignId)).ToArray();
        var stock = isParts ? GetPartUnits(item.PartsGroupId) : GetEngineUnits(item.EngineDesignId);
        var deficit = SaturateTarget((long)desired - stock - pending.Sum(w => (long)w.RemainingUnits));
        if (deficit > 0) work.Add(new EngineManufacturingWorkSnapshot
        { EngineDesignId = item.EngineDesignId, PartsGroupId = item.PartsGroupId, IsParts = isParts, Priority = priority, RemainingUnits = deficit });
        var promote = SaturateTarget((long)desired - stock - work.Where(w => w.IsParts == isParts
            && (isParts ? w.PartsGroupId == item.PartsGroupId : w.EngineDesignId == item.EngineDesignId) && w.Priority <= priority).Sum(w => (long)w.RemainingUnits));
        foreach (var order in pending.Where(w => w.Priority > priority))
        {
            if (promote == 0) break;
            var count = System.Math.Min(promote, order.RemainingUnits);
            if (count == order.RemainingUnits) order.Priority = priority;
            else { var split = Clone(order); split.Priority = priority; split.RemainingUnits = count; work.Add(split); order.RemainingUnits -= count; order.CompletedManDays = 0; }
            promote -= count;
        }
    }
    private static int SaturateTarget(long value) => (int)System.Math.Max(0L, System.Math.Min(int.MaxValue, value));
    private static int SumTargets(IEnumerable<int> values)
    {
        long total = 0;
        foreach (var value in values)
        {
            total += System.Math.Max(0, value);
            if (total >= int.MaxValue) return int.MaxValue;
        }
        return (int)total;
    }
    public IReadOnlyDictionary<string, InventoryValueEntry> GetInventoryValueEntries(IReadOnlyDictionary<string, EngineManufacturingCatalogItem> catalog)
    {
        InitializeInventoryCostBasis(catalog);
        var result = new Dictionary<string, InventoryValueEntry>(StringComparer.Ordinal);
        foreach (var entry in engines) if (entry.Value > 0 && engineCostBasis.TryGetValue(entry.Key, out var engineBasis))
            AddValue(result, "motor-engine:" + entry.Key, entry.Value, engineBasis / entry.Value);
        foreach (var entry in parts)
        { if (entry.Value > 0 && partCostBasis.TryGetValue(entry.Key, out var partBasis)) AddValue(result, "motor-part:" + entry.Key, entry.Value, partBasis / entry.Value); }
        var sequence = 0;
        foreach (var order in work.Where(w => w.CompletedManDays > 0 && w.UnitManDays > 0))
            AddValue(result, "motor-wip:" + sequence++, 1, order.CompletedManDays * (ProductionEconomyBalance.LaborCostPerManDay + (order.UnitMaterialCost + order.UnitOverheadCost) / order.UnitManDays));
        return result;
    }
    public EngineManufacturingSnapshot CreateSnapshot() => new()
    {
        NextSiteSequence = nextSite, LastProcessedMonth = lastMonth, Sites = sites.Select(Clone).ToList(), WorkOrders = work.Select(Clone).ToList(),
        NextEquipmentSequence = nextEquipment, StoredEquipment = storedEquipment.Select(Clone).ToList(),
        EngineStock = new Dictionary<string, int>(engines, StringComparer.Ordinal), PartStock = new Dictionary<string, int>(parts, StringComparer.Ordinal),
        EngineStockCostBasis = new Dictionary<string, double>(engineCostBasis, StringComparer.Ordinal), PartStockCostBasis = new Dictionary<string, double>(partCostBasis, StringComparer.Ordinal),
    };
    public void RestoreSnapshot(EngineManufacturingSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        sites.Clear(); work.Clear(); engines.Clear(); parts.Clear(); nextSite = System.Math.Max(1, snapshot.NextSiteSequence); lastMonth = snapshot.LastProcessedMonth;
        storedEquipment.Clear(); engineCostBasis.Clear(); partCostBasis.Clear();
        nextEquipment = System.Math.Max(1, snapshot.NextEquipmentSequence);
        foreach (var item in snapshot.StoredEquipment ?? new List<StoredEngineEquipment>()) storedEquipment.Add(Clone(item));
        foreach (var site in snapshot.Sites ?? new List<EngineManufacturingSite>()) sites.Add(Clone(site));
        foreach (var order in snapshot.WorkOrders ?? new List<EngineManufacturingWorkSnapshot>()) if (order.RemainingUnits > 0) work.Add(Clone(order));
        foreach (var entry in snapshot.EngineStock ?? new Dictionary<string, int>()) engines[entry.Key] = System.Math.Max(0, entry.Value);
        foreach (var entry in snapshot.PartStock ?? new Dictionary<string, int>()) parts[entry.Key] = System.Math.Max(0, entry.Value);
        foreach (var entry in snapshot.EngineStockCostBasis ?? new Dictionary<string, double>())
            if (engines.ContainsKey(entry.Key)) engineCostBasis[entry.Key] = System.Math.Max(0d, entry.Value);
        foreach (var entry in snapshot.PartStockCostBasis ?? new Dictionary<string, double>())
            if (parts.ContainsKey(entry.Key)) partCostBasis[entry.Key] = System.Math.Max(0d, entry.Value);
    }
    private static int Priority(EngineManufacturingDemand demand) => demand.ForAircraftProduction ? 1 : 0;
    private EngineManufacturingSite Find(string id) => sites.FirstOrDefault(s => s.SiteId == id) ?? throw new ArgumentException("Motor plant not found.", nameof(id));
    private static EngineManufacturingCatalogItem? Anchor(IReadOnlyDictionary<string, EngineManufacturingCatalogItem> catalog, string group) =>
        catalog.Values.Where(i => i.PartsGroupId == group).OrderByDescending(i => i.IsPartsGroupAnchor).ThenByDescending(i => i.EngineDesignId == group)
            .ThenBy(i => i.EngineDesignId, StringComparer.Ordinal).FirstOrDefault();
    private static bool Consume(Dictionary<string, int> stock, Dictionary<string, double> basis, string id, int units, out double consumedCost)
    {
        consumedCost = 0d;
        if (string.IsNullOrWhiteSpace(id) || units <= 0) throw new ArgumentOutOfRangeException(nameof(units));
        if (!stock.TryGetValue(id, out var available) || available < units) return false;
        if (!basis.TryGetValue(id, out var totalCost)) throw new InvalidOperationException("Initialize motor inventory cost basis before consumption.");
        consumedCost = totalCost * units / available;
        stock[id] = available - units;
        basis[id] = available == units ? 0d : System.Math.Max(0d, totalCost - consumedCost);
        return true;
    }
    private static void Validate(EngineManufacturingCatalogItem item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.EngineDesignId) || string.IsNullOrWhiteSpace(item.PartsGroupId)
            || double.IsNaN(item.MaterialCost) || double.IsInfinity(item.MaterialCost) || item.MaterialCost < 0
            || double.IsNaN(item.ManDays) || double.IsInfinity(item.ManDays) || item.ManDays <= 0) throw new ArgumentException("Invalid motor manufacturing recipe.");
    }
    private static void AddValue(Dictionary<string, InventoryValueEntry> values, string id, int count, double cost)
    { if (count > 0) values[id] = new InventoryValueEntry(id, count, cost, count * cost); }
    private static EngineManufacturingWorkSnapshot Clone(EngineManufacturingWorkSnapshot s) => new()
    { EngineDesignId=s.EngineDesignId, PartsGroupId=s.PartsGroupId, IsParts=s.IsParts, Priority=s.Priority, RemainingUnits=s.RemainingUnits,
        SiteId=s.SiteId, CompletedManDays=s.CompletedManDays, UnitManDays=s.UnitManDays, UnitMaterialCost=s.UnitMaterialCost, UnitOverheadCost=s.UnitOverheadCost };
    private static EngineManufacturingSite Clone(EngineManufacturingSite s) => new()
    { SiteId=s.SiteId, BuildingSize=s.BuildingSize, ConstructionMonthsRemaining=s.ConstructionMonthsRemaining, ExpansionMonthsRemaining=s.ExpansionMonthsRemaining,
        InstallationMonthsRemaining=s.InstallationMonthsRemaining, EngineDesignId=s.EngineDesignId, AutomationTier=s.AutomationTier,
        MonthlyEngineTarget=s.MonthlyEngineTarget, PartReserve=s.PartReserve, PartsAllocationPercent=s.PartsAllocationPercent,
        WorkforceEmployeeLimit=s.WorkforceEmployeeLimit, EquipmentCost=s.EquipmentCost, OperatingMonths=s.OperatingMonths,
        LifetimeProducedEngines=s.LifetimeProducedEngines, LifetimeProducedParts=s.LifetimeProducedParts,
        LastProductionMonth=s.LastProductionMonth, LastProducedEngines=s.LastProducedEngines, LastProducedParts=s.LastProducedParts };
    private static StoredEngineEquipment Clone(StoredEngineEquipment s) => new()
    { EquipmentId = s.EquipmentId, EngineDesignId = s.EngineDesignId, AutomationTier = s.AutomationTier,
        BuildingSize = s.BuildingSize, PurchaseCost = s.PurchaseCost };
}
