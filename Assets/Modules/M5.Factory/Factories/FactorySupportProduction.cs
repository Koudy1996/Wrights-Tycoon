using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Core.Factories;

public enum FactoryProductionPriority { GuaranteedSupportFirst = 0, AircraftFirst = 1, AllSupportFirst = 2 }

public sealed record FactorySupportRequest(string DesignId, int AirframeSpareParts, bool Guaranteed = true);

public sealed class FactorySupportProductionReport
{
    public int MonthIndex { get; internal set; }
    public double UsedManDays { get; internal set; }
    public double MaterialExpenses { get; internal set; }
    public double LaborExpenses { get; internal set; }
    public double EquipmentExpenses { get; internal set; }
    public double TotalExpenses => MaterialExpenses + LaborExpenses + EquipmentExpenses;
    public int CompletedAirframeSpareParts { get; internal set; }
    public double CompletedInventoryCostBasis { get; internal set; }
}

public sealed partial class AircraftProductionFactory
{
    private readonly Dictionary<string, FactorySupportInventorySnapshot> _supportInventory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _airframePartsAliases = new(StringComparer.Ordinal);

    public string ResolveAirframePartsGroup(string designId) => _airframePartsAliases.TryGetValue(designId, out var group) ? group : designId;

    /// <summary>Reattach family aliases after load. Physical airframe parts move; paid work recipes stay intact.</summary>
    public void SetAirframePartsGroups(SparePartsGroupRegistrySnapshot snapshot)
    {
        var registry = new SparePartsGroupRegistry();
        registry.RestoreSnapshot(snapshot);
        registry.MergeFamilyGroups();
        _airframePartsAliases.Clear();
        foreach (var group in registry.CreateSnapshot().Groups)
            foreach (var member in group.MemberIds) _airframePartsAliases[member] = group.AnchorDesignId;
        foreach (var stock in _supportInventory.Values.ToArray())
        {
            var group = ResolveAirframePartsGroup(stock.DesignId);
            if (group == stock.DesignId || stock.AirframeSpareParts == 0) continue;
            var destination = GetSupportStock(group);
            destination.UnvaluedUnits = checked((destination.TotalCostBasis.HasValue ? destination.UnvaluedUnits : destination.AirframeSpareParts)
                + (stock.TotalCostBasis.HasValue ? stock.UnvaluedUnits : stock.AirframeSpareParts));
            destination.TotalCostBasis = (destination.TotalCostBasis ?? 0d) + (stock.TotalCostBasis ?? 0d);
            destination.AirframeSpareParts = checked(destination.AirframeSpareParts + stock.AirframeSpareParts);
            stock.AirframeSpareParts = 0;
            stock.TotalCostBasis = 0d;
            stock.UnvaluedUnits = 0;
        }
    }

    private bool MatchesSupportDesign(string equipmentDesignId, FactorySupportWorkOrderSnapshot order) =>
        ResolveAirframePartsGroup(equipmentDesignId) == ResolveAirframePartsGroup(order.DesignId);

    private bool MatchesSupportOrder(FactorySupportWorkOrderSnapshot order, string designId) =>
        ResolveAirframePartsGroup(order.DesignId) == ResolveAirframePartsGroup(designId);
    private readonly Dictionary<string, int> _aircraftStockTargets = new(StringComparer.Ordinal);
    private readonly List<FactorySupportWorkOrderSnapshot> _supportOrders = new();
    private readonly Dictionary<string, double> _usedMonthlyManDays = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _usedMonthlySupportManDays = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _monthlyAvailableManDays = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _monthlyPartsAllocationPercent = new(StringComparer.Ordinal);
    private readonly HashSet<string> _chargedEquipmentThisMonth = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem>? _supportCatalogForMonth;
    private int _productionBudgetMonth = -1;
    private int _lastAircraftProductionMonth = -1;
    public FactorySupportProductionReport LastSupportReport { get; private set; } = new();
    public FactoryProductionPriority ProductionPriority { get; set; }

