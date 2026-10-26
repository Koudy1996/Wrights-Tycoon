using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Factories;

public sealed class AutomaticAircraftStockTests
{
    [TestCase(FactoryBuildingSize.Small)]
    [TestCase(FactoryBuildingSize.Medium, Category = "Extended")]
    [TestCase(FactoryBuildingSize.Large, Category = "Extended")]
    public void MissingEnginesCapsPaidWorkAtOnePhysicalMonthAndDoesNotChargeItTwice(FactoryBuildingSize size)
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("bounded", 100, 2, 20);
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork();
        var site = factory.Factories[0];
        factory.TryPurchaseAndInstallEquipment(site.FactoryId, item.DesignId, FactoryAutomationTier.HandBuilt,
            120, double.MaxValue, out _, out _);
        var snapshot = factory.CreateSnapshot();
        snapshot.Factories[0].BuildingSize = (int)size;
        factory.RestoreSnapshot(snapshot);
        site = factory.Factories[0];
        var supply = new TestEngineSupply();
        factory.ExternalEngineSupply = supply;
        factory.AutomaticStockReplenishment = true;
        factory.SetAircraftStockTarget(item.DesignId, 100000);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var accounting = new FactoryAccountingCalculator();
        var capacity = site.BuildingCapacityManDays;
        var first = factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(first.TotalAircraftProduced, Is.Zero);
        Assert.That(first.TotalUsedManDays, Is.EqualTo(capacity));
        Assert.That(site.Line!.CarryOverManDays, Is.EqualTo(capacity));
        var paid = accounting.CalculateMonthlyCosts(first, catalog).VariablePlaneStaffExpenses;
        Assert.That(paid, Is.EqualTo(System.Math.Ceiling(capacity / 21d) * FactoryBalance.PlaneStaffMonthlyCostPerEmployee));

        // Save/load must neither lose paid work nor allow another month to accumulate.
        var restored = new AircraftProductionFactory();
        restored.RestoreSnapshot(factory.CreateSnapshot());
        restored.ExternalEngineSupply = supply;
        var stopped = restored.SimulateMonth(catalog, 1, SimulationDate.WrightTycoonStart.NextMonth());
        Assert.That(stopped.TotalUsedManDays, Is.Zero);
        Assert.That(stopped.TotalAircraftProduced, Is.Zero);
        Assert.That(accounting.CalculateMonthlyCosts(stopped, catalog).VariableCosts, Is.Zero);
        Assert.That(restored.Lines.Single().CarryOverManDays, Is.EqualTo(capacity));

