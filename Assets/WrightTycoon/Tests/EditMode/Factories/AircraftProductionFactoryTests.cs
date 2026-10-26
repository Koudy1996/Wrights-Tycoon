using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Units;
using AircraftModel = WrightTycoon.Core.Aircraft.Aircraft;

namespace WrightTycoon.Core.Tests.Factories;

[TestFixture]
public sealed class AircraftProductionFactoryTests
{
    [Test]
    public void ActiveAutomaticFactoryStoresEquipmentAndDiscardsOnlyUnfinishedWork()
    {
        var network = BuildOperationalFactoryNetwork(2);
        var source = network.Factories[0].FactoryId;
        var target = network.Factories[1].FactoryId;
        network.TryPurchaseAndInstallEquipment(source, "type", FactoryAutomationTier.HandBuilt, 100d, double.MaxValue, out _, out _);
        var snapshot = network.CreateSnapshot();
        snapshot.AutomaticStockReplenishment = true;
        snapshot.AircraftStockTargets["type"] = 1000;
        snapshot.WarehouseEntries.Add(new FactoryWarehouseEntrySnapshot { DesignId = "type", Units = 7 });
        snapshot.Factories[0].Line!.CarryOverManDays = 100d;
        snapshot.Factories[0].Line!.MonthlyProductionTargetUnits = 1000;
        snapshot.SupportWorkOrders.Add(new FactorySupportWorkOrderSnapshot
        { DesignId = "type", FactoryId = source, RemainingUnits = 100, CompletedManDays = 5d,
            UnitManDays = 10d, UnitMaterialCost = 20d, UnitOverheadCost = 2d });
        network.RestoreSnapshot(snapshot);
        Assert.That(network.TryStoreInstalledEquipment(source, out _), Is.True);
        Assert.That(network.TryStoreInstalledEquipment(source, out _), Is.False);
        var equipmentId = network.Equipment.Single().EquipmentId;
        network.RestoreSnapshot(network.CreateSnapshot());
        Assert.That(network.Factories[0].Line!.CarryOverManDays, Is.Zero);
        Assert.That(network.Factories[0].Line!.AssignedDesignId, Is.Null);
        Assert.That(network.CreateSnapshot().SupportWorkOrders, Is.Empty);
        Assert.That(network.GetWarehouseUnits("type"), Is.EqualTo(7));
        Assert.That(network.TryReinstallEquipment(equipmentId, target, double.MaxValue, out _, out _), Is.True);
        Assert.That(network.Factories[1].Line!.CarryOverManDays, Is.Zero);
        Assert.That(network.CreateSnapshot().SupportWorkOrders, Is.Empty);
    }

    [Test]
    public void FactoryConstruction_CostDurationAndMaintenanceMatchBalance()
    {
        var network = new AircraftProductionFactory();
        Assert.That(network.TryStartFactoryConstruction(FactoryBalance.FactoryConstructionCost, out var cost, out _), Is.True);
        Assert.That(cost, Is.EqualTo(80_000_000d));
        for (var month = 0; month < 17; month++) network.AdvanceMonth();
        Assert.That(network.OperationalFactoryCount, Is.Zero);
        network.AdvanceMonth();
        Assert.That(network.OperationalFactoryCount, Is.EqualTo(1));
        Assert.That(network.Factories[0].Line, Is.Not.Null);
        Assert.That(network.GetMonthlyFactoryMaintenance(), Is.EqualTo(600_000d));
    }

    [Test]
    public void FactoryNetwork_AllowsUnlimitedConcurrentFactories()
    {
        var network = new AircraftProductionFactory();
        Assert.That(network.TryStartFactoryConstruction(79_999_999d, out _, out _), Is.False);
        Assert.That(network.TryStartFactoryConstruction(double.MaxValue, out _, out _), Is.True);
        Assert.That(network.TryStartFactoryConstruction(double.MaxValue, out _, out _), Is.True);
        Assert.That(network.TryStartFactoryConstruction(double.MaxValue, out _, out _), Is.True);
        Assert.That(network.Factories.Count, Is.EqualTo(3));
        Assert.That(network.Factories.Select(item => item.FactoryId).Distinct().Count(), Is.EqualTo(3));
        for (var month = 0; month < FactoryBalance.FactoryConstructionMonths; month++) network.AdvanceMonth();
        Assert.That(network.OperationalFactoryCount, Is.EqualTo(3));
        Assert.That(network.GetMonthlyFactoryMaintenance(), Is.EqualTo(1_800_000d));
    }