    public int? GetAircraftStockTarget(string designId) => _aircraftStockTargets.TryGetValue(designId, out var target) ? target : null;
    public void SetAircraftStockTarget(string designId, int target)
    {
        ValidateDesignId(designId);
        if (target < 0) throw new ArgumentOutOfRangeException(nameof(target));
        _aircraftStockTargets[designId] = target;
    }
    public void ClearAircraftStockTarget(string designId) => _aircraftStockTargets.Remove(designId);

    public int GetAirframeSparePartUnits(string designId) => _supportInventory.TryGetValue(ResolveAirframePartsGroup(designId), out var stock) ? stock.AirframeSpareParts : 0;
    public double GetMonthlyAllocatedManDays(string factoryId) => _usedMonthlyManDays.TryGetValue(factoryId, out var used) ? used : 0d;
    public double GetMonthlyAllocatedSupportManDays(string factoryId) => _usedMonthlySupportManDays.TryGetValue(factoryId, out var used) ? used : 0d;
    public double GetMonthlyPartsCapacityManDays(string factoryId) => _monthlyAvailableManDays.TryGetValue(factoryId, out var capacity)
        ? capacity * _monthlyPartsAllocationPercent[factoryId] / 100d : GetFactory(factoryId)?.PartsCapacityManDays ?? 0d;
    public double GetMonthlyProductionCapacityManDays(string factoryId) => _monthlyAvailableManDays.TryGetValue(factoryId, out var capacity)
        ? capacity - GetMonthlyPartsCapacityManDays(factoryId) : GetFactory(factoryId)?.ProductionCapacityManDays ?? 0d;
    public void SetPartsAllocationPercent(string factoryId, int percent)
    {
        var site = GetFactory(factoryId) ?? throw new ArgumentException("Factory not found.", nameof(factoryId));
        site.PartsAllocationPercent = System.Math.Clamp(percent, 1, 100);
    }

    public void SetWorkforceEmployeeLimit(string factoryId, int employees)
    {
        var site = GetFactory(factoryId) ?? throw new ArgumentException("Factory not found.", nameof(factoryId));
        if (employees < 0) throw new ArgumentOutOfRangeException(nameof(employees));
        site.WorkforceEmployeeLimit = employees;
    }

    public bool TryStartFactoryExpansion(string factoryId, double availableCash, out double cost, out string message)
    {
        cost = 0d;
        var site = GetFactory(factoryId);
        if (site is null || !site.IsOperational || site.ExpansionMonthsRemaining > 0 || site.BuildingSize == FactoryBuildingSize.Large)
        { message = "Select an operational factory that can be expanded."; return false; }
        if (GetEquipmentForFactory(site.FactoryId) is not null)
        { message = "Move equipment to the depot before expanding the factory."; return false; }
        cost = FactoryBalance.GetExpansionCost(site.BuildingSize);
        if (availableCash < cost) { message = "Insufficient cash for factory expansion."; return false; }
        site.ExpansionMonthsRemaining = FactoryBalance.GetExpansionMonths(site.BuildingSize);
        message = $"Factory expansion started ({site.ExpansionMonthsRemaining} months); production pauses during building work.";
        return true;
    }

