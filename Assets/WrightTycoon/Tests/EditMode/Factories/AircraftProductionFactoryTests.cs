using System.Collections.Generic;
using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
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
    public void Production_CarriesOverUnusedManDaysAcrossMonths()
    {
        var factory = new AircraftProductionFactory();
        var lineId = factory.AddLine("line.main");
        factory.SetEmployees(lineId, 10);
        factory.AssignDesign(lineId, "design.alpha");

        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem>
        {
            ["design.alpha"] = CreateCatalogItem("design.alpha", "Alpha", productionManDaysPerAircraft: 100d, engineCount: 2, engineProductionManDays: 8d),
        };

        var month1 = factory.SimulateMonth(catalog, monthIndex: 0, SimulationDate.WrightTycoonStart);
        var month2 = factory.SimulateMonth(catalog, monthIndex: 1, SimulationDate.WrightTycoonStart.NextMonth());

        Assert.That(month1.TotalAircraftProduced, Is.EqualTo(2));
        Assert.That(month1.LineReports[0].CarryOverManDays, Is.EqualTo(10d).Within(0.0001d));
        Assert.That(month2.TotalAircraftProduced, Is.EqualTo(2));
        Assert.That(month2.LineReports[0].CarryOverManDays, Is.EqualTo(20d).Within(0.0001d));
    }

    [Test]
    public void Production_IsBlockedWhenToolingRequirementsNotMet()
    {
        var factory = new AircraftProductionFactory();
        var lineId = factory.AddLine("line.tools");
        factory.SetEmployees(lineId, 10);
        factory.AssignDesign(lineId, "design.beta");

        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem>
        {
            ["design.beta"] = CreateCatalogItem("design.beta", "Beta", productionManDaysPerAircraft: 60d, engineCount: 1, engineProductionManDays: 6d, requiredFactoryTool: 2, requiredLineTool: 3),
        };

        var blocked = factory.SimulateMonth(catalog, monthIndex: 0, SimulationDate.WrightTycoonStart);
        factory.UpgradeFactoryToolLevel(2);
        factory.UpgradeLineToolLevel(lineId, 3);
        var active = factory.SimulateMonth(catalog, monthIndex: 1, SimulationDate.WrightTycoonStart.NextMonth());

        Assert.That(blocked.TotalAircraftProduced, Is.EqualTo(0));
        Assert.That(blocked.LineReports[0].IsBlocked, Is.True);
        Assert.That(active.TotalAircraftProduced, Is.GreaterThan(0));
        Assert.That(active.LineReports[0].IsBlocked, Is.False);
    }

    [Test]
    public void EngineFactory_AutoScalesAndDiscardsExcessCapacity()
    {
        var factory = new AircraftProductionFactory();
        var lineId = factory.AddLine("line.eng");
        factory.SetEmployees(lineId, 10);
        factory.AssignDesign(lineId, "design.gamma");

        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem>
        {
            ["design.gamma"] = CreateCatalogItem("design.gamma", "Gamma", productionManDaysPerAircraft: 10d, engineCount: 2, engineProductionManDays: 8d),
        };

        var report = factory.SimulateMonth(catalog, monthIndex: 0, SimulationDate.WrightTycoonStart);

        Assert.That(report.TotalAircraftProduced, Is.EqualTo(21));
        Assert.That(report.EngineFactoryReport.TotalRequiredEngines, Is.EqualTo(42));
        Assert.That(report.EngineFactoryReport.TotalProducedEngines, Is.EqualTo(42));
        Assert.That(report.EngineFactoryReport.TotalDiscardedEngines, Is.EqualTo(0));
        Assert.That(report.EngineFactoryReport.TotalAutoAssignedEmployees, Is.EqualTo(16));
    }

    [Test]
    public void Production_AddsFinishedAircraftToWarehouse()
    {
        var factory = new AircraftProductionFactory();
        var lineId = factory.AddLine("line.warehouse");
        factory.SetEmployees(lineId, 10);
        factory.AssignDesign(lineId, "design.stock");

        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem>
        {
            ["design.stock"] = CreateCatalogItem("design.stock", "Stock", productionManDaysPerAircraft: 30d, engineCount: 2, engineProductionManDays: 8d),
        };

        var report = factory.SimulateMonth(catalog, monthIndex: 0, SimulationDate.WrightTycoonStart);

        Assert.That(report.TotalAircraftProduced, Is.EqualTo(7));
        Assert.That(factory.GetWarehouseUnits("design.stock"), Is.EqualTo(7));
        Assert.That(report.TotalWarehouseUnits, Is.EqualTo(7));
    }

    [Test]
    public void Warehouse_WithdrawHonorsAvailableStock()
    {
        var factory = new AircraftProductionFactory();
        factory.AddWarehouseUnits("design.stock", 12);

        var failed = factory.TryWithdrawFromWarehouse("design.stock", 20, out var failedMessage);
        var succeeded = factory.TryWithdrawFromWarehouse("design.stock", 7, out var successMessage);

        Assert.That(failed, Is.False);
        Assert.That(failedMessage, Does.Contain("Warehouse has 12 units"));
        Assert.That(succeeded, Is.True);
        Assert.That(successMessage, Does.Contain("Withdrew 7 units"));
        Assert.That(factory.GetWarehouseUnits("design.stock"), Is.EqualTo(5));
    }

    private static FactoryAircraftTypeCatalogItem CreateCatalogItem(
        string designId,
        string displayName,
        double productionManDaysPerAircraft,
        int engineCount,
        double engineProductionManDays,
        int requiredFactoryTool = 1,
        int requiredLineTool = 1)
    {
        var engineDefinition = new EngineDefinition
        {
            Id = $"eng.{designId}",
            DisplayName = $"Engine {displayName}",
            EngineType = EngineType.Radial,
        };

        var engine = new Engine(
            engineDefinition,
            new EngineParameters
            {
                DisplacementLiters = 10d,
                CompressionRatio = 8d,
                BoostType = BoostType.None,
                OptimizationAltitudeMeters = 1500d,
                CoolingEfficiency = 0.8d,
            },
            new EnginePerformanceReport(
                EngineOutputKind.PowerKilowatts,
                new[]
                {
                    new EngineAltitudePerformance(0d, 450d, 180d),
                    new EngineAltitudePerformance(5000d, 270d, 120d),
                    new EngineAltitudePerformance(10000d, 150d, 70d),
                },
                reliability: 0.9d,
                weight: Mass.FromKilograms(300d),
                cost: 50000d,
                productionManDays: engineProductionManDays));

        var airframeParameters = new AirframeParameters
        {
            EngineCount = engineCount,
            StructureType = StructureType.Monocoque,
            MaterialType = MaterialType.Aluminum,
            WingPosition = WingPosition.Low,
            WingShape = WingShape.Straight,
            WingAreaSquareMeters = 34d,
            TailType = TailType.Classic,
            FlapType = FlapType.Fowler,
            LandingGearType = LandingGearType.SemiRetractable,
            EngineCowling = 0.55d,
            FuelAmountLiters = 1200d,
            CrewCount = 2,
        };

        var airframe = new Airframe(
            new AirframeDefinition
            {
                Id = $"air.{designId}",
                DisplayName = $"Airframe {displayName}",
                BaseStructuralWeightKilograms = 1200d,
                ReferenceWingAreaSquareMeters = 34d,
                BaseDragCoefficient = 0.03d,
                BaseLiftCoefficient = 1.05d,
                BaseReliabilityModifier = 1d,
                BaseCost = 70000d,
                BaseProductionManDays = 140d,
            },
            airframeParameters,
            new AirframePerformanceReport(
                Mass.FromKilograms(2800d),
                0.045d,
                1.15d,
                72d,
                95000d,
                productionManDaysPerAircraft,
                1d,
                Force.FromNewtons(100000d),
                Force.FromNewtons(5200d)));

        var family = new AircraftFamilyDefinition
        {
            Id = $"fam.{designId}",
            DisplayName = $"Family {displayName}",
            EngineDefinitionId = engineDefinition.Id,
            AirframeDefinitionId = $"air.{designId}",
            LockedEngineType = EngineType.Radial,
            LockedStructureType = StructureType.Monocoque,
            LockedMaterialType = MaterialType.Aluminum,
            LockedEngineCount = engineCount,
            RequiredFactoryToolLevel = requiredFactoryTool,
            BaseEngineParameters = engine.Parameters.Clone(),
            BaseAirframeParameters = airframe.Parameters.Clone(),
            BaseDevelopmentCost = 90000d,
            BaseDevelopmentManDays = 210d,
        };

        var type = new AircraftTypeDefinition
        {
            Id = $"type.{designId}",
            FamilyId = family.Id,
            DisplayName = $"Type {displayName}",
            RequiredLineToolLevel = requiredLineTool,
        };

        var aircraft = new AircraftModel(
            family,
            type,
            engine,
            airframe,
            new AircraftPerformanceReport(
                new[]
                {
                    new AircraftPerformancePoint(0d, 90d),
                    new AircraftPerformancePoint(5000d, 84d),
                    new AircraftPerformancePoint(10000d, 78d),
                },
                1800d,
                Mass.FromKilograms(3000d),
                220000d,
                productionManDaysPerAircraft,
                0.88d,
                Mass.FromKilograms(3900d)),
            new DevelopmentCostReport(0, 0d, 90000d, 220d));

        return new FactoryAircraftTypeCatalogItem(
            designId,
            displayName,
            aircraft,
            requiredFactoryTool,
            requiredLineTool);
    }
}