    [TestCase(FactoryAutomationTier.HandBuilt, 1d, 10, 5d)]
    [TestCase(FactoryAutomationTier.JigsAndFixtures, 0.90d, 25, 45d)]
    [TestCase(FactoryAutomationTier.MechanizedCells, 0.78d, 50, 140d)]
    [TestCase(FactoryAutomationTier.AutomatedLine, 0.65d, 75, 300d)]
    [TestCase(FactoryAutomationTier.SophisticatedLine, 0.50d, 100, 455d)]
    [Category("Extended")]
    public void AutomationTier_UsesConfiguredEconomics(FactoryAutomationTier tier, double mdMultiplier, int maxOutput, double costMultiplier)
    {
        Assert.That(FactoryBalance.GetProductionManDayMultiplier(tier), Is.EqualTo(mdMultiplier));
        Assert.That(FactoryBalance.GetMaximumMonthlyOutput(tier), Is.EqualTo(maxOutput));
        Assert.That(FactoryBalance.GetPurchaseCostMultiplier(tier), Is.EqualTo(costMultiplier));
    }

    [Test]
    public void SophisticatedLine_GrossLaborBreakEvenUsesFixedEquipmentPrices()
    {
        const double laborCost = 16_000d;
        var premium = FactoryBalance.GetEquipmentPurchaseCost(FactoryAutomationTier.SophisticatedLine, FactoryBuildingSize.Small)
            - FactoryBalance.GetEquipmentPurchaseCost(FactoryAutomationTier.HandBuilt, FactoryBuildingSize.Small);
        var savingPerUnit = laborCost * (1d - FactoryBalance.GetProductionManDayMultiplier(FactoryAutomationTier.SophisticatedLine));
        Assert.That(premium / savingPerUnit, Is.EqualTo(11_125d));
    }

    [TestCase(FactoryAutomationTier.HandBuilt, 1_000_000d)]
    [TestCase(FactoryAutomationTier.JigsAndFixtures, 8_000_000d)]
    [TestCase(FactoryAutomationTier.MechanizedCells, 25_000_000d)]
    [TestCase(FactoryAutomationTier.AutomatedLine, 55_000_000d)]
    [TestCase(FactoryAutomationTier.SophisticatedLine, 90_000_000d)]
    [Category("Extended")]
    public void EquipmentPriceDependsOnlyOnTierAndBuildingSize(FactoryAutomationTier tier, double smallPrice)
    {
        foreach (var size in new[] { FactoryBuildingSize.Small, FactoryBuildingSize.Medium, FactoryBuildingSize.Large })
        {
            var multiplier = size == FactoryBuildingSize.Small ? 1 : size == FactoryBuildingSize.Medium ? 3 : 9;
            var state = new FactoryStateSnapshot();
            state.Factories.Add(new FactorySiteSnapshot { FactoryId = "s", BuildingSize = (int)size, CapacitySchemaVersion = 1, Line = new FactoryLineSnapshot() });
            foreach (var md in new[] { 10d, 10_000d })
            {
                var network = new AircraftProductionFactory(); network.RestoreSnapshot(state);
                Assert.That(network.TryPurchaseAndInstallEquipment("s", "type", tier, md, double.MaxValue, out var cost, out _), Is.True);
                Assert.That(cost, Is.EqualTo(smallPrice * multiplier));
                Assert.That(network.Equipment[0].BuildingSize, Is.EqualTo(size));
                Assert.That(network.Equipment[0].BaselineProductionManDays, Is.EqualTo(md));
            }
        }
    }