    public void InitializeInventoryCostBasis(IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        foreach (var stock in _supportInventory.Values.Where(stock => !stock.TotalCostBasis.HasValue || stock.UnvaluedUnits > 0))
        {
            if (stock.AirframeSpareParts == 0) { stock.TotalCostBasis = 0d; stock.UnvaluedUnits = 0; continue; }
            if (!catalog.TryGetValue(stock.DesignId, out var item)) continue;
            var md = item.Aircraft.Performance.AirframeProductionManDays * FleetServiceBalance.AirframeSparePartFraction;
            var overhead = (FactoryBalance.GetBuildingMonthlyMaintenance(FactoryBuildingSize.Small)
                + FactoryBalance.GetEquipmentPurchaseCost(FactoryAutomationTier.HandBuilt, FactoryBuildingSize.Small)
                    * FactoryBalance.GetMonthlyActiveEquipmentCostShare(FactoryAutomationTier.HandBuilt))
                / FactoryBalance.GetBuildingCapacityManDays(FactoryBuildingSize.Small);
            var unvalued = stock.TotalCostBasis.HasValue ? stock.UnvaluedUnits : stock.AirframeSpareParts;
            stock.TotalCostBasis = (stock.TotalCostBasis ?? 0d) + unvalued * (item.Aircraft.Performance.AirframeProductionCost
                * FleetServiceBalance.AirframeSparePartFraction + md * (ProductionEconomyBalance.LaborCostPerManDay + overhead));
            stock.UnvaluedUnits = 0;
        }
    }

    public bool TryConsumeAirframeSpareParts(string designId, int units) => TryConsumeAirframeSpareParts(designId, units, out _);
    public bool TryConsumeAirframeSpareParts(string designId, int units, out double consumedCost)
    {
        consumedCost = 0d;
        ValidateDesignId(designId);
        if (units <= 0) throw new ArgumentOutOfRangeException(nameof(units));
        if (GetAirframeSparePartUnits(designId) < units) return false;
        var stock = GetSupportStock(ResolveAirframePartsGroup(designId));
        if (!stock.TotalCostBasis.HasValue || stock.UnvaluedUnits > 0) throw new InvalidOperationException("Initialize airframe inventory cost basis before consumption.");
        consumedCost = stock.TotalCostBasis.Value * units / stock.AirframeSpareParts;
        stock.AirframeSpareParts -= units;
        stock.TotalCostBasis = stock.AirframeSpareParts == 0 ? 0d : System.Math.Max(0d, stock.TotalCostBasis.Value - consumedCost);
        return true;
    }

