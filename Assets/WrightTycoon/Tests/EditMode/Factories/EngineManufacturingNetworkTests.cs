using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Factories;

[TestFixture]
public sealed class EngineManufacturingNetworkTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void DepotDiscardsPaidPartialUnitWithoutRefundOrDuplicateCharges(bool isParts)
    {
        var state = Plant(isParts ? 1_000_000d : 10_000d);
        var network = state.network;
        var site = network.Sites[0];
        var demand = new[] { new EngineManufacturingDemand("engine.a", "group.a", isParts ? 0 : 100, isParts ? 100 : 0) };
        var first = network.ProcessMonth(0, state.catalog, demand);
        Assert.That(network.CreateSnapshot().WorkOrders.Single().CompletedManDays, Is.GreaterThan(0d));
        Assert.That(first.LaborExpenses, Is.EqualTo(first.UsedManDays * 80d).Within(1e-6));
        Assert.That(first.MaterialExpenses, Is.EqualTo(first.UsedManDays / 10_000d * (isParts ? 10d : 1000d)).Within(1e-6));
        Assert.That(network.TryStoreEquipment(site.SiteId, out _), Is.True);
        Assert.That(network.TryStoreEquipment(site.SiteId, out _), Is.False);
        var equipmentId = network.StoredEquipment.Single().EquipmentId;
        network.RestoreSnapshot(network.CreateSnapshot());
        Assert.That(network.GetInventoryValueEntries(state.catalog).Values.Sum(v => v.TotalValue), Is.Zero);
        Assert.That(network.CreateSnapshot().WorkOrders, Is.Empty);
        var paused = network.ProcessMonth(1, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(paused.UsedManDays, Is.Zero);
        Assert.That(paused.MaterialExpenses + paused.LaborExpenses, Is.Zero);
        Assert.That(network.TryReinstallEquipment(equipmentId, site.SiteId, double.MaxValue, out _, out _), Is.True);
        for (var i = 0; i < FactoryBalance.GetReinstallationMonths(FactoryAutomationTier.HandBuilt); i++) network.AdvanceMonth();
        var last = network.ProcessMonth(2, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(last.CompletedEngines + last.CompletedParts, Is.Zero);
        Assert.That(last.MaterialExpenses + last.LaborExpenses, Is.Zero);
        Assert.That(network.CreateSnapshot().WorkOrders, Is.Empty);
    }

    private static (EngineManufacturingNetwork network, Dictionary<string, EngineManufacturingCatalogItem> catalog)
        Plant(double manDays = 100d, FactoryAutomationTier tier = FactoryAutomationTier.HandBuilt)
    {
        var network = new EngineManufacturingNetwork();
        Assert.That(network.TryConstruct(double.MaxValue, out var capital, out _), Is.True);
        Assert.That(capital, Is.EqualTo(40_000_000));
        for (var month = 0; month < 18; month++) network.AdvanceMonth();
        var item = new EngineManufacturingCatalogItem("engine.a", "group.a", 1_000, manDays) { IsPartsGroupAnchor = true };
        Assert.That(network.Configure(network.Sites[0].SiteId, item, tier, 0, 0, double.MaxValue, out _, out _), Is.True);
        for (var month = 0; month < FactoryBalance.GetInitialInstallationMonths(tier); month++) network.AdvanceMonth();
        // Keep historical paid-WIP/queue fixtures at a fixed 8,400 staffed MD budget.
        network.SetWorkforceEmployeeLimit(network.Sites[0].SiteId, 400);
        return (network, new Dictionary<string, EngineManufacturingCatalogItem> { [item.EngineDesignId] = item });
    }

    [TestCase(FactoryAutomationTier.HandBuilt, 1d)]
    [TestCase(FactoryAutomationTier.JigsAndFixtures, .88d)]
    [TestCase(FactoryAutomationTier.MechanizedCells, .74d)]
    [TestCase(FactoryAutomationTier.AutomatedLine, .59d)]
    [TestCase(FactoryAutomationTier.SophisticatedLine, .45d)]
    [Category("Extended")]
    public void AllAutomationTiers_UseOneSharedBudgetAndChargeMaterialsAndLaborOnce(FactoryAutomationTier tier, double factor)
    {
        var state = Plant(tier: tier);
        var report = state.network.ProcessMonth(0, state.catalog, new[] { new EngineManufacturingDemand("engine.a", "group.a", 2, 3) });
        Assert.That(report.CompletedEngines, Is.EqualTo(2)); Assert.That(report.CompletedParts, Is.EqualTo(3));
        Assert.That(report.UsedManDays, Is.EqualTo(203 * factor).Within(.000001));
        Assert.That(report.MaterialExpenses, Is.EqualTo(2_030).Within(.000001));
        Assert.That(report.LaborExpenses, Is.EqualTo(203 * factor * 80).Within(.000001));
        Assert.That(report.MaintenanceExpenses, Is.EqualTo(300_000));
        var equipment = state.network.Sites[0].EquipmentCost * FactoryBalance.GetMonthlyActiveEquipmentCostShare(tier);
        Assert.That(report.EquipmentExpenses, Is.EqualTo(equipment));
        Assert.That(report.TotalExpenses, Is.EqualTo(report.MaterialExpenses + report.LaborExpenses + 300_000 + equipment));
    }

    [TestCase(FactoryBuildingSize.Small, 12_600d, 300_000d)]
    [TestCase(FactoryBuildingSize.Medium, 50_400d, 900_000d)]
    [TestCase(FactoryBuildingSize.Large, 201_600d, 2_700_000d)]
    public void BuildingSizes_HaveFiniteCapacityAndSizeSpecificMaintenance(FactoryBuildingSize size, double capacity, double upkeep)
    {
        var state = Plant(); var saved = state.network.CreateSnapshot(); saved.Sites[0].BuildingSize = size;
        saved.Sites[0].WorkforceEmployeeLimit = int.MaxValue;
        state.network.RestoreSnapshot(saved);
        var report = state.network.ProcessMonth(0, state.catalog, new[] { new EngineManufacturingDemand("engine.a", "group.a", 10000, 0) });
        Assert.That(report.UsedManDays, Is.EqualTo(capacity).Within(1e-8)); Assert.That(report.MaintenanceExpenses, Is.EqualTo(upkeep));
        Assert.That(report.CompletedEngines, Is.EqualTo((int)(capacity / 100)));
        Assert.That(state.network.Sites[0].BuildingCapacityManDays, Is.EqualTo(capacity));
    }

    [TestCase(FactoryAutomationTier.HandBuilt, 1d, 1d)]
    [TestCase(FactoryAutomationTier.JigsAndFixtures, .88d, .90d)]
    [TestCase(FactoryAutomationTier.MechanizedCells, .74d, .78d)]
    [TestCase(FactoryAutomationTier.AutomatedLine, .59d, .65d)]
    [TestCase(FactoryAutomationTier.SophisticatedLine, .45d, .50d)]
    [Category("Extended")]
    public void EngineAutomationHasItsOwnBalanceWithoutChangingAircraft(FactoryAutomationTier tier, double engine, double aircraft)
    {
        Assert.That(FactoryBalance.GetEngineProductionManDayMultiplier(tier), Is.EqualTo(engine));
        Assert.That(FactoryBalance.GetProductionManDayMultiplier(tier), Is.EqualTo(aircraft));
    }

    [Test]
    public void OldPaidEngineWorkKeepsItsFrozenRecipeWhileNewUnitsUseNewAutomation()
    {
        var state = Plant(20_000, FactoryAutomationTier.MechanizedCells);
        var snapshot = state.network.CreateSnapshot();
        snapshot.Sites[0].MonthlyEngineTarget = 2;
        snapshot.WorkOrders.Add(new EngineManufacturingWorkSnapshot
        {
            EngineDesignId = "engine.a", PartsGroupId = "group.a", SiteId = snapshot.Sites[0].SiteId,
            RemainingUnits = 1, CompletedManDays = 8400, UnitManDays = 15600, UnitMaterialCost = 1000,
        });
        state.network.RestoreSnapshot(snapshot);
        var report = state.network.ProcessMonth(0, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(report.CompletedEngines, Is.EqualTo(1));
        Assert.That(report.UsedManDays, Is.EqualTo(8400));
        var pending = state.network.CreateSnapshot().WorkOrders.Single();
        Assert.That(pending.UnitManDays, Is.EqualTo(14800));
        Assert.That(pending.CompletedManDays, Is.EqualTo(1200));
    }

    [Test]
    public void SharedGroupParts_UseCanonicalAnchorRecipeRegardlessOfConfiguredCheaperVariant()
    {
        var state = Plant();
        var anchor = new EngineManufacturingCatalogItem("anchor", "shared", 10_000, 100) { IsPartsGroupAnchor = true };
        var variant = new EngineManufacturingCatalogItem("variant", "shared", 1_000, 10);
        var catalog = new Dictionary<string, EngineManufacturingCatalogItem> { [variant.EngineDesignId] = variant, [anchor.EngineDesignId] = anchor };
        state.network.Configure(state.network.Sites[0].SiteId, variant, FactoryAutomationTier.HandBuilt, 0, 0, double.MaxValue, out _, out _);
        var report = state.network.ProcessMonth(0, catalog, new[] { new EngineManufacturingDemand("variant", "shared", 0, 10) });
        Assert.That(report.UsedManDays, Is.EqualTo(10)); Assert.That(report.MaterialExpenses, Is.EqualTo(1_000));
        Assert.That(state.network.GetPartUnits("shared"), Is.EqualTo(10));
        var inventory = state.network.GetInventoryValueEntries(catalog);
        Assert.That(inventory["motor-part:shared"].TotalValue, Is.EqualTo(report.CompletedPartCostBasis));
        Assert.That(state.network.TryConsumePartUnits("shared", 11), Is.False);
        Assert.That(state.network.GetPartUnits("shared"), Is.EqualTo(10));
        Assert.That(state.network.TryConsumePartUnits("shared", 4), Is.True);
        Assert.That(state.network.GetPartUnits("shared"), Is.EqualTo(6));
        Assert.That(state.network.TryConsumeEngineUnits("variant", 1), Is.False);
    }

    private static IEnumerable<TestCaseData> CapacityAndEquipmentCases()
    {
        foreach (FactoryBuildingSize size in Enum.GetValues(typeof(FactoryBuildingSize)))
        foreach (FactoryAutomationTier tier in Enum.GetValues(typeof(FactoryAutomationTier)))
            yield return new TestCaseData(size, tier);
    }

    [TestCaseSource(nameof(CapacityAndEquipmentCases))]
    [Category("Extended")]
    public void EverySizeAndEquipmentTierUsesFullSharedBuildingCapacity(FactoryBuildingSize size, FactoryAutomationTier tier)
    {
        var state = Plant(tier: tier);
        var saved = state.network.CreateSnapshot(); saved.Sites[0].BuildingSize = size;
        saved.Sites[0].WorkforceEmployeeLimit = int.MaxValue;
        state.network.RestoreSnapshot(saved);
        var report = state.network.ProcessMonth(0, state.catalog,
            new[] { new EngineManufacturingDemand("engine.a", "group.a", 10000, 0) });
        var capacity = FactoryBalance.GetBuildingCapacityManDays(size);
        var unitWork = 100d * FactoryBalance.GetEngineProductionManDayMultiplier(tier);
        Assert.That(state.network.Sites[0].BuildingCapacityManDays, Is.EqualTo(capacity));
        Assert.That(report.UsedManDays, Is.EqualTo(capacity).Within(.000001));
        Assert.That(report.CompletedEngines, Is.EqualTo((int)System.Math.Floor(capacity / unitWork)));
        Assert.That(report.MaintenanceExpenses, Is.EqualTo(FactoryBalance.GetBuildingMonthlyMaintenance(size) / 2d));
    }

    [Test]
    public void WipSnapshot_PreservesPaidWorkStockAndClosedMonth()
    {
        var unitMd = FactoryBalance.GetBuildingCapacityManDays(FactoryBuildingSize.Small) * .8d * 1.4d;
        var state = Plant(unitMd);
        state.network.SetWorkforceEmployeeLimit(state.network.Sites[0].SiteId, int.MaxValue);
        var demand = new[] { new EngineManufacturingDemand("engine.a", "group.a", 2, 0) };
        var first = state.network.ProcessMonth(0, state.catalog, demand);
        Assert.That(first.CompletedEngines, Is.Zero);
        Assert.That(state.network.GetInventoryValueEntries(state.catalog).Values.Sum(v => v.TotalValue),
            Is.GreaterThan(first.MaterialExpenses + first.LaborExpenses));
        var restored = new EngineManufacturingNetwork(); restored.RestoreSnapshot(state.network.CreateSnapshot());
        Assert.Throws<InvalidOperationException>(() => restored.ProcessMonth(0, state.catalog, demand));
        var second = restored.ProcessMonth(1, state.catalog, demand);
        var third = restored.ProcessMonth(2, state.catalog, demand);
        Assert.That(restored.GetEngineUnits("engine.a"), Is.EqualTo(2));
        Assert.That(first.UsedManDays + second.UsedManDays + third.UsedManDays, Is.EqualTo(unitMd * 2).Within(1e-8));
        Assert.That(first.MaterialExpenses + second.MaterialExpenses + third.MaterialExpenses, Is.EqualTo(2_000).Within(.000001));
        Assert.That(restored.CreateSnapshot().WorkOrders, Is.Empty);
        Assert.That(restored.TryConsumeEngineUnits("engine.a", 3), Is.False);
        Assert.That(restored.TryConsumeEngineUnits("engine.a", 2), Is.True);
    }

    [Test]
    public void AllMaintenance_PrecedesAircraftSupplyRegardlessOfWarranty()
    {
        var state = Plant();
        var report = state.network.ProcessMonth(0, state.catalog, new[] {
            new EngineManufacturingDemand("engine.a", "group.a", 2, 10),
            new EngineManufacturingDemand("engine.a", "group.a", 6, 0, false, true),
            new EngineManufacturingDemand("engine.a", "group.a", 100, 0, false),
        });
        Assert.That(report.CompletedParts, Is.EqualTo(10)); Assert.That(report.CompletedEngines, Is.EqualTo(83));
        Assert.That(report.UsedManDays, Is.EqualTo(8_400));
        Assert.That(state.network.CreateSnapshot().WorkOrders.All(w => w.Priority <= 1), Is.True);
    }

    [Test]
    public void Expansion_PausesWorkAndChangesCapacityAndUpkeepAfterRealDuration()
    {
        var state = Plant(); var id = state.network.Sites[0].SiteId;
        Assert.That(state.network.TryExpand(id, double.MaxValue, out _, out _), Is.False);
        Assert.That(state.network.TryStoreEquipment(id, out _), Is.True);
        Assert.That(state.network.TryExpand(id, 79_999_999, out _, out _), Is.False);
        Assert.That(state.network.TryExpand(id, 80_000_000, out var cost, out _), Is.True);
        Assert.That(cost, Is.EqualTo(80_000_000));
        var report = state.network.ProcessMonth(0, state.catalog, new[] { new EngineManufacturingDemand("engine.a", "group.a", 1, 0) });
        Assert.That(report.UsedManDays, Is.Zero);
        for (var month = 0; month < 12; month++) state.network.AdvanceMonth();
        Assert.That(state.network.Sites[0].BuildingCapacityManDays, Is.EqualTo(50_400));
        Assert.That(EngineManufacturingNetwork.Maintenance(state.network.Sites[0].BuildingSize), Is.EqualTo(900_000));
    }

    [Test]
    public void MotorEquipmentUsesFixedPricesAndDepotPreservesTypeSizeAndOriginalCost()
    {
        var state = Plant(); var network = state.network; var site = network.Sites[0];
        Assert.That(site.EquipmentCost, Is.EqualTo(1_000_000d));
        var variant = new EngineManufacturingCatalogItem("other", "other", 999d, 9000d);
        Assert.That(network.Configure(site.SiteId, variant, FactoryAutomationTier.HandBuilt, 0, 0, double.MaxValue, out var price, out _), Is.True);
        Assert.That(price, Is.EqualTo(1_000_000d));
        Assert.That(network.StoredEquipment.Count, Is.EqualTo(1));
        Assert.That(network.TryStoreEquipment(site.SiteId, out _), Is.True);
        Assert.That(network.StoredEquipment.Count, Is.EqualTo(2));
        var snapshot = network.CreateSnapshot();
        snapshot.Sites.Add(new EngineManufacturingSite { SiteId = "large", BuildingSize = FactoryBuildingSize.Large });
        network.RestoreSnapshot(snapshot);
        var equipment = network.StoredEquipment[0];
        Assert.That(equipment.EngineDesignId, Is.EqualTo("engine.a"));
        Assert.That(network.MonthlyEquipmentStorageCost, Is.EqualTo(200d));
        Assert.That(network.ProcessMonth(0, state.catalog, System.Array.Empty<EngineManufacturingDemand>()).EquipmentExpenses, Is.EqualTo(200d));
        Assert.That(network.TryReinstallEquipment(equipment.EquipmentId, "large", double.MaxValue, out _, out _), Is.False);
        Assert.That(network.TryReinstallEquipment(equipment.EquipmentId, site.SiteId, 29_999d, out _, out _), Is.False);
        Assert.That(network.TryReinstallEquipment(equipment.EquipmentId, site.SiteId, 30_000d, out var fee, out _), Is.True);
        Assert.That(fee, Is.EqualTo(30_000d));
        Assert.That(network.Sites[0].EngineDesignId, Is.EqualTo("engine.a"));
        Assert.That(network.Sites[0].EquipmentCost, Is.EqualTo(1_000_000d));
        Assert.That(network.MonthlyEquipmentStorageCost, Is.EqualTo(100d));
    }

    [TestCase(FactoryAutomationTier.HandBuilt, 1_000_000d)]
    [TestCase(FactoryAutomationTier.JigsAndFixtures, 8_000_000d)]
    [TestCase(FactoryAutomationTier.MechanizedCells, 25_000_000d)]
    [TestCase(FactoryAutomationTier.AutomatedLine, 55_000_000d)]
    [TestCase(FactoryAutomationTier.SophisticatedLine, 90_000_000d)]
    [Category("Extended")]
    public void MotorEquipmentPriceMatchesAircraftPriceForEverySize(FactoryAutomationTier tier, double basePrice)
    {
        foreach (var size in new[] { FactoryBuildingSize.Small, FactoryBuildingSize.Medium, FactoryBuildingSize.Large })
        foreach (var md in new[] { 10d, 9000d })
        {
            var network = new EngineManufacturingNetwork();
            var snapshot = new EngineManufacturingSnapshot();
            snapshot.Sites.Add(new EngineManufacturingSite { SiteId = "site", BuildingSize = size });
            network.RestoreSnapshot(snapshot);
            var item = new EngineManufacturingCatalogItem("engine", "group", 999d, md);
            var expected = basePrice * (size == FactoryBuildingSize.Small ? 1 : size == FactoryBuildingSize.Medium ? 3 : 9);
            Assert.That(network.Configure("site", item, tier, 0, 0, expected - 1d, out _, out _), Is.False);
            Assert.That(network.Configure("site", item, tier, 0, 0, expected, out var price, out _), Is.True);
            Assert.That(price, Is.EqualTo(expected));
        }
    }

    private sealed class ExternalSupply : IExternalAircraftEngineSupply
    {
        public int Units;
        public int AvailableEngines(string id) => Units;
        public bool TryConsumeEngines(string id, int units) { if (Units < units) return false; Units -= units; return true; }
    }

    [Test]
    public void AircraftExternalSupply_ConsumesRealEnginesAndNeverFabricatesOrBillsThemAgain()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("external", 100, 2, 20);
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork();
        var site = factory.Factories[0];
        factory.TryPurchaseAndInstallEquipment(site.FactoryId, item.DesignId, FactoryAutomationTier.HandBuilt, 100, double.MaxValue, out _, out _);
        var supply = new ExternalSupply { Units = 3 }; factory.ExternalEngineSupply = supply;
        factory.TrySetMonthlyProductionTargetUnits(site.FactoryId, 2, out _, out _);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var report = factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(report.TotalAircraftProduced, Is.EqualTo(1)); Assert.That(report.TotalUsedManDays, Is.EqualTo(200));
        Assert.That(supply.Units, Is.EqualTo(1)); Assert.That(report.EngineFactoryReport.TotalProducedEngines, Is.Zero);
        var costs = new FactoryAccountingCalculator().CalculateMonthlyCosts(report, catalog);
        Assert.That(costs.VariableEngineBuildExpenses, Is.Zero); Assert.That(costs.VariableEngineStaffExpenses, Is.Zero);
        Assert.That(costs.VariablePlaneBuildExpenses, Is.EqualTo(150_000));
        Assert.Throws<InvalidOperationException>(() => factory.ExternalEngineSupply = null);
        supply.Units = 2;
        factory.SetTotalProductionTargetUnits(site.Line!.LineId, 2);
        var next = factory.SimulateMonth(catalog, 1, SimulationDate.WrightTycoonStart.NextMonth());
        Assert.That(next.TotalAircraftProduced, Is.EqualTo(1)); Assert.That(next.TotalUsedManDays, Is.Zero);
        Assert.That(supply.Units, Is.Zero); Assert.That(site.Line.CarryOverManDays, Is.Zero);
    }

    [Test]
    public void FamilyAirframePartsMigrationPreservesStockAndPaidRecipes()
    {
        var factory = new AircraftProductionFactory();
        var snapshot = new FactoryStateSnapshot();
        snapshot.SupportInventory.Add(new FactorySupportInventorySnapshot { DesignId = "anchor", AirframeSpareParts = 7, TotalCostBasis = 70 });
        snapshot.SupportInventory.Add(new FactorySupportInventorySnapshot { DesignId = "old-split", AirframeSpareParts = 11, TotalCostBasis = 110 });
        snapshot.SupportWorkOrders.Add(new FactorySupportWorkOrderSnapshot
        { DesignId = "old-split", RemainingUnits = 2, CompletedManDays = .5, UnitManDays = 3, UnitMaterialCost = 19, FactoryId = "old-factory" });
        factory.RestoreSnapshot(snapshot);
        var groups = new SparePartsGroupRegistrySnapshot(new[]
        {
            new SparePartsGroupSnapshot("anchor", "family", new[] { "anchor" }),
            new SparePartsGroupSnapshot("old-split", "family", new[] { "old-split" }),
        });
        factory.SetAirframePartsGroups(groups);
        factory.SetAirframePartsGroups(groups);
        Assert.That(factory.GetAirframeSparePartUnits("anchor"), Is.EqualTo(18));
        Assert.That(factory.GetAirframeSparePartUnits("old-split"), Is.EqualTo(18));
        Assert.That(factory.CreateSnapshot().SupportInventory.Sum(stock => stock.AirframeSpareParts), Is.EqualTo(18));
        var paid = factory.CreateSnapshot().SupportWorkOrders.Single();
        Assert.That(paid.DesignId, Is.EqualTo("old-split"));
        Assert.That(paid.UnitManDays, Is.EqualTo(3)); Assert.That(paid.UnitMaterialCost, Is.EqualTo(19));
        Assert.That(paid.CompletedManDays, Is.EqualTo(.5)); Assert.That(paid.RemainingUnits, Is.EqualTo(2));
        Assert.That(factory.TryConsumeAirframeSpareParts("old-split", 8, out var consumedBasis), Is.True);
        Assert.That(consumedBasis, Is.EqualTo(80));
        Assert.That(factory.GetAirframeSparePartUnits("anchor"), Is.EqualTo(10));
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(factory.CreateSnapshot()); restored.SetAirframePartsGroups(groups);
        Assert.That(restored.GetAirframeSparePartUnits("old-split"), Is.EqualTo(10));
    }

    [Test]
    public void OldAirframeGroupWorkCompletesIntoFamilyPoolWithoutRepricingPaidRecipe()
    {
        var anchor = AircraftProductionFactoryTests.CreateCatalogItem("family.anchor", 100, 2, 20);
        var version = AircraftProductionFactoryTests.CreateCatalogItem("family.old-split", 200, 2, 20);
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork();
        factory.TryPurchaseAndInstallEquipment(factory.Factories[0].FactoryId, version.DesignId,
            FactoryAutomationTier.HandBuilt, 120, double.MaxValue, out _, out _);
        var snapshot = factory.CreateSnapshot();
        snapshot.SupportWorkOrders.Add(new FactorySupportWorkOrderSnapshot
        { DesignId = version.DesignId, Guaranteed = true, RemainingUnits = 1,
            CompletedManDays = .5, UnitManDays = 2, UnitMaterialCost = 20, FactoryId = factory.Factories[0].FactoryId });
        factory.RestoreSnapshot(snapshot);
        factory.SetAirframePartsGroups(new SparePartsGroupRegistrySnapshot(new[]
        { new SparePartsGroupSnapshot(anchor.DesignId, "family", new[] { anchor.DesignId, version.DesignId }) }));
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [anchor.DesignId] = anchor, [version.DesignId] = version };
        var report = factory.ProduceSupport(0, catalog, new[]
        { new FactorySupportRequest(anchor.DesignId, 1) });
        Assert.That(report.CompletedAirframeSpareParts, Is.EqualTo(1));
        Assert.That(report.UsedManDays, Is.EqualTo(1.5));
        Assert.That(report.MaterialExpenses, Is.EqualTo(15));
        Assert.That(factory.GetAirframeSparePartUnits(version.DesignId), Is.EqualTo(1));
        Assert.That(factory.CreateSnapshot().SupportWorkOrders, Is.Empty);
    }

    [Test]
    public void AnyFamilyEquipmentCanManufactureSharedAirframeParts()
    {
        var anchor = AircraftProductionFactoryTests.CreateCatalogItem("family.anchor", 100, 2, 20);
        var version = AircraftProductionFactoryTests.CreateCatalogItem("family.version", 200, 2, 20);
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork();
        factory.TryPurchaseAndInstallEquipment(factory.Factories[0].FactoryId, version.DesignId,
            FactoryAutomationTier.HandBuilt, 120, double.MaxValue, out _, out _);
        factory.SetAirframePartsGroups(new SparePartsGroupRegistrySnapshot(new[]
        { new SparePartsGroupSnapshot(anchor.DesignId, "family", new[] { anchor.DesignId, version.DesignId }) }));
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [anchor.DesignId] = anchor, [version.DesignId] = version };
        var report = factory.ProduceSupport(0, catalog, new[]
        { new FactorySupportRequest(anchor.DesignId, 2) });
        Assert.That(report.CompletedAirframeSpareParts, Is.EqualTo(2));
        Assert.That(report.UsedManDays, Is.EqualTo(2));
        Assert.That(factory.GetAirframeSparePartUnits(version.DesignId), Is.EqualTo(2));
        Assert.That(factory.TryConsumeAirframeSpareParts(version.DesignId, 2), Is.True);
        Assert.That(factory.GetAirframeSparePartUnits(anchor.DesignId), Is.Zero);
    }

    [Test]
    public void AirframeParts_RoundTripConsumesOnlyCompleteRequestedUnits()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("airframe.parts", 100, 2, 20);
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork(); var site = factory.Factories[0];
        factory.TryPurchaseAndInstallEquipment(site.FactoryId, item.DesignId, FactoryAutomationTier.HandBuilt, 120, double.MaxValue, out _, out _);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var report = factory.ProduceSupport(0, catalog, new[] { new FactorySupportRequest(item.DesignId, 2) });
        Assert.That(report.CompletedAirframeSpareParts, Is.EqualTo(2)); Assert.That(report.UsedManDays, Is.EqualTo(2));
        Assert.That(report.MaterialExpenses, Is.EqualTo(3_000));
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(factory.CreateSnapshot());
        Assert.That(restored.GetAirframeSparePartUnits(item.DesignId), Is.EqualTo(2));
        Assert.That(restored.TryConsumeAirframeSpareParts(item.DesignId, 3), Is.False);
        Assert.That(restored.TryConsumeAirframeSpareParts(item.DesignId, 2), Is.True);
    }

    [Test]
    public void RestoredExternalAircraftWip_KeepsItsWorkMeaningAndReattachesOnlyMatchingSupplyMode()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("external.saved", 100, 2, 20);
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork(); var site = factory.Factories[0];
        factory.TryPurchaseAndInstallEquipment(site.FactoryId, item.DesignId, FactoryAutomationTier.HandBuilt, 100, double.MaxValue, out _, out _);
        factory.ExternalEngineSupply = new ExternalSupply();
        factory.TrySetMonthlyProductionTargetUnits(site.FactoryId, 2, out _, out _);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        var saved = factory.CreateSnapshot();
        Assert.That(saved.UsesExternalEngineSupply, Is.True);
        Assert.That(saved.Factories[0].Line!.CarryOverManDays, Is.EqualTo(200));
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(saved);
        Assert.That(restored.UsesExternalEngineSupply, Is.True);
        Assert.Throws<InvalidOperationException>(() => restored.ProduceSupport(1, catalog, Array.Empty<FactorySupportRequest>()));
        restored.ExternalEngineSupply = new ExternalSupply { Units = 2 };
        var next = restored.SimulateMonth(catalog, 1, SimulationDate.WrightTycoonStart.NextMonth());
        Assert.That(next.TotalAircraftProduced, Is.EqualTo(1)); Assert.That(next.TotalUsedManDays, Is.Zero);
        Assert.That(restored.Lines[0].CarryOverManDays, Is.EqualTo(100));
        Assert.Throws<InvalidOperationException>(() => restored.ExternalEngineSupply = null);

        saved.UsesExternalEngineSupply = false;
        var legacy = new AircraftProductionFactory(); legacy.RestoreSnapshot(saved);
        Assert.Throws<InvalidOperationException>(() => legacy.ExternalEngineSupply = new ExternalSupply { Units = 2 });
        Assert.That(legacy.Lines[0].CarryOverManDays, Is.EqualTo(200));
    }

    [Test]
    public void MonthlyStockTargets_TopUpAfterConsumptionThenStopAndResumeWithoutDuplicateOrders()
    {
        var state = Plant(); var site = state.network.Sites[0]; var item = state.catalog["engine.a"];
        void Targets(int motors, int reserve) => Assert.That(state.network.Configure(site.SiteId, item,
            FactoryAutomationTier.HandBuilt, motors, reserve, double.MaxValue, out _, out _), Is.True);
        Targets(3, 20);
        var first = state.network.ProcessMonth(0, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(first.CompletedEngines, Is.EqualTo(3)); Assert.That(first.CompletedParts, Is.EqualTo(20));
        var idle = state.network.ProcessMonth(1, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(idle.UsedManDays, Is.Zero); Assert.That(idle.MaterialExpenses + idle.LaborExpenses + idle.EquipmentExpenses, Is.Zero);
        state.network.TryConsumeEngineUnits(item.EngineDesignId, 2); state.network.TryConsumePartUnits(item.PartsGroupId, 7);
        var refill = state.network.ProcessMonth(2, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(refill.CompletedEngines, Is.EqualTo(2)); Assert.That(refill.CompletedParts, Is.EqualTo(7));
        Assert.That(refill.UsedManDays, Is.EqualTo(207));
        Targets(0, 0);
        Assert.That(state.network.ProcessMonth(3, state.catalog, Array.Empty<EngineManufacturingDemand>()).UsedManDays, Is.Zero);
        Assert.That(state.network.GetEngineUnits(item.EngineDesignId), Is.EqualTo(3));
        Assert.That(state.network.GetPartUnits(item.PartsGroupId), Is.EqualTo(20));
        state.network.TryConsumeEngineUnits(item.EngineDesignId, 3); state.network.TryConsumePartUnits(item.PartsGroupId, 20);
        Targets(1, 2);
        var resumed = state.network.ProcessMonth(4, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(resumed.CompletedEngines, Is.EqualTo(1)); Assert.That(resumed.CompletedParts, Is.EqualTo(2));
        Assert.That(resumed.UsedManDays, Is.EqualTo(102)); Assert.That(state.network.CreateSnapshot().WorkOrders, Is.Empty);
    }

    [Test]
    public void LowerTargetToZero_FinishesOnlyOnePaidUnitAndCancelsItsUnstartedTailAfterSaveLoad()
    {
        var state = Plant(10080); var site = state.network.Sites[0]; var item = state.catalog["engine.a"];
        state.network.Configure(site.SiteId, item, FactoryAutomationTier.HandBuilt, 10, 0, double.MaxValue, out _, out _);
        var first = state.network.ProcessMonth(0, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(first.CompletedEngines, Is.Zero);
        Assert.That(state.network.CreateSnapshot().WorkOrders.Single().RemainingUnits, Is.EqualTo(10));
        state.network.Configure(site.SiteId, item, FactoryAutomationTier.HandBuilt, 0, 0, double.MaxValue, out _, out _);
        var restored = new EngineManufacturingNetwork(); restored.RestoreSnapshot(state.network.CreateSnapshot());
        var finish = restored.ProcessMonth(1, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(finish.CompletedEngines, Is.EqualTo(1)); Assert.That(finish.UsedManDays, Is.EqualTo(1680));
        Assert.That(first.MaterialExpenses + finish.MaterialExpenses, Is.EqualTo(1000).Within(.000001));
        Assert.That(first.LaborExpenses + finish.LaborExpenses, Is.EqualTo(10080 * 80));
        Assert.That(restored.GetEngineUnits(item.EngineDesignId), Is.EqualTo(1)); Assert.That(restored.CreateSnapshot().WorkOrders, Is.Empty);
        Assert.That(restored.ProcessMonth(2, state.catalog, Array.Empty<EngineManufacturingDemand>()).UsedManDays, Is.Zero);
    }

    [Test]
    public void LowerTargets_CancelsUntouchedMotorAndPartsOrdersRatherThanCompletingStaleDemand()
    {
        var state = Plant(); var site = state.network.Sites[0]; var item = state.catalog["engine.a"];
        state.network.SetWorkforceEmployeeLimit(site.SiteId, 0);
        state.network.Configure(site.SiteId, item, FactoryAutomationTier.HandBuilt, 10, 100, double.MaxValue, out _, out _);
        state.network.ProcessMonth(0, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(state.network.CreateSnapshot().WorkOrders.Sum(order => order.RemainingUnits), Is.EqualTo(110));
        state.network.Configure(site.SiteId, item, FactoryAutomationTier.HandBuilt, 2, 5, double.MaxValue, out _, out _);
        state.network.SetWorkforceEmployeeLimit(site.SiteId, 100);
        var reduced = state.network.ProcessMonth(1, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(reduced.CompletedEngines, Is.EqualTo(2)); Assert.That(reduced.CompletedParts, Is.EqualTo(5));
        Assert.That(reduced.UsedManDays, Is.EqualTo(205)); Assert.That(state.network.CreateSnapshot().WorkOrders, Is.Empty);
    }

    [Test]
    public void NewGuaranteedDemand_PromotesOnlyNeededQuantityWithoutDiscardingOrDuplicatingPaidWip()
    {
        var state = Plant(9600); var site = state.network.Sites[0]; var item = state.catalog["engine.a"];
        state.network.Configure(site.SiteId, item, FactoryAutomationTier.HandBuilt, 4, 0, double.MaxValue, out _, out _);
        var first = state.network.ProcessMonth(0, state.catalog, Array.Empty<EngineManufacturingDemand>());
        var next = state.network.ProcessMonth(1, state.catalog, new[]
        {
            new EngineManufacturingDemand(item.EngineDesignId, item.PartsGroupId, 1, 0),
            new EngineManufacturingDemand(item.EngineDesignId, item.PartsGroupId, 1, 0, false, true),
        });
        Assert.That(next.CompletedEngines, Is.EqualTo(1)); Assert.That(next.UsedManDays, Is.EqualTo(8400));
        Assert.That(first.MaterialExpenses + next.MaterialExpenses, Is.EqualTo(1750).Within(.000001));
        var pending = state.network.CreateSnapshot().WorkOrders;
        Assert.That(pending.Single(order => order.Priority == 1).CompletedManDays, Is.EqualTo(7200));
        Assert.That(pending.Single(order => order.Priority == 1).RemainingUnits, Is.EqualTo(1));
        Assert.That(pending.Single(order => order.Priority == 2).CompletedManDays, Is.Zero);
        Assert.That(pending.Single(order => order.Priority == 2).RemainingUnits, Is.EqualTo(2));
        Assert.That(pending.Sum(order => order.CompletedManDays) + state.network.GetEngineUnits(item.EngineDesignId) * 9600,
            Is.EqualTo(first.UsedManDays + next.UsedManDays));
    }

    [Test]
    public void SharedGroupTargets_CountNetworkStockAndPendingOnceAcrossDifferentConfiguredVersions()
    {
        var state = Plant(); var first = state.network.Sites[0]; var anchor = state.catalog["engine.a"];
        state.network.Configure(first.SiteId, anchor, FactoryAutomationTier.HandBuilt, 3, 20, double.MaxValue, out _, out _);
        state.network.TryConstruct(double.MaxValue, out _, out _);
        for (var month = 0; month < 18; month++) state.network.AdvanceMonth();
        var variant = new EngineManufacturingCatalogItem("engine.b", anchor.PartsGroupId, 500, 50);
        state.catalog[variant.EngineDesignId] = variant;
        state.network.Configure(state.network.Sites[1].SiteId, variant, FactoryAutomationTier.HandBuilt, 2, 30, double.MaxValue, out _, out _);
        var demand = new[] { new EngineManufacturingDemand(anchor.EngineDesignId, anchor.PartsGroupId, 4, 40) };
        var report = state.network.ProcessMonth(0, state.catalog, demand);
        Assert.That(state.network.GetEngineUnits(anchor.EngineDesignId), Is.EqualTo(4));
        Assert.That(state.network.GetEngineUnits(variant.EngineDesignId), Is.EqualTo(2));
        Assert.That(state.network.GetPartUnits(anchor.PartsGroupId), Is.EqualTo(50));
        Assert.That(report.UsedManDays, Is.EqualTo(550)); Assert.That(state.network.CreateSnapshot().WorkOrders, Is.Empty);
        var unchanged = state.network.ProcessMonth(1, state.catalog, demand);
        Assert.That(unchanged.CompletedEngines + unchanged.CompletedParts, Is.Zero);
        Assert.That(unchanged.UsedManDays, Is.Zero); Assert.That(state.network.CreateSnapshot().WorkOrders, Is.Empty);
    }

    [Test]
    public void PartsStockTopUp_ConsumesRealSharedCapacityAndLeavesOnlySpecificMotorWip()
    {
        var state = Plant(); var site = state.network.Sites[0]; var item = state.catalog["engine.a"];
        state.network.Configure(site.SiteId, item, FactoryAutomationTier.HandBuilt, 200, 2000, double.MaxValue, out _, out _);
        var partsFirst = state.network.ProcessMonth(0, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(partsFirst.CompletedParts, Is.EqualTo(1680)); Assert.That(partsFirst.CompletedEngines, Is.EqualTo(67));
        Assert.That(partsFirst.PartsManDaysBySite[site.SiteId], Is.EqualTo(1680));
        Assert.That(partsFirst.ProductionManDaysBySite[site.SiteId], Is.EqualTo(6720));
        Assert.That(partsFirst.UsedManDays, Is.EqualTo(site.AvailableManDays));
        var enginesNext = state.network.ProcessMonth(1, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(enginesNext.CompletedParts, Is.EqualTo(320)); Assert.That(enginesNext.CompletedEngines, Is.EqualTo(81));
        Assert.That(enginesNext.UsedManDays, Is.EqualTo(8400));
        Assert.That(state.network.GetPartUnits(item.PartsGroupId), Is.EqualTo(2000));
    }

    [TestCase(1)]
    [TestCase(25)]
    [TestCase(100)]
    public void OutstandingPartsDemandFillsItsMaximumShareBeforeEnginesReceiveRemainder(int percent)
    {
        var state = Plant(); var site = state.network.Sites[0];
        state.network.SetPartsAllocationPercent(site.SiteId, percent);
        var report = state.network.ProcessMonth(0, state.catalog,
            new[] { new EngineManufacturingDemand("engine.a", "group.a", 10000, 10000) });
        var partsQuota = site.AvailableManDays * percent / 100d;
        var productionQuota = site.AvailableManDays - partsQuota;
        Assert.That(report.PartsManDaysBySite[site.SiteId], Is.EqualTo(partsQuota).Within(1e-8));
        Assert.That(report.ProductionManDaysBySite[site.SiteId], Is.EqualTo(productionQuota).Within(1e-8));
        Assert.That(report.UsedManDays, Is.EqualTo(site.AvailableManDays).Within(1e-8));
        Assert.That(report.CompletedParts, Is.EqualTo((int)System.Math.Floor(partsQuota)));
        Assert.That(report.CompletedEngines, Is.EqualTo((int)System.Math.Floor(productionQuota / 100d)));
    }

    [Test]
    public void IdlePartsQuotaIsReusedForEnginesWithinTheWorkforceBudget()
    {
        var state = Plant(); var site = state.network.Sites[0];
        state.network.SetPartsAllocationPercent(site.SiteId, 25);
        state.network.SetWorkforceEmployeeLimit(site.SiteId, 10);
        var report = state.network.ProcessMonth(0, state.catalog,
            new[] { new EngineManufacturingDemand("engine.a", "group.a", 100, 0) });
        Assert.That(site.AvailableManDays, Is.EqualTo(210));
        Assert.That(report.UsedManDays, Is.EqualTo(210));
        Assert.That(report.CompletedParts, Is.Zero);
        Assert.That(report.CompletedEngines, Is.EqualTo(2));
        Assert.That(state.network.CreateSnapshot().WorkOrders.Sum(order => order.CompletedManDays), Is.EqualTo(10));
    }

    [Test]
    [Category("Extended")]
    public void AllocationDefaultClampingAndSnapshotRetainFutureMonthlyPolicy()
    {
        var state = Plant(); var site = state.network.Sites[0];
        Assert.That(site.PartsAllocationPercent, Is.EqualTo(20));
        state.network.SetPartsAllocationPercent(site.SiteId, 0);
        Assert.That(site.PartsAllocationPercent, Is.EqualTo(1));
        state.network.SetPartsAllocationPercent(site.SiteId, 101);
        Assert.That(site.PartsAllocationPercent, Is.EqualTo(100));
        state.network.SetPartsAllocationPercent(site.SiteId, 25);
        var snapshot = state.network.CreateSnapshot();
        Assert.That(snapshot.Sites[0].PartsAllocationPercent, Is.EqualTo(25));
        var restored = new EngineManufacturingNetwork(); restored.RestoreSnapshot(snapshot);
        snapshot.Sites[0].PartsAllocationPercent = 50;
        Assert.That(restored.Sites[0].PartsAllocationPercent, Is.EqualTo(25));
        var report = restored.ProcessMonth(0, state.catalog,
            new[] { new EngineManufacturingDemand("engine.a", "group.a", 10000, 10000) });
        Assert.That(report.PartsManDaysBySite[site.SiteId], Is.EqualTo(site.AvailableManDays * .25d));
        Assert.That(new EngineManufacturingSite().PartsAllocationPercent, Is.EqualTo(20));
    }

    [Test]
    public void EngineWipKeepsPaidWorkAndBorrowsUnusedQuotaAfterPartsReserveIsSatisfied()
    {
        var state = Plant(); var site = state.network.Sites[0];
        state.network.SetWorkforceEmployeeLimit(site.SiteId, 1);
        state.network.SetPartsAllocationPercent(site.SiteId, 25);
        var demand = new[] { new EngineManufacturingDemand("engine.a", "group.a", 1, 10) };
        var first = state.network.ProcessMonth(0, state.catalog, demand);
        var paid = state.network.CreateSnapshot().WorkOrders.Single(order => !order.IsParts).CompletedManDays;
        Assert.That(paid, Is.EqualTo(15.75));
        state.network.SetPartsAllocationPercent(site.SiteId, 100);
        var paused = state.network.ProcessMonth(1, state.catalog, demand);
        Assert.That(paused.CompletedEngines, Is.Zero);
        Assert.That(state.network.CreateSnapshot().WorkOrders.Single(order => !order.IsParts).CompletedManDays,
            Is.EqualTo(paid + 16.25));
        state.network.SetPartsAllocationPercent(site.SiteId, 1);
        var resumed = state.network.ProcessMonth(2, state.catalog, demand);
        Assert.That(resumed.ProductionManDaysBySite[site.SiteId], Is.EqualTo(21).Within(1e-10));
        Assert.That(state.network.CreateSnapshot().WorkOrders.Single(order => !order.IsParts).CompletedManDays,
            Is.EqualTo(paid + 16.25 + 21).Within(1e-10));
        Assert.That(first.UsedManDays, Is.LessThanOrEqualTo(21));
        Assert.That(paused.UsedManDays, Is.LessThanOrEqualTo(21));
    }

    [Test]
    public void MultiplePlantsWithMaximumLegalTargetsSaturateAggregatesWithoutBreakingFiniteBudgets()
    {
        var state = Plant(); var item = state.catalog["engine.a"];
        Assert.That(state.network.TryConstruct(double.MaxValue, out _, out _), Is.True);
        for (var month = 0; month < 18; month++) state.network.AdvanceMonth();
        foreach (var site in state.network.Sites)
            Assert.That(state.network.Configure(site.SiteId, item, FactoryAutomationTier.HandBuilt,
                int.MaxValue, int.MaxValue, double.MaxValue, out _, out _), Is.True);
        var demand = new[]
        {
            new EngineManufacturingDemand(item.EngineDesignId, item.PartsGroupId, int.MaxValue, int.MaxValue),
            new EngineManufacturingDemand(item.EngineDesignId, item.PartsGroupId, int.MaxValue, int.MaxValue, false, true),
        };
        for (var month = 0; month < 2; month++)
        {
            var report = state.network.ProcessMonth(month, state.catalog, demand);
            Assert.That(double.IsNaN(report.UsedManDays) || double.IsInfinity(report.UsedManDays), Is.False);
            Assert.That(report.UsedManDays, Is.LessThanOrEqualTo(state.network.Sites.Sum(site => site.AvailableManDays)));
            Assert.That(report.CompletedEngines, Is.GreaterThan(0));
            Assert.That(report.CompletedParts, Is.GreaterThan(0));
            foreach (var group in state.network.CreateSnapshot().WorkOrders.GroupBy(order => order.IsParts))
                Assert.That(group.Sum(order => (long)order.RemainingUnits), Is.LessThanOrEqualTo((long)int.MaxValue));
        }
        Assert.That(state.network.Sites.All(site => site.MonthlyEngineTarget == int.MaxValue && site.PartReserve == int.MaxValue), Is.True);
    }

    [Test]
    public void FullPartsReserveReleasesAllCapacityToEnginesEvenAtHundredPercentAllocation()
    {
        var state = Plant(); var site = state.network.Sites[0]; var item = state.catalog["engine.a"];
        state.network.SetPartsAllocationPercent(site.SiteId, 100);
        state.network.Configure(site.SiteId, item, FactoryAutomationTier.HandBuilt, 0, 20, double.MaxValue, out _, out _);
        var stocked = state.network.ProcessMonth(0, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(stocked.CompletedParts, Is.EqualTo(20));
        Assert.That(state.network.GetPartUnits(item.PartsGroupId), Is.EqualTo(20));
        state.network.Configure(site.SiteId, item, FactoryAutomationTier.HandBuilt, 100, 20, double.MaxValue, out _, out _);
        var report = state.network.ProcessMonth(1, state.catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(report.CompletedParts, Is.Zero);
        Assert.That(report.PartsManDaysBySite[site.SiteId], Is.Zero);
        Assert.That(report.ProductionManDaysBySite[site.SiteId], Is.EqualTo(site.AvailableManDays));
        Assert.That(report.CompletedEngines, Is.EqualTo(84));
        Assert.That(state.network.GetPartUnits(item.PartsGroupId), Is.EqualTo(20));
        Assert.That(site.MonthlyEngineTarget, Is.EqualTo(100));
        Assert.That(site.PartReserve, Is.EqualTo(20));
    }

    [Test]
    public void PartialPartsNeedReleasesOnlyUnusedWorkAndPreservesExactEngineWipAndBilling()
    {
        var state = Plant(); var site = state.network.Sites[0];
        state.network.SetPartsAllocationPercent(site.SiteId, 100);
        var demand = new[]
        {
            new EngineManufacturingDemand("engine.a", "group.a", 100, 0),
            new EngineManufacturingDemand("engine.a", "group.a", 0, 5, false),
        };
        var report = state.network.ProcessMonth(0, state.catalog, demand);
        Assert.That(report.CompletedParts, Is.EqualTo(5));
        Assert.That(report.PartsManDaysBySite[site.SiteId], Is.EqualTo(5));
        Assert.That(report.ProductionManDaysBySite[site.SiteId], Is.EqualTo(8395));
        Assert.That(report.UsedManDays, Is.EqualTo(8400));
        Assert.That(report.CompletedEngines, Is.EqualTo(83));
        Assert.That(state.network.CreateSnapshot().WorkOrders.Single().CompletedManDays, Is.EqualTo(95));
        Assert.That(report.LaborExpenses, Is.EqualTo(8400 * 80));
        Assert.That(report.MaterialExpenses, Is.EqualTo(84000).Within(1e-8));
        Assert.That(report.EquipmentExpenses, Is.EqualTo(site.EquipmentCost
            * FactoryBalance.GetMonthlyActiveEquipmentCostShare(site.AutomationTier)));
        Assert.Throws<InvalidOperationException>(() => state.network.ProcessMonth(0, state.catalog, demand));
        Assert.That(state.network.GetEngineUnits("engine.a"), Is.EqualTo(83));
        Assert.That(state.network.GetPartUnits("group.a"), Is.EqualTo(5));
        var restored = new EngineManufacturingNetwork(); restored.RestoreSnapshot(state.network.CreateSnapshot());
        Assert.That(restored.CreateSnapshot().WorkOrders.Single().CompletedManDays, Is.EqualTo(95));
    }

    [TestCase(0, 60)]
    [TestCase(0, 1000)]
    [TestCase(1, 60)]
    [TestCase(1, 1000)]
    [TestCase(0, 0)]
    [Category("Extended")]
    public void EngineTargets_ReleaseCapacityToPartsAndStopAtActualDemand(int engineTarget, int partsTarget)
    {
        var state = Plant(); var site = state.network.Sites[0];
        state.network.SetWorkforceEmployeeLimit(site.SiteId, 10);
        state.network.SetPartsAllocationPercent(site.SiteId, 25);
        var report = state.network.ProcessMonth(0, state.catalog,
            new[] { new EngineManufacturingDemand("engine.a", "group.a", engineTarget, partsTarget) });
        var engineWork = engineTarget * 100d;
        var partsWork = System.Math.Min(partsTarget, 210d - engineWork);
        Assert.That(report.CompletedEngines, Is.EqualTo(engineTarget));
        Assert.That(report.CompletedParts, Is.EqualTo((int)partsWork));
        Assert.That(report.UsedManDays, Is.EqualTo(engineWork + partsWork));
        Assert.That(report.LaborExpenses, Is.EqualTo((engineWork + partsWork) * ProductionEconomyBalance.LaborCostPerManDay));
        Assert.That(report.MaterialExpenses, Is.EqualTo(engineTarget * 1000d + partsWork * 10d));
        Assert.That(report.UsedManDays, Is.LessThanOrEqualTo(210));
        var restored = new EngineManufacturingNetwork(); restored.RestoreSnapshot(state.network.CreateSnapshot());
        Assert.That(restored.GetEngineUnits("engine.a"), Is.EqualTo(engineTarget));
        Assert.That(restored.GetPartUnits("group.a"), Is.EqualTo((int)partsWork));
    }

    [Test]
    public void IdleEngineCapacityIsFullyReusedForOutstandingParts()
    {
        var state = Plant(); var site = state.network.Sites[0];
        state.network.SetPartsAllocationPercent(site.SiteId, 20);
        var report = state.network.ProcessMonth(0, state.catalog,
            new[] { new EngineManufacturingDemand("engine.a", "group.a", 0, 10000) });
        Assert.That(report.CompletedEngines, Is.Zero);
        Assert.That(report.CompletedParts, Is.EqualTo(8400));
        Assert.That(report.UsedManDays, Is.EqualTo(8400));
        Assert.That(report.ProductionManDaysBySite[site.SiteId], Is.Zero);
    }

    [Test]
    public void ActualMotorStockBasis_WeightsBothBatchesAndSurvivesConsumptionAndSave()
    {
        var state = Plant(); var item = state.catalog["engine.a"];
        var first = state.network.ProcessMonth(0, state.catalog, new[] { new EngineManufacturingDemand(item.EngineDesignId, item.PartsGroupId, 2, 4) });
        var changed = item with { MaterialCost = 2000, ManDays = 200 };
        var changedCatalog = new Dictionary<string, EngineManufacturingCatalogItem> { [item.EngineDesignId] = changed };
        var second = state.network.ProcessMonth(1, changedCatalog, new[] { new EngineManufacturingDemand(item.EngineDesignId, item.PartsGroupId, 4, 8) });
        var engineTotal = first.CompletedEngineCostBasis + second.CompletedEngineCostBasis;
        var partTotal = first.CompletedPartCostBasis + second.CompletedPartCostBasis;
        Assert.That(engineTotal, Is.GreaterThan(first.MaterialExpenses + first.LaborExpenses));
        var restored = new EngineManufacturingNetwork(); restored.RestoreSnapshot(state.network.CreateSnapshot());
        restored.InitializeInventoryCostBasis(state.catalog);
        Assert.That(restored.TryConsumeEngineUnits(item.EngineDesignId, 5, out var missed), Is.False); Assert.That(missed, Is.Zero);
        Assert.That(restored.TryConsumeEngineUnits(item.EngineDesignId, 2, out var engineCost), Is.True);
        Assert.That(engineCost, Is.EqualTo(engineTotal / 2d).Within(1e-8));
        Assert.That(restored.TryConsumePartUnits(item.PartsGroupId, 4, out var partCost), Is.True);
        Assert.That(partCost, Is.EqualTo(partTotal / 2d).Within(1e-8));
        Assert.That(restored.TryConsumeEngineUnits(item.EngineDesignId, 1), Is.True);
        var values = restored.GetInventoryValueEntries(changedCatalog);
        Assert.That(values["motor-engine:" + item.EngineDesignId].TotalValue, Is.EqualTo(engineTotal / 4d).Within(1e-8));
        Assert.That(values["motor-part:" + item.PartsGroupId].TotalValue, Is.EqualTo(partTotal / 2d).Within(1e-8));
    }

    [Test]
    [Category("Extended")]
    public void UnknownOpeningMotorBasis_IsEstimatedOnceBeforeConsumption()
    {
        var network = new EngineManufacturingNetwork();
        network.RestoreSnapshot(new EngineManufacturingSnapshot
        { EngineStock = new Dictionary<string,int> { ["a"] = 2 }, PartStock = new Dictionary<string,int> { ["shared"] = 3 } });
        Assert.Throws<InvalidOperationException>(() => network.TryConsumeEngineUnits("a", 1, out _));
        Assert.Throws<InvalidOperationException>(() => network.TryConsumePartUnits("shared", 1, out _));
        var item = new EngineManufacturingCatalogItem("a", "shared", 1000, 100) { IsPartsGroupAnchor = true };
        var catalog = new Dictionary<string, EngineManufacturingCatalogItem> { ["a"] = item };
        network.InitializeInventoryCostBasis(catalog);
        var snapshot = network.CreateSnapshot();
        network.InitializeInventoryCostBasis(new Dictionary<string, EngineManufacturingCatalogItem> { ["a"] = item with { MaterialCost = 1 } });
        Assert.That(network.TryConsumeEngineUnits("a", 2, out var enginesCost), Is.True);
        Assert.That(enginesCost, Is.EqualTo(snapshot.EngineStockCostBasis!["a"]).Within(1e-8));
        Assert.That(enginesCost, Is.GreaterThan(2 * 9000));
        Assert.That(network.TryConsumePartUnits("shared", 3, out var partsCost), Is.True);
        Assert.That(partsCost, Is.EqualTo(snapshot.PartStockCostBasis!["shared"]).Within(1e-8));
    }

    [Test]
    public void WarrantyMetadata_DoesNotChangeManufacturingResults()
    {
        var guaranteed = Plant(); var optional = Plant();
        var first = guaranteed.network.ProcessMonth(0, guaranteed.catalog, new[] { new EngineManufacturingDemand("engine.a", "group.a", 100, 10000, true) });
        var second = optional.network.ProcessMonth(0, optional.catalog, new[] { new EngineManufacturingDemand("engine.a", "group.a", 100, 10000, false) });
        Assert.That(first.CompletedParts, Is.EqualTo(second.CompletedParts));
        Assert.That(first.CompletedEngines, Is.EqualTo(second.CompletedEngines));
        Assert.That(first.CompletedPartCostBasis, Is.EqualTo(second.CompletedPartCostBasis));
        Assert.That(first.CompletedEngineCostBasis, Is.EqualTo(second.CompletedEngineCostBasis));
    }

    [Test]
    public void MotorWip_FrozenOverheadAndRecipeSurviveSaveAndLaterCatalogRepricing()
    {
        var state = Plant(20000);
        var demand = new[] { new EngineManufacturingDemand("engine.a", "group.a", 1, 0) };
        state.network.ProcessMonth(0, state.catalog, demand);
        var snapshot = state.network.CreateSnapshot(); var paid = snapshot.WorkOrders.Single();
        Assert.That(paid.UnitOverheadCost, Is.GreaterThan(0d));
        var frozenCost = paid.UnitMaterialCost + paid.UnitManDays * ProductionEconomyBalance.LaborCostPerManDay + paid.UnitOverheadCost;
        snapshot.Sites[0].EquipmentCost *= 10;
        var restored = new EngineManufacturingNetwork(); restored.RestoreSnapshot(snapshot);
        var changedCatalog = new Dictionary<string, EngineManufacturingCatalogItem>
            { ["engine.a"] = state.catalog["engine.a"] with { MaterialCost = 999999, ManDays = 100 } };
        restored.ProcessMonth(1, changedCatalog, demand);
        restored.ProcessMonth(2, changedCatalog, demand);
        Assert.That(restored.TryConsumeEngineUnits("engine.a", 1, out var consumedCost), Is.True);
        Assert.That(consumedCost, Is.EqualTo(frozenCost).Within(1e-8));
        Assert.That(restored.CreateSnapshot().EngineStockCostBasis!["engine.a"], Is.Zero);
    }
}