    [Test]
    [Category("Extended")]
    public void StoredEquipmentRetainsSizeRecipeAndPriceAcrossSaveAndRejectsOtherSizes()
    {
        var network = BuildOperationalFactoryNetwork(2);
        network.TryPurchaseAndInstallEquipment(network.Factories[0].FactoryId, "type", FactoryAutomationTier.HandBuilt, 123d, double.MaxValue, out _, out _);
        Assert.That(network.TryStartFactoryExpansion(network.Factories[0].FactoryId, double.MaxValue, out _, out _), Is.False);
        Assert.That(network.TryStoreInstalledEquipment(network.Factories[0].FactoryId, out _), Is.True);
        var snapshot = network.CreateSnapshot(); snapshot.Factories[1].BuildingSize = (int)FactoryBuildingSize.Large;
        network.RestoreSnapshot(snapshot);
        var equipment = network.Equipment[0];
        Assert.That(network.GetMonthlyEquipmentStorageCost(), Is.EqualTo(100d));
        Assert.That(network.TryReinstallEquipment(equipment.EquipmentId, network.Factories[1].FactoryId, double.MaxValue, out _, out _), Is.False);
        Assert.That(network.TryReinstallEquipment(equipment.EquipmentId, network.Factories[0].FactoryId, 29_999d, out _, out _), Is.False);
        Assert.That(network.TryReinstallEquipment(equipment.EquipmentId, network.Factories[0].FactoryId, 30_000d, out var fee, out _), Is.True);
        Assert.That(fee, Is.EqualTo(30_000d));
        Assert.That(network.Factories[0].BaselineAircraftManDays, Is.EqualTo(123d));
        Assert.That(network.GetMonthlyEquipmentStorageCost(), Is.Zero);
    }

