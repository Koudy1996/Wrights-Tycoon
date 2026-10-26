using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Factories;

[TestFixture]
public sealed class AircraftSparePartsProductionTests
{
    private static (AircraftProductionFactory factory, Dictionary<string, FactoryAircraftTypeCatalogItem> catalog)
        Setup(double airframeMd = 100d, double enginesMd = 20d, FactoryAutomationTier tier = FactoryAutomationTier.HandBuilt)
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("parts.transport", airframeMd, 2, enginesMd);
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork();
        // Recipe/WIP scenarios use a stable staffed budget even when building balance changes.
        factory.SetWorkforceEmployeeLimit(factory.Factories[0].FactoryId, 400);
        factory.TryPurchaseAndInstallEquipment(factory.Factories[0].FactoryId, item.DesignId, tier,
            item.Aircraft.Performance.TotalProductionManDays, double.MaxValue, out _, out _);
        for (var month = 0; month < FactoryBalance.GetInitialInstallationMonths(tier); month++) factory.AdvanceMonth();
        return (factory, new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item });
    }

    private static FactorySupportRequest Parts(string id, int count) => new(id, count);

    [Test]
    public void DepotDiscardsPartialPartsAlreadyChargedWithoutCompletingOrChargingThemAgain()
    {
        var state = Setup(300_000, 50_000);
        var id = state.catalog.Keys.Single();
        var site = state.factory.Factories[0].FactoryId;
        var first = state.factory.ProduceSupport(0, state.catalog, new[] { Parts(id, 100) });
        Assert.That(first.LaborExpenses, Is.EqualTo(first.UsedManDays * 80d).Within(1e-6));
        Assert.That(first.MaterialExpenses, Is.EqualTo(first.UsedManDays / 3000d * 1500d).Within(1e-6));
        Assert.That(state.factory.TryStoreInstalledEquipment(site, out _), Is.True);
        var equipmentId = state.factory.Equipment.Single().EquipmentId;
        state.factory.RestoreSnapshot(state.factory.CreateSnapshot());
        var paused = state.factory.ProduceSupport(1, state.catalog, Array.Empty<FactorySupportRequest>());
        Assert.That(paused.UsedManDays, Is.Zero);
        Assert.That(state.factory.GetAirframeSparePartUnits(id), Is.Zero);
        Assert.That(state.factory.TryReinstallEquipment(equipmentId, site, double.MaxValue, out _, out _), Is.True);
        for (var i = 0; i < FactoryBalance.GetReinstallationMonths(FactoryAutomationTier.HandBuilt); i++) state.factory.AdvanceMonth();
        var last = state.factory.ProduceSupport(2, state.catalog, Array.Empty<FactorySupportRequest>());
        Assert.That(state.factory.GetAirframeSparePartUnits(id), Is.Zero);
        Assert.That(last.UsedManDays + last.MaterialExpenses + last.LaborExpenses, Is.Zero);
        Assert.That(state.factory.CreateSnapshot().SupportWorkOrders, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LoweredPartsTarget_DropsUnstartedTailButFinishesOnePaidUnit(bool omitRequest)
    {
        var state = Setup(300_000, 50_000); var id = state.catalog.Keys.Single();
        var first = state.factory.ProduceSupport(0, state.catalog, new[] { Parts(id, 100) });
        Assert.That(first.UsedManDays, Is.EqualTo(1680));
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(state.factory.CreateSnapshot());
        var next = restored.ProduceSupport(1, state.catalog, omitRequest ? Array.Empty<FactorySupportRequest>() : new[] { Parts(id, 0) });
        Assert.That(next.UsedManDays, Is.EqualTo(1320));
        Assert.That(restored.GetAirframeSparePartUnits(id), Is.EqualTo(1));
        Assert.That(restored.CreateSnapshot().SupportWorkOrders, Is.Empty);
        Assert.That(first.MaterialExpenses + next.MaterialExpenses, Is.EqualTo(1500).Within(.000001));
        Assert.That(restored.ProduceSupport(2, state.catalog, omitRequest ? Array.Empty<FactorySupportRequest>() : new[] { Parts(id, 0) }).UsedManDays, Is.Zero);

        var unstarted = Setup(); id = unstarted.catalog.Keys.Single();
        unstarted.factory.SetWorkforceEmployeeLimit(unstarted.factory.Factories[0].FactoryId, 0);
        unstarted.factory.ProduceSupport(0, unstarted.catalog, new[] { Parts(id, 100) });
        unstarted.factory.ProduceSupport(0, unstarted.catalog, omitRequest ? Array.Empty<FactorySupportRequest>() : new[] { Parts(id, 0) });
        Assert.That(unstarted.factory.CreateSnapshot().SupportWorkOrders, Is.Empty);
    }

    [Test]
    public void MaximumStockTargetsPlusMaintenance_SaturateWithoutOverflowOrDuplicateOrders()
    {
        var state = Setup(); var id = state.catalog.Keys.Single();
        var requests = new[] { new FactorySupportRequest(id, int.MaxValue, false), new FactorySupportRequest(id, 1) };
        var report = state.factory.ProduceSupport(0, state.catalog, requests);
        var originalWork = report.UsedManDays;
        state.factory.ProduceSupport(0, state.catalog, requests);
        var pending = state.factory.CreateSnapshot().SupportWorkOrders;
        Assert.That(pending.Sum(order => (long)order.RemainingUnits)
            + state.factory.GetAirframeSparePartUnits(id), Is.EqualTo((long)int.MaxValue));
        Assert.That(report.UsedManDays, Is.EqualTo(originalWork));
        Assert.That(report.UsedManDays, Is.LessThanOrEqualTo(state.factory.GetMonthlyPartsCapacityManDays(state.factory.Factories[0].FactoryId)));
    }

    [TestCase(FactoryAutomationTier.HandBuilt, 1d)]
    [TestCase(FactoryAutomationTier.SophisticatedLine, .5d)]
    public void AirframeRecipe_ChargesOnePercentMaterialsAndWorkWithSingleAutomation(FactoryAutomationTier tier, double multiplier)
    {
        var state = Setup(tier: tier); var id = state.catalog.Keys.Single();
        var report = state.factory.ProduceSupport(0, state.catalog, new[] { Parts(id, 6) });
        Assert.That(report.CompletedAirframeSpareParts, Is.EqualTo(6));
        Assert.That(report.MaterialExpenses, Is.EqualTo(6 * 150_000d * .01d).Within(.000001));
        Assert.That(report.UsedManDays, Is.EqualTo(6 * 100d * .01d * multiplier).Within(.000001));
        Assert.That(report.LaborExpenses, Is.EqualTo(report.UsedManDays * 80d).Within(.000001));
    }

    [Test]
    public void RepeatedPlanning_DoesNotDuplicatePartsOrWork()
    {
        var state = Setup(); var id = state.catalog.Keys.Single(); var site = state.factory.Factories[0];
        state.factory.SetWorkforceEmployeeLimit(site.FactoryId, 0);
        var request = new FactorySupportRequest(id, 3);
        state.factory.ProduceSupport(0, state.catalog, new[] { request });
        Assert.That(state.factory.CreateSnapshot().SupportWorkOrders.Count, Is.EqualTo(1));
        state.factory.SetWorkforceEmployeeLimit(site.FactoryId, 100);
        var report = state.factory.ProduceSupport(1, state.catalog, new[] { request });
        var originalWork = report.UsedManDays;
        state.factory.ProduceSupport(1, state.catalog, new[] { request });
        Assert.That(state.factory.GetAirframeSparePartUnits(id), Is.EqualTo(3));
        Assert.That(report.UsedManDays, Is.EqualTo(originalWork));
        Assert.That(state.factory.CreateSnapshot().SupportWorkOrders, Is.Empty);
    }

    [Test]
    public void Consumption_IsPositiveAndAtomic()
    {
        var state = Setup(); var id = state.catalog.Keys.Single();
        state.factory.ProduceSupport(0, state.catalog, new[] { new FactorySupportRequest(id, 3) });
        Assert.That(state.factory.TryConsumeAirframeSpareParts(id, 4), Is.False);
        Assert.That(state.factory.GetAirframeSparePartUnits(id), Is.EqualTo(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.factory.TryConsumeAirframeSpareParts(id, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.factory.TryConsumeAirframeSpareParts(id, -1));
        Assert.That(state.factory.TryConsumeAirframeSpareParts(id, 2), Is.True);
        Assert.That(state.factory.GetAirframeSparePartUnits(id), Is.EqualTo(1));
        Assert.That(state.factory.TryConsumeAirframeSpareParts("unknown", 1), Is.False);
    }

    [Test]
    public void OversizedPart_WipAndMonthlyBudgetSurviveRoundTripWithoutDuplicateProduction()
    {
        var state = Setup(500_000, 100_000); var id = state.catalog.Keys.Single();
        var first = state.factory.ProduceSupport(0, state.catalog, new[] { Parts(id, 1) });
        Assert.That(first.UsedManDays, Is.EqualTo(1_680));
        Assert.That(state.factory.GetAirframeSparePartUnits(id), Is.Zero);
        var saved = state.factory.CreateSnapshot();
        Assert.That(saved.SupportWorkOrders.Single().CompletedManDays, Is.EqualTo(1_680));
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(saved);
        var sameMonth = restored.ProduceSupport(0, state.catalog, new[] { Parts(id, 1) });
        Assert.That(sameMonth.UsedManDays, Is.Zero);
        var second = restored.ProduceSupport(1, state.catalog, new[] { Parts(id, 1) });
        var third = restored.ProduceSupport(2, state.catalog, new[] { Parts(id, 1) });
        Assert.That(first.UsedManDays + second.UsedManDays + third.UsedManDays, Is.EqualTo(5_000));
        Assert.That(first.MaterialExpenses + second.MaterialExpenses + third.MaterialExpenses, Is.EqualTo(1_500).Within(.000001));
        Assert.That(restored.GetAirframeSparePartUnits(id), Is.EqualTo(1));
        Assert.That(restored.CreateSnapshot().SupportWorkOrders, Is.Empty);
    }

    [Test]
    public void PartsAndNewAircraft_ShareFiniteBudgetIncludingEngineWork()
    {
        var state = Setup(); var id = state.catalog.Keys.Single(); var site = state.factory.Factories[0];
        state.factory.TrySetMonthlyProductionTargetUnits(site.FactoryId, 10, out _, out _);
        var parts = state.factory.ProduceSupport(0, state.catalog, new[] { Parts(id, 1_000) });
        var aircraft = state.factory.SimulateMonth(state.catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(parts.UsedManDays, Is.EqualTo(1_000).Within(.000001));
        Assert.That(aircraft.TotalAircraftProduced, Is.EqualTo(10));
        Assert.That(parts.UsedManDays + aircraft.TotalUsedManDays, Is.EqualTo(2_200));
        Assert.That(site.Line!.CarryOverManDays, Is.Zero);
    }

    [Test]
    public void AirframePartsStock_RoundTripKeepsValuationSeparateFromAircraft()
    {
        var state = Setup(); var id = state.catalog.Keys.Single();
        state.factory.ProduceSupport(0, state.catalog, new[] { Parts(id, 4) });
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(state.factory.CreateSnapshot());
        Assert.That(restored.GetAirframeSparePartUnits(id), Is.EqualTo(4));
        var values = FleetInventoryValuation.AddSupport(new Dictionary<string, InventoryValueEntry>(), restored.CreateSnapshot(), state.catalog);
        Assert.That(values.Count, Is.EqualTo(1));
        Assert.That(values["airframe-spare-part:" + id].TotalValue, Is.GreaterThan(4 * (1500 + 80)));
        Assert.That(values["airframe-spare-part:" + id].TotalValue, Is.EqualTo(restored.CreateSnapshot().SupportInventory.Single().TotalCostBasis));
    }

    [Test]
    public void WarrantyFlag_DoesNotGiveManufacturingPriorityOrStarveAircraft()
    {
        var state = Setup(); var id = state.catalog.Keys.Single(); var site = state.factory.Factories[0];
        state.factory.SetWorkforceEmployeeLimit(site.FactoryId, 100);
        state.factory.TrySetMonthlyProductionTargetUnits(site.FactoryId, 17, out _, out _);
        var requests = new[] { Parts(id, 2), Parts(id, 100) with { Guaranteed = false } };
        var support = state.factory.ProduceSupport(0, state.catalog, requests);
        Assert.That(support.CompletedAirframeSpareParts, Is.EqualTo(102));
        Assert.That(support.UsedManDays, Is.EqualTo(102).Within(.000001));
        var aircraft = state.factory.SimulateMonth(state.catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(aircraft.TotalAircraftProduced, Is.EqualTo(16));
        Assert.That(support.CompletedAirframeSpareParts, Is.EqualTo(102));
        Assert.That(support.UsedManDays + aircraft.TotalUsedManDays, Is.EqualTo(2_100).Within(.000001));
        var used = support.UsedManDays;
        state.factory.ProduceSupport(0, state.catalog, requests);
        var queued = state.factory.CreateSnapshot().SupportWorkOrders;
        Assert.That(queued.Sum(order => order.RemainingUnits), Is.Zero);
        Assert.That(queued.All(order => !order.Guaranteed), Is.True);
        Assert.That(support.UsedManDays, Is.EqualTo(used));
    }

    [Test]
    public void ChangingWarrantyFlag_PreservesExistingWipExactlyOnce()
    {
        var state = Setup(300_000, 50_000); var id = state.catalog.Keys.Single();
        // Fix the monthly budget so changing warranty metadata cannot duplicate or discard paid work.
        var site = state.factory.Factories[0];
        state.factory.SetWorkforceEmployeeLimit(site.FactoryId, 80);
        state.factory.SetPartsAllocationPercent(site.FactoryId, 100);
        var first = state.factory.ProduceSupport(0, state.catalog, new[] { Parts(id, 3) with { Guaranteed = false } });
        state.factory.SimulateMonth(new Dictionary<string, FactoryAircraftTypeCatalogItem>(), 0, SimulationDate.WrightTycoonStart);
        var previousOrder = state.factory.CreateSnapshot().SupportWorkOrders.Single();
        Assert.That(previousOrder.RemainingUnits, Is.EqualTo(3));
        Assert.That(previousOrder.CompletedManDays, Is.EqualTo(1_680));
        var saved = state.factory.CreateSnapshot(); var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(saved);
        var requests = new[] { Parts(id, 1), Parts(id, 2) with { Guaranteed = false } };
        var next = restored.ProduceSupport(1, state.catalog, requests);
        Assert.That(next.CompletedAirframeSpareParts, Is.EqualTo(1));
        Assert.That(next.UsedManDays, Is.EqualTo(1_680));
        Assert.That(first.MaterialExpenses + next.MaterialExpenses, Is.EqualTo(1_680).Within(.000001));
        var remaining = restored.CreateSnapshot().SupportWorkOrders.Single();
        Assert.That(remaining.RemainingUnits, Is.EqualTo(2));
        Assert.That(remaining.Guaranteed, Is.False);
        Assert.That(remaining.CompletedManDays, Is.EqualTo(360));
        restored.ProduceSupport(1, state.catalog, requests);
        Assert.That(next.UsedManDays, Is.EqualTo(1_680));
        Assert.That(restored.GetAirframeSparePartUnits(id), Is.EqualTo(1));
        var afterReplan = restored.CreateSnapshot().SupportWorkOrders.Single();
        Assert.That(afterReplan.RemainingUnits, Is.EqualTo(2));
        Assert.That(afterReplan.CompletedManDays, Is.EqualTo(360));
        Assert.That(first.UsedManDays + next.UsedManDays,
            Is.EqualTo(restored.GetAirframeSparePartUnits(id) * afterReplan.UnitManDays + afterReplan.CompletedManDays));
    }

    [Test]
    public void ActualAirframeStockBasis_AveragesBatchesAndDoesNotRepriceAfterSaveOrCatalogChanges()
    {
        var state = Setup(); var id = state.catalog.Keys.Single();
        var first = state.factory.ProduceSupport(0, state.catalog, new[] { Parts(id, 2) });
        var initialBasis = state.factory.CreateSnapshot().SupportInventory.Single().TotalCostBasis!.Value;
        Assert.That(initialBasis, Is.GreaterThan(first.MaterialExpenses + first.LaborExpenses));
        var changed = AircraftProductionFactoryTests.CreateCatalogItem(id, 200d, 2, 20d);
        var changedCatalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [id] = changed };
        var second = state.factory.ProduceSupport(1, changedCatalog, new[] { Parts(id, 4) });
        var total = initialBasis + second.CompletedInventoryCostBasis;
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(state.factory.CreateSnapshot());
        restored.InitializeInventoryCostBasis(state.catalog);
        Assert.That(restored.TryConsumeAirframeSpareParts(id, 5, out var shortageCost), Is.False);
        Assert.That(shortageCost, Is.Zero);
        Assert.That(restored.TryConsumeAirframeSpareParts(id, 2, out var consumed), Is.True);
        Assert.That(consumed, Is.EqualTo(total / 2d).Within(1e-8));
        Assert.That(restored.TryConsumeAirframeSpareParts(id, 1), Is.True);
        var values = FleetInventoryValuation.AddSupport(new Dictionary<string, InventoryValueEntry>(), restored.CreateSnapshot(), changedCatalog);
        Assert.That(values["airframe-spare-part:" + id].TotalValue, Is.EqualTo(total / 4d).Within(1e-8));
    }

    [Test]
    public void MissingAirframeBasis_InitializesOnceAndCannotSilentlyProduceZeroConsumptionCost()
    {
        var state = Setup(); var id = state.catalog.Keys.Single();
        var snapshot = state.factory.CreateSnapshot();
        snapshot.SupportInventory.Add(new FactorySupportInventorySnapshot { DesignId = id, AirframeSpareParts = 3 });
        state.factory.RestoreSnapshot(snapshot);
        Assert.Throws<InvalidOperationException>(() => state.factory.TryConsumeAirframeSpareParts(id, 1, out _));
        state.factory.InitializeInventoryCostBasis(state.catalog);
        var estimated = state.factory.CreateSnapshot().SupportInventory.Single().TotalCostBasis!.Value;
        Assert.That(estimated, Is.GreaterThan(3 * 1580d));
        state.factory.InitializeInventoryCostBasis(new Dictionary<string, FactoryAircraftTypeCatalogItem>
            { [id] = AircraftProductionFactoryTests.CreateCatalogItem(id, 1000, 2, 20) });
        Assert.That(state.factory.TryConsumeAirframeSpareParts(id, 3, out var cost), Is.True);
        Assert.That(cost, Is.EqualTo(estimated));
        Assert.That(state.factory.CreateSnapshot().SupportInventory.Single().TotalCostBasis, Is.Zero);
    }

    [Test]
    public void AirframeWip_FrozenCostSurvivesSaveAndLaterRecipeChanges()
    {
        var state = Setup(300000, 20); var id = state.catalog.Keys.Single();
        state.factory.ProduceSupport(0, state.catalog, new[] { Parts(id, 1) });
        var snapshot = state.factory.CreateSnapshot(); var paid = snapshot.SupportWorkOrders.Single();
        Assert.That(paid.UnitOverheadCost, Is.GreaterThan(0d));
        var frozenCost = paid.UnitMaterialCost + paid.UnitManDays * ProductionEconomyBalance.LaborCostPerManDay + paid.UnitOverheadCost;
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(snapshot);
        var changedCatalog = new Dictionary<string, FactoryAircraftTypeCatalogItem>
            { [id] = AircraftProductionFactoryTests.CreateCatalogItem(id, 1, 2, 20) };
        restored.ProduceSupport(1, changedCatalog, new[] { Parts(id, 1) });
        Assert.That(restored.TryConsumeAirframeSpareParts(id, 1, out var consumedCost), Is.True);
        Assert.That(consumedCost, Is.EqualTo(frozenCost).Within(1e-8));
    }
}