    // Requests are desired outstanding demand, not additive orders. Replanning cannot duplicate stock or WIP.
    public FactorySupportProductionReport ProduceSupport(int monthIndex,
        IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog, IEnumerable<FactorySupportRequest> requests)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        if (requests is null) throw new ArgumentNullException(nameof(requests));
        InitializeInventoryCostBasis(catalog);
        PrepareProductionMonth(monthIndex, catalog);
        // Aircraft production may receive only currently manufactured designs later in this same month.
        // Keep service recipes for supported designs even after their new-aircraft production was stopped.
        _supportCatalogForMonth = catalog.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var requested = requests.Select(request => request with { DesignId = ResolveAirframePartsGroup(request.DesignId) }).ToArray();
        var requestedDesigns = new HashSet<string>(requested.Select(item => item.DesignId), StringComparer.Ordinal);
        foreach (var designId in _supportOrders.Select(order => ResolveAirframePartsGroup(order.DesignId))
            .Distinct(StringComparer.Ordinal).Where(id => !requestedDesigns.Contains(id)).ToArray())
            PlanAirframeSpareParts(designId, 0);
        foreach (var group in requested.GroupBy(item => item.DesignId, StringComparer.Ordinal))
            PlanAirframeSpareParts(group.Key, SaturatingSupportUnits(group.Select(item => item.AirframeSpareParts)));
        if (ProductionPriority != FactoryProductionPriority.AircraftFirst) RunSupportQueue(catalog, true);
        if (ProductionPriority == FactoryProductionPriority.AllSupportFirst) RunSupportQueue(catalog, false);
        return LastSupportReport;
    }

    private static int SaturatingSupportUnits(IEnumerable<int> units)
    {
        long total = 0;
        foreach (var value in units)
            total = System.Math.Min(int.MaxValue, total + System.Math.Max(0, value));
        return (int)total;
    }

    private void PlanAirframeSpareParts(string designId, int desiredUnits)
    {
        var stock = GetAirframeSparePartUnits(designId);
        // Reconcile target reductions without throwing away the paid fraction of a concrete unit.
        var wanted = System.Math.Max(0L, (long)desiredUnits - stock);
        foreach (var order in _supportOrders.Where(order => MatchesSupportOrder(order, designId))
            .OrderByDescending(order => order.CompletedManDays > Numeric.Epsilon).ToArray())
        {
            var retain = (int)System.Math.Min(order.RemainingUnits, System.Math.Max(wanted, order.CompletedManDays > Numeric.Epsilon ? 1L : 0L));
            order.RemainingUnits = retain;
            wanted = System.Math.Max(0L, wanted - retain);
            if (retain == 0) _supportOrders.Remove(order);
        }
        var pending = _supportOrders.Where(order => MatchesSupportOrder(order, designId)).ToArray();
        var deficit = (int)System.Math.Max(0L, (long)desiredUnits - stock
            - SaturatingSupportUnits(pending.Select(order => order.RemainingUnits)));
        if (deficit > 0) _supportOrders.Add(new FactorySupportWorkOrderSnapshot
        { DesignId = designId, Guaranteed = false, RemainingUnits = deficit });


    }

    private void RunSupportQueue(IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog, bool guaranteed, bool borrowUnusedProduction = false)
    {
        // Warranty changes reputation obligations, never manufacturing access or queue order.
        foreach (var order in _supportOrders.ToArray())
        {
            if (!catalog.TryGetValue(order.DesignId, out var item)) continue;
            var site = !string.IsNullOrWhiteSpace(order.FactoryId) ? GetFactory(order.FactoryId) : null;
            site ??= _factories.Where(factory => factory.IsOperational && RemainingManDays(factory, true, borrowUnusedProduction) > Numeric.Epsilon
                && GetEquipmentForFactory(factory.FactoryId) is { IsReady: true } equipment && MatchesSupportDesign(equipment.DesignId, order))
                .OrderBy(factory => factory.FactoryId, StringComparer.Ordinal).FirstOrDefault();
            if (site is null || GetEquipmentForFactory(site.FactoryId) is not { IsReady: true } ready || !MatchesSupportDesign(ready.DesignId, order)) continue;
            if (order.UnitManDays <= 0d)
            {
                order.FactoryId = site.FactoryId;
                var multiplier = FactoryBalance.GetProductionManDayMultiplier(ready.AutomationTier);
                order.UnitManDays = System.Math.Max(Numeric.Epsilon,
                    item.Aircraft.Performance.AirframeProductionManDays * FleetServiceBalance.AirframeSparePartFraction * multiplier);
                order.UnitMaterialCost = System.Math.Max(0d,
                    item.Aircraft.Performance.AirframeProductionCost * FleetServiceBalance.AirframeSparePartFraction);
                order.UnitOverheadCost = order.UnitManDays * (FactoryBalance.GetBuildingMonthlyMaintenance(site.BuildingSize)
                    + GetEquipmentMonthlyActiveCost(ready)) / site.BuildingCapacityManDays;
            }
            var work = System.Math.Min(RemainingManDays(site, true, borrowUnusedProduction), order.RemainingUnits * order.UnitManDays - order.CompletedManDays);
            if (work <= Numeric.Epsilon) continue;
            if (GetMonthlyAllocatedManDays(site.FactoryId) <= Numeric.Epsilon && (AutomaticStockReplenishment || site.Line!.MonthlyProductionTargetUnits == 0))
            {
                var expense = ChargeMonthlyEquipment(ready);
                LastSupportReport.EquipmentExpenses += expense;
            }
            ReserveManDays(site, work);
            _usedMonthlySupportManDays[site.FactoryId] = GetMonthlyAllocatedSupportManDays(site.FactoryId) + work;
            LastSupportReport.UsedManDays += work;
            LastSupportReport.LaborExpenses += work * ProductionEconomyBalance.LaborCostPerManDay;
            LastSupportReport.MaterialExpenses += work / order.UnitManDays * order.UnitMaterialCost;
            order.CompletedManDays += work;
            var completed = System.Math.Min(order.RemainingUnits, (int)System.Math.Floor((order.CompletedManDays + Numeric.Epsilon) / order.UnitManDays));
            order.CompletedManDays = System.Math.Max(0d, order.CompletedManDays - completed * order.UnitManDays);
            order.RemainingUnits -= completed;
            var stock = GetSupportStock(ResolveAirframePartsGroup(order.DesignId));
            if ((!stock.TotalCostBasis.HasValue || stock.UnvaluedUnits > 0) && stock.AirframeSpareParts > 0)
                throw new InvalidOperationException("Initialize existing airframe stock cost basis before production.");
            var completedCost = completed * (order.UnitMaterialCost + order.UnitManDays * ProductionEconomyBalance.LaborCostPerManDay + order.UnitOverheadCost);
            stock.TotalCostBasis = (stock.TotalCostBasis ?? 0d) + completedCost;
            stock.AirframeSpareParts += completed;
            LastSupportReport.CompletedInventoryCostBasis += completedCost;
            LastSupportReport.CompletedAirframeSpareParts += completed;
            if (order.RemainingUnits == 0) _supportOrders.Remove(order);
        }
    }

    private FactorySupportInventorySnapshot GetSupportStock(string designId)
    {
        ValidateDesignId(designId);
        if (!_supportInventory.TryGetValue(designId, out var stock))
            _supportInventory.Add(designId, stock = new FactorySupportInventorySnapshot { DesignId = designId });
        return stock;
    }

    private void PrepareProductionMonth(int monthIndex, IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog)
    {
        if (_externalEngineWorkMode && ExternalEngineSupply is null)
            throw new InvalidOperationException("Restore the external motor supply callback before processing aircraft production.");
        if (monthIndex < 0) throw new ArgumentOutOfRangeException(nameof(monthIndex));
        if (_productionBudgetMonth > monthIndex) throw new InvalidOperationException("Production month cannot move backwards.");
        if (_productionBudgetMonth != monthIndex)
        {
            _productionBudgetMonth = monthIndex;
            _usedMonthlyManDays.Clear();
            _usedMonthlySupportManDays.Clear();
            _monthlyAvailableManDays.Clear();
            _monthlyPartsAllocationPercent.Clear();
            _chargedEquipmentThisMonth.Clear();
            _supportCatalogForMonth = null;
            LastSupportReport = new FactorySupportProductionReport { MonthIndex = monthIndex };
        }
        foreach (var site in _factories)
        {
            if (!_monthlyAvailableManDays.ContainsKey(site.FactoryId))
            {
                _monthlyAvailableManDays[site.FactoryId] = site.AvailableManDays;
                _monthlyPartsAllocationPercent[site.FactoryId] = site.PartsAllocationPercent;
            }
            var equipment = GetEquipmentForFactory(site.FactoryId);
            if (equipment is null || !catalog.TryGetValue(equipment.DesignId, out var item)) continue;
            site.BaselineAircraftManDays = System.Math.Max(Numeric.Epsilon,
                item.Aircraft.Performance.AirframeProductionManDays + (ExternalEngineSupply is null ? item.Aircraft.Performance.EnginesProductionManDays : 0d));
            if (!site.NeedsLegacyCapacityMigration) continue;
            site.LegacyCapacityMd = System.Math.Max(site.LegacyCapacityMd, site.Line!.MonthlyProductionTargetUnits
                * site.BaselineAircraftManDays * FactoryBalance.GetProductionManDayMultiplier(equipment.AutomationTier)
                / System.Math.Max(.01d, (100d - site.PartsAllocationPercent) / 100d) * FactoryBalance.CapacityReductionMultiplier);
            _monthlyAvailableManDays[site.FactoryId] = site.AvailableManDays;
            site.NeedsLegacyCapacityMigration = false;
        }
    }

    private double RemainingManDays(FactorySiteState site, bool support, bool borrowUnusedProduction = false)
    {
        var totalCapacity = _monthlyAvailableManDays.TryGetValue(site.FactoryId, out var frozen) ? frozen : site.AvailableManDays;
        var totalLeft = System.Math.Max(0d, System.Math.Min(totalCapacity, site.AvailableManDays) - GetMonthlyAllocatedManDays(site.FactoryId));
        var supportUsed = GetMonthlyAllocatedSupportManDays(site.FactoryId);
        // Once aircraft have had their turn, support can use all remaining physical/staff capacity.
        if (support && borrowUnusedProduction) return totalLeft;
        // Aircraft may borrow only after compatible queued support is satisfied.
        if (!support && !_supportOrders.Any(order => order.RemainingUnits > 0
            && GetEquipmentForFactory(site.FactoryId) is { IsReady: true } equipment && MatchesSupportDesign(equipment.DesignId, order)
            && (_supportCatalogForMonth?.ContainsKey(order.DesignId) ?? false))) return totalLeft;
        var categoryLeft = support ? GetMonthlyPartsCapacityManDays(site.FactoryId) - supportUsed
            : GetMonthlyProductionCapacityManDays(site.FactoryId) - (GetMonthlyAllocatedManDays(site.FactoryId) - supportUsed);
        return System.Math.Max(0d, System.Math.Min(totalLeft, categoryLeft));
    }
    private void ReserveManDays(FactorySiteState site, double work) => _usedMonthlyManDays[site.FactoryId] = GetMonthlyAllocatedManDays(site.FactoryId) + work;

    private double ChargeMonthlyEquipment(FactoryEquipmentState equipment)
    {
        if (!_chargedEquipmentThisMonth.Add(equipment.EquipmentId)) return 0d;
        var cost = GetEquipmentMonthlyActiveCost(equipment);
        equipment.CumulativeActiveEquipmentExpenses += cost;
        return cost;
    }

    private void WriteSupportSnapshot(FactoryStateSnapshot snapshot)
    {
        snapshot.ProductionBudgetMonth = _productionBudgetMonth;
        snapshot.LastAircraftProductionMonth = _lastAircraftProductionMonth;
        snapshot.ProductionPriority = (int)ProductionPriority;
        snapshot.AircraftStockTargets = new Dictionary<string, int>(_aircraftStockTargets, StringComparer.Ordinal);
        snapshot.ChargedEquipmentIds = _chargedEquipmentThisMonth.OrderBy(id => id, StringComparer.Ordinal).ToList();
        foreach (var stock in _supportInventory.Values) snapshot.SupportInventory.Add(new FactorySupportInventorySnapshot
        { DesignId = stock.DesignId, AirframeSpareParts = stock.AirframeSpareParts, TotalCostBasis = stock.TotalCostBasis, UnvaluedUnits = stock.UnvaluedUnits });
        foreach (var order in _supportOrders) snapshot.SupportWorkOrders.Add(new FactorySupportWorkOrderSnapshot
        { DesignId = order.DesignId, Guaranteed = order.Guaranteed, RemainingUnits = order.RemainingUnits,
            CompletedManDays = order.CompletedManDays, UnitManDays = order.UnitManDays, UnitMaterialCost = order.UnitMaterialCost, UnitOverheadCost = order.UnitOverheadCost, FactoryId = order.FactoryId });
        foreach (var budget in _monthlyAvailableManDays) snapshot.MonthlyBudgets.Add(new FactoryMonthlyBudgetSnapshot
        { FactoryId = budget.Key, UsedManDays = GetMonthlyAllocatedManDays(budget.Key),
            SupportUsedManDays = GetMonthlyAllocatedSupportManDays(budget.Key), CapacitySchemaVersion = 1,
            AvailableCapacityManDays = budget.Value, PartsAllocationPercent = _monthlyPartsAllocationPercent[budget.Key] });
    }

    private void RestoreSupportSnapshot(FactoryStateSnapshot snapshot)
    {
        _supportInventory.Clear(); _supportOrders.Clear(); _usedMonthlyManDays.Clear(); _airframePartsAliases.Clear();
        _aircraftStockTargets.Clear();
        foreach (var target in snapshot.AircraftStockTargets ?? new Dictionary<string, int>())
            if (!string.IsNullOrWhiteSpace(target.Key) && target.Value >= 0) _aircraftStockTargets[target.Key] = target.Value;
        _usedMonthlySupportManDays.Clear(); _monthlyAvailableManDays.Clear(); _monthlyPartsAllocationPercent.Clear();
        _externalEngineSupply = null;
        _externalEngineWorkMode = snapshot.UsesExternalEngineSupply;
        _supportCatalogForMonth = null;
        _chargedEquipmentThisMonth.Clear();
        foreach (var equipmentId in snapshot.ChargedEquipmentIds ?? new List<string>())
            if (!string.IsNullOrWhiteSpace(equipmentId)) _chargedEquipmentThisMonth.Add(equipmentId);
        _productionBudgetMonth = snapshot.ProductionBudgetMonth;
        _lastAircraftProductionMonth = snapshot.LastAircraftProductionMonth;
        ProductionPriority = (FactoryProductionPriority)System.Math.Clamp(snapshot.ProductionPriority, 0, 2);
        LastSupportReport = new FactorySupportProductionReport { MonthIndex = _productionBudgetMonth };
        foreach (var stock in snapshot.SupportInventory ?? new List<FactorySupportInventorySnapshot>())
            if (!string.IsNullOrWhiteSpace(stock.DesignId)) _supportInventory[stock.DesignId] = new FactorySupportInventorySnapshot
            { DesignId = stock.DesignId, AirframeSpareParts = System.Math.Max(0, stock.AirframeSpareParts), TotalCostBasis = stock.TotalCostBasis };
        foreach (var order in snapshot.SupportWorkOrders ?? new List<FactorySupportWorkOrderSnapshot>())
            if (!string.IsNullOrWhiteSpace(order.DesignId) && order.RemainingUnits > 0)
                _supportOrders.Add(new FactorySupportWorkOrderSnapshot
                { DesignId = order.DesignId, Guaranteed = order.Guaranteed, RemainingUnits = order.RemainingUnits,
                    CompletedManDays = System.Math.Max(0d, order.CompletedManDays), UnitManDays = System.Math.Max(0d, order.UnitManDays),
                    UnitMaterialCost = System.Math.Max(0d, order.UnitMaterialCost), UnitOverheadCost = System.Math.Max(0d, order.UnitOverheadCost), FactoryId = order.FactoryId });
        foreach (var budget in snapshot.MonthlyBudgets ?? new List<FactoryMonthlyBudgetSnapshot>())
            if (!string.IsNullOrWhiteSpace(budget.FactoryId))
            {
                _usedMonthlyManDays[budget.FactoryId] = System.Math.Max(0d, budget.UsedManDays);
                _usedMonthlySupportManDays[budget.FactoryId] = System.Math.Clamp(budget.SupportUsedManDays, 0d, _usedMonthlyManDays[budget.FactoryId]);
                if (budget.CapacitySchemaVersion > 0)
                {
                    _monthlyAvailableManDays[budget.FactoryId] = System.Math.Max(0d, budget.AvailableCapacityManDays);
                    _monthlyPartsAllocationPercent[budget.FactoryId] = System.Math.Clamp(budget.PartsAllocationPercent, 1, 100);
                }
            }
    }
}