        // Two engines finish one aircraft; only the 100 newly performed MD are paid.
        supply.Units = 2;
        var resumed = restored.SimulateMonth(catalog, 2, SimulationDate.WrightTycoonStart.AddMonths(2));
        Assert.That(resumed.TotalAircraftProduced, Is.EqualTo(1));
        Assert.That(resumed.TotalUsedManDays, Is.EqualTo(100));
        Assert.That(restored.Lines.Single().CarryOverManDays, Is.EqualTo(capacity));
        Assert.That(supply.Units, Is.Zero);
        var costs = accounting.CalculateMonthlyCosts(resumed, catalog);
        Assert.That(costs.VariablePlaneStaffExpenses, Is.EqualTo(5 * FactoryBalance.PlaneStaffMonthlyCostPerEmployee));
        Assert.That(costs.VariablePlaneBuildExpenses, Is.EqualTo(item.Aircraft.Performance.AirframeProductionCost));
        Assert.That(costs.VariableEngineBuildExpenses, Is.Zero);
    }

    [Test]
    public void AlmostFullAndLegacyWorkChargeOnlyRemainingRoomAndPreserveAlreadyPaidExcess()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("partial.bound", 100, 1, 20);
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork();
        var site = factory.Factories[0];
        factory.TryPurchaseAndInstallEquipment(site.FactoryId, item.DesignId, FactoryAutomationTier.HandBuilt,
            120, double.MaxValue, out _, out _);
        factory.ExternalEngineSupply = new TestEngineSupply();
        factory.AutomaticStockReplenishment = true;
        factory.SetAircraftStockTarget(item.DesignId, 10000);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var snapshot = factory.CreateSnapshot();
        var capacity = site.BuildingCapacityManDays;
        snapshot.Factories[0].Line!.CarryOverManDays = capacity - 105;
        factory.RestoreSnapshot(snapshot);
        factory.ExternalEngineSupply = new TestEngineSupply();
        var partial = factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(partial.TotalUsedManDays, Is.EqualTo(105));
        Assert.That(new FactoryAccountingCalculator().CalculateMonthlyCosts(partial, catalog).VariablePlaneStaffExpenses,
            Is.EqualTo(5 * FactoryBalance.PlaneStaffMonthlyCostPerEmployee));
        Assert.That(factory.Lines.Single().CarryOverManDays, Is.EqualTo(capacity));

        snapshot = factory.CreateSnapshot();
        snapshot.Factories[0].Line!.CarryOverManDays = capacity * 3;
        factory.RestoreSnapshot(snapshot);
        var supply = new TestEngineSupply();
        factory.ExternalEngineSupply = supply;
        var stopped = factory.SimulateMonth(catalog, 1, SimulationDate.WrightTycoonStart.NextMonth());
        Assert.That(stopped.TotalUsedManDays, Is.Zero);
        Assert.That(factory.Lines.Single().CarryOverManDays, Is.EqualTo(capacity * 3));
        supply.Units = (int)(capacity * 2 / 100);
        var drained = factory.SimulateMonth(catalog, 2, SimulationDate.WrightTycoonStart.AddMonths(2));
        Assert.That(drained.TotalUsedManDays, Is.Zero);
        Assert.That(factory.Lines.Single().CarryOverManDays, Is.EqualTo(capacity));
        Assert.That(new FactoryAccountingCalculator().CalculateMonthlyCosts(drained, catalog).VariablePlaneStaffExpenses, Is.Zero);
    }

    private sealed class TestEngineSupply : IExternalAircraftEngineSupply
    {
        public int Units;
        public int AvailableEngines(string aircraftDesignId) => Units;
        public bool TryConsumeEngines(string aircraftDesignId, int units)
        {
            if (units > Units) return false;
            Units -= units;
            return true;
        }
    }

    [Test]
    public void StockOnlyProductionFillsBothGoalsStopsAndResumesAfterWithdrawalsAcrossPlants()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("automatic", 100, 1, 20);
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork(2);
        factory.AutomaticStockReplenishment = true;
        foreach (var site in factory.Factories)
        {
            factory.TryPurchaseAndInstallEquipment(site.FactoryId, item.DesignId, FactoryAutomationTier.HandBuilt,
                120, double.MaxValue, out _, out _);
            // Obsolete limits must not restrict replenishment, including previously stopped lines.
            factory.TrySetMonthlyProductionTargetUnits(site.FactoryId, site == factory.Factories[0] ? 0 : 1, out _, out _);
            factory.SetTotalProductionTargetUnits(site.Line!.LineId, 1);
            factory.SetPartsAllocationPercent(site.FactoryId, 100);
        }
        factory.SetAircraftStockTarget(item.DesignId, 5);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var requests = new[] { new FactorySupportRequest(item.DesignId, 20, false) };
        var parts = factory.ProduceSupport(0, catalog, requests);
        var planes = factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(parts.CompletedAirframeSpareParts, Is.EqualTo(20));
        Assert.That(planes.TotalAircraftProduced, Is.EqualTo(5));
        Assert.That(factory.GetWarehouseUnits(item.DesignId), Is.EqualTo(5));
        Assert.That(factory.ProduceSupport(1, catalog, requests).UsedManDays, Is.Zero);
        Assert.That(factory.SimulateMonth(catalog, 1, SimulationDate.WrightTycoonStart.NextMonth()).TotalUsedManDays, Is.Zero);
        factory.TryWithdrawFromWarehouse(item.DesignId, 2, out _);
        factory.TryConsumeAirframeSpareParts(item.DesignId, 3);
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(factory.CreateSnapshot());
        Assert.That(restored.AutomaticStockReplenishment, Is.True);
        var refilledParts = restored.ProduceSupport(2, catalog, requests);
        Assert.That(restored.SimulateMonth(catalog, 2, SimulationDate.WrightTycoonStart.AddMonths(2)).TotalAircraftProduced, Is.EqualTo(2));
        Assert.That(refilledParts.CompletedAirframeSpareParts, Is.EqualTo(3));
        Assert.That(restored.GetWarehouseUnits(item.DesignId), Is.EqualTo(5));
        restored.TryConsumeAirframeSpareParts(item.DesignId, 1);
        var partsOnly = restored.ProduceSupport(3, catalog, requests);
        Assert.That(restored.SimulateMonth(catalog, 3, SimulationDate.WrightTycoonStart.AddMonths(3)).TotalAircraftProduced, Is.Zero);
        Assert.That(partsOnly.CompletedAirframeSpareParts, Is.EqualTo(1));
    }

    [Test]
    public void ZeroStockGoalPausesPaidWorkAndRaisingItResumesWithoutMonthlyConfiguration()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("large.automatic", 20000, 1, 0);
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork();
        var site = factory.Factories[0];
        factory.TryPurchaseAndInstallEquipment(site.FactoryId, item.DesignId, FactoryAutomationTier.HandBuilt,
            20000, double.MaxValue, out _, out _);
        factory.AutomaticStockReplenishment = true;
        factory.SetAircraftStockTarget(item.DesignId, 1);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        var paid = site.Line!.CarryOverManDays;
        Assert.That(paid, Is.EqualTo(12600));
        factory.SetAircraftStockTarget(item.DesignId, 0);
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(factory.CreateSnapshot());
        Assert.That(restored.SimulateMonth(catalog, 1, SimulationDate.WrightTycoonStart.NextMonth()).TotalUsedManDays, Is.Zero);
        Assert.That(restored.Lines.Single().CarryOverManDays, Is.EqualTo(paid));
        restored.SetAircraftStockTarget(item.DesignId, 1);
        var completed = restored.SimulateMonth(catalog, 2, SimulationDate.WrightTycoonStart.AddMonths(2));
        Assert.That(completed.TotalAircraftProduced, Is.EqualTo(1));
        Assert.That(completed.TotalUsedManDays, Is.EqualTo(20000 - paid));
    }
}