    [Test]
    public void EquipmentDepot_IsCompanyWideAndAllowsDuplicateDesignEquipment()
    {
        var item = CreateCatalogItem("design.alpha", 100d, 2, 20d);
        var network = BuildOperationalFactoryNetwork(2);
        var md = TotalProductionManDays(item);
        Assert.That(network.TryPurchaseAndInstallEquipment(network.Factories[0].FactoryId, item.DesignId,
            FactoryAutomationTier.HandBuilt, md, double.MaxValue, out _, out _), Is.True);
        Assert.That(network.TryPurchaseAndInstallEquipment(network.Factories[1].FactoryId, item.DesignId,
            FactoryAutomationTier.AutomatedLine, md, double.MaxValue, out _, out _), Is.True);
        Assert.That(network.Equipment.Count, Is.EqualTo(2));
        Assert.That(network.Equipment.Select(item => item.EquipmentId).Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    [Category("Extended")]
    public void LegacyEquipmentInfersInstalledSizeWithoutRepricing()
    {
        var snapshot = new FactoryStateSnapshot();
        snapshot.Factories.Add(new FactorySiteSnapshot { FactoryId = "medium", BuildingSize = 1, CapacitySchemaVersion = 1,
            BaselineAircraftManDays = 321d, Line = new FactoryLineSnapshot() });
        snapshot.Equipment.Add(new FactoryEquipmentSnapshot { EquipmentId = "old", DesignId = "type", FactoryId = "medium", PurchaseCost = 12345d });
        var network = new AircraftProductionFactory(); network.RestoreSnapshot(snapshot);
        Assert.That(network.Equipment[0].BuildingSize, Is.EqualTo(FactoryBuildingSize.Medium));
        Assert.That(network.Equipment[0].PurchaseCost, Is.EqualTo(12345d));
        Assert.That(network.TryStoreInstalledEquipment("medium", out _), Is.True);
        network.RestoreSnapshot(network.CreateSnapshot());
        Assert.That(network.Equipment[0].BuildingSize, Is.EqualTo(FactoryBuildingSize.Medium));
        Assert.That(network.TryReinstallEquipment("old", "medium", double.MaxValue, out var cost, out _), Is.True);
        Assert.That(cost, Is.EqualTo(12345d * .03d));
        Assert.That(network.Factories[0].BaselineAircraftManDays, Is.EqualTo(321d));
    }

    [Test]
    public void StoredEquipment_IsFreeAndCanMoveToAnotherFactory()
    {
        var item = CreateCatalogItem("design.reinstall", 100d, 1, 10d);
        var network = BuildOperationalFactoryNetwork(2);
        var source = network.Factories[0];
        var target = network.Factories[1];
        network.TryPurchaseAndInstallEquipment(source.FactoryId, item.DesignId, FactoryAutomationTier.SophisticatedLine,
            TotalProductionManDays(item), double.MaxValue, out var purchaseCost, out _);
        for (var month = 0; month < 10; month++) network.AdvanceMonth();
        Assert.That(network.TryStoreInstalledEquipment(source.FactoryId, out _), Is.True);
        var equipment = network.Equipment[0];
        Assert.That(equipment.Location, Is.EqualTo(FactoryEquipmentLocation.Stored));
        Assert.That(network.GetMonthlyActiveEquipmentCost(), Is.Zero);
        Assert.That(network.TryReinstallEquipment(equipment.EquipmentId, target.FactoryId, double.MaxValue,
            out var reinstallCost, out _), Is.True);
        Assert.That(reinstallCost, Is.EqualTo(purchaseCost * 0.03d));
        Assert.That(equipment.FactoryId, Is.EqualTo(target.FactoryId));
        Assert.That(equipment.InstallationMonthsRemaining, Is.EqualTo(3));
    }

    [TestCase(FactoryAutomationTier.HandBuilt, 115)]
    [TestCase(FactoryAutomationTier.JigsAndFixtures, 128)]
    [TestCase(FactoryAutomationTier.MechanizedCells, 147)]
    [TestCase(FactoryAutomationTier.AutomatedLine, 177)]
    [TestCase(FactoryAutomationTier.SophisticatedLine, 230)]
    [Category("Extended")]
    public void MonthlyTarget_IsClampedToPhysicalCombinedMdCapacity(FactoryAutomationTier tier, int expectedMaximum)
    {
        var item = CreateCatalogItem($"design.{tier}", 100d, 1, 10d);
        var network = BuildOperationalFactoryNetwork();
        var factory = network.Factories[0];
        network.TryPurchaseAndInstallEquipment(factory.FactoryId, item.DesignId, tier, TotalProductionManDays(item),
            double.MaxValue, out _, out _);
        Assert.That(network.TrySetMonthlyProductionTargetUnits(factory.FactoryId, 999, out var applied, out _), Is.True);
        Assert.That(applied, Is.EqualTo(expectedMaximum));
        Assert.That(factory.Line!.MonthlyProductionTargetUnits, Is.EqualTo(expectedMaximum));
    }

    [Test]
    public void MonthlyTarget_SetDuringInstallation_PersistsAfterCompletion()
    {
        var item = CreateCatalogItem("design.plan", 100d, 2, 20d);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var network = BuildOperationalFactoryNetwork();
        var factory = network.Factories[0];
        network.TryPurchaseAndInstallEquipment(factory.FactoryId, item.DesignId, FactoryAutomationTier.AutomatedLine,
            TotalProductionManDays(item), double.MaxValue, out _, out _);
        network.TrySetMonthlyProductionTargetUnits(factory.FactoryId, 90, out var applied, out _);
        Assert.That(applied, Is.EqualTo(90));
        var blocked = network.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(blocked.TotalAircraftProduced, Is.Zero);
        Assert.That(factory.Line!.MonthlyProductionTargetUnits, Is.EqualTo(90));
        for (var month = 0; month < FactoryBalance.GetInitialInstallationMonths(FactoryAutomationTier.AutomatedLine); month++)
            network.AdvanceMonth();
        Assert.That(factory.Line.MonthlyProductionTargetUnits, Is.EqualTo(90));
        var produced = network.SimulateMonth(catalog, 1, SimulationDate.WrightTycoonStart.NextMonth());
        Assert.That(produced.TotalAircraftProduced, Is.EqualTo(90));
    }

    [Test]
    public void MultipleFactories_ProductionAndEconomicsAreAggregated()
    {
        var firstItem = CreateCatalogItem("design.first", 100d, 2, 20d);
        var secondItem = CreateCatalogItem("design.second", 50d, 1, 10d);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem>
        {
            [firstItem.DesignId] = firstItem,
            [secondItem.DesignId] = secondItem,
        };
        var network = BuildOperationalFactoryNetwork(2);
        network.TryPurchaseAndInstallEquipment(network.Factories[0].FactoryId, firstItem.DesignId,
            FactoryAutomationTier.HandBuilt, TotalProductionManDays(firstItem), double.MaxValue, out _, out _);
        network.TryPurchaseAndInstallEquipment(network.Factories[1].FactoryId, secondItem.DesignId,
            FactoryAutomationTier.SophisticatedLine, TotalProductionManDays(secondItem), double.MaxValue, out _, out _);
        for (var month = 0; month < 10; month++) network.AdvanceMonth();
        network.TrySetMonthlyProductionTargetUnits(network.Factories[0].FactoryId, 10, out _, out _);
        network.TrySetMonthlyProductionTargetUnits(network.Factories[1].FactoryId, 20, out _, out _);
        var report = network.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(report.TotalAircraftProduced, Is.EqualTo(30));
        Assert.That(report.LineReports.Count, Is.EqualTo(2));
        Assert.That(report.TotalFactoryMaintenanceExpenses, Is.EqualTo(1_200_000d));
        Assert.That(report.TotalActiveEquipmentExpenses, Is.GreaterThan(0d));
        Assert.That(report.LineReports[1].GrossLaborSavings, Is.GreaterThan(0d));
    }

    [Test]
    public void TotalTarget_StopsLineWithoutDeletingEquipment()
    {
        var item = CreateCatalogItem("design.target", 20d, 1, 6d);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var network = BuildOperationalFactoryNetwork();
        var factory = network.Factories[0];
        network.TryPurchaseAndInstallEquipment(factory.FactoryId, item.DesignId, FactoryAutomationTier.HandBuilt,
            TotalProductionManDays(item), double.MaxValue, out _, out _);
        network.TrySetMonthlyProductionTargetUnits(factory.FactoryId, 5, out _, out _);
        network.SetTotalProductionTargetUnits(factory.Line!.LineId, 8);
        network.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        var completionMonth = network.SimulateMonth(catalog, 1, SimulationDate.WrightTycoonStart.NextMonth());
        Assert.That(factory.Line.MonthlyProductionTargetUnits, Is.Zero);
        Assert.That(completionMonth.TotalActiveEquipmentExpenses, Is.GreaterThan(0d));
        Assert.That(network.Equipment.Count, Is.EqualTo(1));
        Assert.That(network.GetWarehouseUnits(item.DesignId), Is.EqualTo(8));
    }

    [Test]
    public void Snapshot_RestoresFactoriesEquipmentLocationsTargetsEconomicsAndWarehouse()
    {
        var item = CreateCatalogItem("design.snapshot", 30d, 1, 6d);
        var network = BuildOperationalFactoryNetwork(2);
        var factory = network.Factories[1];
        network.TryPurchaseAndInstallEquipment(factory.FactoryId, item.DesignId, FactoryAutomationTier.MechanizedCells,
            TotalProductionManDays(item), double.MaxValue, out _, out _);
        network.TrySetMonthlyProductionTargetUnits(factory.FactoryId, 999, out _, out _);
        network.AddWarehouseUnits(item.DesignId, 7);
        var restored = new AircraftProductionFactory();
        restored.RestoreSnapshot(network.CreateSnapshot());
        Assert.That(restored.Factories.Count, Is.EqualTo(2));
        Assert.That(restored.Equipment[0].FactoryId, Is.EqualTo(restored.Factories[1].FactoryId));
        Assert.That(restored.Factories[1].Line!.MonthlyProductionTargetUnits, Is.EqualTo(449));
        Assert.That(restored.GetWarehouseUnits(item.DesignId), Is.EqualTo(7));
    }

    [Test]
    public void StopProductionForDesign_ClearsTargetsButPreservesEquipmentAndWarehouse()
    {
        var item = CreateCatalogItem("design.decommission", 30d, 1, 6d);
        var network = BuildOperationalFactoryNetwork();
        var factory = network.Factories[0];
        Assert.That(
            network.TryPurchaseAndInstallEquipment(
                factory.FactoryId,
                item.DesignId,
                FactoryAutomationTier.MechanizedCells,
                TotalProductionManDays(item),
                double.MaxValue,
                out _,
                out _),
            Is.True);
        Assert.That(network.TrySetMonthlyProductionTargetUnits(factory.FactoryId, 20, out _, out _), Is.True);
        network.SetTotalProductionTargetUnits(factory.Line!.LineId, 100);
        network.AddWarehouseUnits(item.DesignId, 7);

        var stoppedLines = network.StopProductionForDesign(item.DesignId);

        Assert.That(stoppedLines, Is.EqualTo(1));
        Assert.That(factory.Line.MonthlyProductionTargetUnits, Is.Zero);
        Assert.That(factory.Line.TotalProductionTargetUnits, Is.Zero);
        Assert.That(network.Equipment.Single().DesignId, Is.EqualTo(item.DesignId));
        Assert.That(network.GetWarehouseUnits(item.DesignId), Is.EqualTo(7));
    }

    [Test]
    [Category("Extended")]
    public void LegacySingletonSnapshot_MigratesToFactoryOneAndCentralDepot()
    {
        var legacy = new FactoryStateSnapshot
        {
            HasFactoryAsset = true,
            FactoryOperatingMonths = 12,
            Lines = new List<FactoryLineSnapshot>
            {
                new() { LineId = "line.main", AssignedDesignId = "design.installed", MonthlyProductionTargetUnits = 100 },
            },
            Equipment = new List<FactoryEquipmentSnapshot>
            {
                new() { DesignId = "design.installed", AutomationTier = (int)FactoryAutomationTier.HandBuilt, IsInstalled = true, PurchaseCost = 1000d },
                new() { DesignId = "design.stored", AutomationTier = (int)FactoryAutomationTier.AutomatedLine, IsInstalled = false, PurchaseCost = 2000d },
            },
        };
        var restored = new AircraftProductionFactory();
        restored.RestoreSnapshot(legacy);
        Assert.That(restored.Factories.Single().FactoryId, Is.EqualTo("factory.001"));
        Assert.That(restored.Factories.Single().Line!.LineId, Is.EqualTo("factory.001.line.1"));
        Assert.That(restored.Factories.Single().Line!.MonthlyProductionTargetUnits, Is.EqualTo(100));
        Assert.That(restored.Equipment.Count(item => item.Location == FactoryEquipmentLocation.Stored), Is.EqualTo(1));
    }

    [Test]
    public void FactorySale_OnlyRequiresItsOwnLineToBeEmpty()
    {
        var item = CreateCatalogItem("design.stored", 50d, 1, 10d);
        var network = BuildOperationalFactoryNetwork(2);
        var first = network.Factories[0];
        var second = network.Factories[1];
        network.TryPurchaseAndInstallEquipment(first.FactoryId, item.DesignId, FactoryAutomationTier.HandBuilt,
            TotalProductionManDays(item), double.MaxValue, out _, out _);
        network.TryStoreInstalledEquipment(first.FactoryId, out _);
        for (var month = 0; month < 24; month++) network.AdvanceMonth();
        Assert.That(network.TrySellFactory(second.FactoryId, out var income, out _), Is.True);
        Assert.That(income, Is.EqualTo(80_000_000d * 0.88d));
        Assert.That(network.Factories.Count, Is.EqualTo(1));
        Assert.That(network.Equipment.Count, Is.EqualTo(1));
    }

    internal static AircraftProductionFactory BuildOperationalFactoryNetwork(int count = 1)
    {
        var network = new AircraftProductionFactory();
        for (var index = 0; index < count; index++) network.TryStartFactoryConstruction(double.MaxValue, out _, out _);
        for (var month = 0; month < FactoryBalance.FactoryConstructionMonths; month++) network.AdvanceMonth();
        return network;
    }

    private static double TotalProductionManDays(FactoryAircraftTypeCatalogItem item) =>
        item.Aircraft.Performance.AirframeProductionManDays + item.Aircraft.Performance.EnginesProductionManDays;

    internal static FactoryAircraftTypeCatalogItem CreateCatalogItem(string designId, double airframeMd, int engineCount, double enginesMd)
    {
        var engineDefinition = new EngineDefinition { Id = $"eng.{designId}", DisplayName = "Engine", EngineType = EngineType.Radial };
        var engine = new Engine(engineDefinition,
            new EngineParameters { DisplacementLiters = 10d, CompressionRatio = 8d, OptimizationAltitudeMeters = 1500d, CoolingEfficiency = 0.8d },
            new EnginePerformanceReport(EngineOutputKind.PowerKilowatts,
                new[] { new EngineAltitudePerformance(0d, 450d, 180d) }, 0.9d, Mass.FromKilograms(300d), 50_000d,
                enginesMd / System.Math.Max(engineCount, 1)));
        var parameters = new AirframeParameters { EngineCount = engineCount, StructureType = StructureType.Monocoque,
            MaterialType = MaterialType.Aluminum, WingPosition = WingPosition.Low, WingShape = WingShape.Straight,
            WingAreaSquareMeters = 34d, TailType = TailType.Classic, FlapType = FlapType.Fowler,
            LandingGearType = LandingGearType.SemiRetractable, FuelAmountLiters = 1200d, CrewCount = 2 };
        var definition = new AirframeDefinition { Id = $"air.{designId}", DisplayName = "Airframe",
            BaseStructuralWeightKilograms = 1200d, ReferenceWingAreaSquareMeters = 34d, BaseDragCoefficient = 0.03d,
            BaseLiftCoefficient = 1.05d, BaseReliabilityModifier = 1d, BaseCost = 70_000d, BaseProductionManDays = 140d };
        var airframe = new Airframe(definition, parameters, new AirframePerformanceReport(Mass.FromKilograms(2800d),
            0.045d, 1.15d, 72d, 95_000d, airframeMd, 1d, Force.FromNewtons(100_000d), Force.FromNewtons(5200d)));
        var family = new AircraftFamilyDefinition { Id = $"fam.{designId}", DisplayName = "Family", EngineDefinitionId = engineDefinition.Id,
            AirframeDefinitionId = definition.Id, LockedEngineType = EngineType.Radial, LockedStructureType = StructureType.Monocoque,
            LockedMaterialType = MaterialType.Aluminum, LockedEngineCount = engineCount, BaseEngineParameters = engine.Parameters.Clone(),
            BaseAirframeParameters = airframe.Parameters.Clone(), BaseDevelopmentCost = 90_000d, BaseDevelopmentManDays = 210d };
        var type = new AircraftTypeDefinition { Id = $"type.{designId}", FamilyId = family.Id, DisplayName = "Type" };
        var aircraft = new AircraftModel(family, type, engine, airframe,
            new AircraftPerformanceReport(new[] { new AircraftPerformancePoint(0d, 90d) }, 1800d, Mass.FromKilograms(3000d),
                220_000d, airframeMd + enginesMd, 0.88d, Mass.FromKilograms(3900d),
                AirframeProductionCost: 150_000d, EnginesProductionCost: 70_000d,
                AirframeProductionManDays: airframeMd, EnginesProductionManDays: enginesMd),
            new DevelopmentCostReport(0, 0d, 90_000d, 220d));
        return new FactoryAircraftTypeCatalogItem(designId, designId, aircraft);
    }
}
