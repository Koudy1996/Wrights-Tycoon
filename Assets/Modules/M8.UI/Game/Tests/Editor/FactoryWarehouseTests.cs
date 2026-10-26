using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Balance;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

public sealed class FactoryWarehouseTests
{
    [TestCase(FactoryBuildingSize.Small, 12_600d, 80_000_000d, 600_000d)]
    [TestCase(FactoryBuildingSize.Medium, 50_400d, 240_000_000d, 1_800_000d)]
    [TestCase(FactoryBuildingSize.Large, 201_600d, 720_000_000d, 5_400_000d)]
    public void ReducedPhysicalCapacityPreservesConstructionPriceAndMaintenance(FactoryBuildingSize size, double capacity, double price, double maintenance)
    {
        Assert.That(FactoryBalance.GetBuildingCapacityManDays(size), Is.EqualTo(capacity));
        Assert.That(FactoryBalance.GetConstructionCost(size), Is.EqualTo(price));
        Assert.That(FactoryBalance.GetBuildingMonthlyMaintenance(size), Is.EqualTo(maintenance));
        Assert.That(EngineManufacturingNetwork.Maintenance(size), Is.EqualTo(maintenance / 2d));
        var engines = new EngineManufacturingNetwork();
        Assert.That(engines.TryConstruct(size, price / 2d, out var actualCost, out _), Is.True);
        Assert.That(actualCost, Is.EqualTo(price / 2d));
        Assert.That(engines.Sites[0].BuildingCapacityManDays, Is.EqualTo(capacity));
    }

    [TestCase(FactoryBuildingSize.Small, 80_000_000d, 18)]
    [TestCase(FactoryBuildingSize.Medium, 240_000_000d, 30)]
    [TestCase(FactoryBuildingSize.Large, 720_000_000d, 48)]
    public void DirectConstructionPreservesSizePriceTimeAndCapacityAcrossSaveRestore(FactoryBuildingSize size, double cost, int months)
    {
        var aircraft = new AircraftProductionFactory();
        var engines = new EngineManufacturingNetwork();
        Assert.That(aircraft.TryStartFactoryConstruction(size, cost - 1, out _, out _), Is.False);
        Assert.That(engines.TryConstruct(size, cost / 2 - 1, out _, out _), Is.False);
        Assert.That(aircraft.TryStartFactoryConstruction(size, cost, out var aircraftCost, out _), Is.True);
        Assert.That(engines.TryConstruct(size, cost / 2, out var engineCost, out _), Is.True);
        Assert.That(aircraftCost, Is.EqualTo(cost)); Assert.That(engineCost, Is.EqualTo(cost / 2));
        var restoredAircraft = new AircraftProductionFactory(); restoredAircraft.RestoreSnapshot(aircraft.CreateSnapshot());
        var restoredEngines = new EngineManufacturingNetwork(); restoredEngines.RestoreSnapshot(engines.CreateSnapshot());
        Assert.That(restoredAircraft.Factories[0].BuildingSize, Is.EqualTo(size));
        Assert.That(restoredEngines.Sites[0].BuildingSize, Is.EqualTo(size));
        Assert.That(restoredAircraft.Factories[0].ConstructionMonthsRemaining, Is.EqualTo(months));
        Assert.That(restoredEngines.Sites[0].ConstructionMonthsRemaining, Is.EqualTo(months));
        for (var month = 0; month < months - 1; month++) { restoredAircraft.AdvanceMonth(); restoredEngines.AdvanceMonth(); }
        Assert.That(restoredAircraft.Factories[0].IsOperational, Is.False);
        Assert.That(restoredEngines.Sites[0].IsOperational, Is.False);
        restoredAircraft.AdvanceMonth(); restoredEngines.AdvanceMonth();
        Assert.That(restoredAircraft.Factories[0].IsOperational, Is.True);
        Assert.That(restoredEngines.Sites[0].IsOperational, Is.True);
        Assert.That(restoredAircraft.Factories[0].BuildingCapacityManDays, Is.EqualTo(FactoryBalance.GetBuildingCapacityManDays(size)));
        Assert.That(restoredEngines.Sites[0].BuildingCapacityManDays, Is.EqualTo(FactoryBalance.GetBuildingCapacityManDays(size)));
    }

    [Test]
    public void OverviewKeepsExactEngineStockAndSharedPartsDistinctWithoutMutatingStock()
    {
        var aircraft = new AircraftProductionFactory();
        aircraft.RestoreSnapshot(new FactoryStateSnapshot
        {
            AircraftStockTargets = new Dictionary<string, int> { ["aircraft-v1"] = 12 },
            WarehouseEntries = new List<FactoryWarehouseEntrySnapshot> { new() { DesignId = "aircraft-v1", Units = 7 } },
            SupportInventory = new List<FactorySupportInventorySnapshot>
            {
                new() { DesignId = "airframe-shared", AirframeSpareParts = 25 },
            },
        });
        var engines = new EngineManufacturingNetwork();
        engines.RestoreSnapshot(new EngineManufacturingSnapshot
        {
            EngineStock = new Dictionary<string, int> { ["engine-v1"] = 5, ["engine-v2"] = 9 },
            PartStock = new Dictionary<string, int> { ["shared-engine-parts"] = 42 },
        });
        var go = new GameObject("WarehouseProjectionTest");
        go.SetActive(false);
        var runner = go.AddComponent<WrightTycoonLaunchRunner>();
        try
        {
            var type = typeof(WrightTycoonLaunchRunner);
            type.GetField("_productionFactory", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, aircraft);
            type.GetField("_engineManufacturing", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, engines);
            var rows = ((IEnumerable)type.GetMethod("BuildFactoryWarehouseRows", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(runner, null)!).Cast<object>().ToArray();
            string Kind(object row) => (string)row.GetType().GetProperty("Kind")!.GetValue(row)!;
            int Units(object row) => (int)row.GetType().GetProperty("Units")!.GetValue(row)!;
            Assert.That(rows.Where(r => Kind(r) == "Aircraft").Sum(Units), Is.EqualTo(7));
            var limited = rows.Single(r => Kind(r) == "Aircraft" && Units(r) == 7);
            Assert.That(limited.GetType().GetProperty("StockTarget")!.GetValue(limited), Is.EqualTo(12));
            int Parts(object row) => (int)row.GetType().GetProperty("Parts")!.GetValue(row)!;
            Assert.That(rows.Where(r => Kind(r) == "Engines").Select(Units), Is.EquivalentTo(new[] { 5, 9, 0 }));
            Assert.That(rows.All(r => Kind(r) == "Aircraft" || Kind(r) == "Engines"), Is.True);
            Assert.That(rows.Where(r => Kind(r) == "Engines").Sum(Parts), Is.EqualTo(42));
            Assert.That(rows.Where(r => Kind(r) == "Aircraft").Sum(Parts), Is.EqualTo(25));
            Assert.That(engines.GetEngineUnits("engine-v1"), Is.EqualTo(5));
            Assert.That(engines.GetPartUnits("shared-engine-parts"), Is.EqualTo(42));
            Assert.That(aircraft.GetAirframeSparePartUnits("airframe-shared"), Is.EqualTo(25));
        }
        finally { Object.DestroyImmediate(go); }
    }
}
