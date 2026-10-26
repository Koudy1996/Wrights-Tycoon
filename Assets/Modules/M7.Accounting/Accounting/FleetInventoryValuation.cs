using System;
using System.Collections.Generic;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Core.Accounting;

public static class FleetInventoryValuation
{
    public static IReadOnlyDictionary<string, InventoryValueEntry> AddSupport(
        IReadOnlyDictionary<string, InventoryValueEntry> aircraftInventory, FactoryStateSnapshot factory,
        IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog)
    {
        var result = new Dictionary<string, InventoryValueEntry>(StringComparer.Ordinal);
        foreach (var entry in aircraftInventory) result.Add(entry.Key, entry.Value);
        foreach (var stock in factory.SupportInventory)
        {
            if (stock.AirframeSpareParts <= 0) continue;
            if (stock.TotalCostBasis.HasValue && stock.UnvaluedUnits == 0)
            {
                Add(result, "airframe-spare-part:" + stock.DesignId, stock.AirframeSpareParts,
                    stock.TotalCostBasis.Value / stock.AirframeSpareParts);
                continue;
            }
            if (!catalog.TryGetValue(stock.DesignId, out var design)) continue;
            var p = design.Aircraft.Performance;
            var overheadRate = (FactoryBalance.GetBuildingMonthlyMaintenance(FactoryBuildingSize.Small)
                + FactoryBalance.GetEquipmentPurchaseCost(FactoryAutomationTier.HandBuilt, FactoryBuildingSize.Small)
                    * FactoryBalance.GetMonthlyActiveEquipmentCostShare(FactoryAutomationTier.HandBuilt))
                / FactoryBalance.GetBuildingCapacityManDays(FactoryBuildingSize.Small);
            Add(result, "airframe-spare-part:" + stock.DesignId, stock.AirframeSpareParts,
                ((stock.TotalCostBasis ?? 0d) + (stock.TotalCostBasis.HasValue ? stock.UnvaluedUnits : stock.AirframeSpareParts)
                    * FleetServiceBalance.AirframeSparePartFraction * (p.AirframeProductionCost
                    + p.AirframeProductionManDays * (ProductionEconomyBalance.LaborCostPerManDay + overheadRate))) / stock.AirframeSpareParts);
        }
        var sequence = 0;
        foreach (var work in factory.SupportWorkOrders)
        {
            if (work.CompletedManDays <= 0d || work.UnitManDays <= 0d) continue;
            var value = work.CompletedManDays * (ProductionEconomyBalance.LaborCostPerManDay + (work.UnitMaterialCost + work.UnitOverheadCost) / work.UnitManDays);
            Add(result, $"support-wip:{work.DesignId}:{sequence++}", 1, value);
        }
        return result;
    }

    private static void Add(Dictionary<string, InventoryValueEntry> inventory, string id, int units, double cost)
    {
        if (units <= 0) return;
        inventory[id] = new InventoryValueEntry(id, units, cost, units * cost);
    }
}
