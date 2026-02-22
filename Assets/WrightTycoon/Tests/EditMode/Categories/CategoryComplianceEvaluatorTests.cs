using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Units;
using AircraftModel = WrightTycoon.Core.Aircraft.Aircraft;

namespace WrightTycoon.Core.Tests.Categories;

[TestFixture]
public sealed class CategoryComplianceEvaluatorTests
{
    [Test]
    public void Evaluate_ReturnsCompliant_WhenAircraftWithinCategoryBounds()
    {
        var evaluator = new CategoryComplianceEvaluator();
        var category = AircraftCategory.LightSportAndRecreational;
        var aircraft = CreateAircraft(
            category,
            crewCount: 2,
            speedAtSeaLevelMetersPerSecond: 24d,
            rangeKilometers: 520d,
            payloadKilograms: 260d,
            totalWeightKilograms: 900d);

        var result = evaluator.Evaluate(aircraft);

        Assert.That(result.IsCompliant, Is.True);
        Assert.That(result.UnmetRequirements.Count, Is.EqualTo(0));
    }

    [Test]
    public void Evaluate_ReturnsDetailedUnmetRequirements_WhenAircraftOutsideCategoryBounds()
    {
        var evaluator = new CategoryComplianceEvaluator();
        var category = AircraftCategory.LightSportAndRecreational;
        var aircraft = CreateAircraft(
            category,
            crewCount: 4,
            speedAtSeaLevelMetersPerSecond: 50d,
            rangeKilometers: 1200d,
            payloadKilograms: 100d,
            totalWeightKilograms: 2000d);

        var result = evaluator.Evaluate(aircraft);

        Assert.That(result.IsCompliant, Is.False);
        Assert.That(result.UnmetRequirements, Has.Some.Contains("Crew"));
        Assert.That(result.UnmetRequirements, Has.Some.Contains("Weight"));
        Assert.That(result.UnmetRequirements, Has.Some.Contains("Speed"));
        Assert.That(result.UnmetRequirements, Has.Some.Contains("Range"));
        Assert.That(result.UnmetRequirements, Has.Some.Contains("Payload"));
    }

    [Test]
    public void Evaluate_ReturnsWoodFabricWeightLimitWarning_WhenExceeded()
    {
        var evaluator = new CategoryComplianceEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.SpecialCivil,
            crewCount: 2,
            speedAtSeaLevelMetersPerSecond: 70d,
            rangeKilometers: 1000d,
            payloadKilograms: 3000d,
            totalWeightKilograms: 16000d,
            materialType: MaterialType.WoodFabric);

        var result = evaluator.Evaluate(aircraft);

        Assert.That(result.IsCompliant, Is.False);
        Assert.That(result.UnmetRequirements, Has.Some.Contains("Wood/Fabric limit exceeded"));
    }

    private static AircraftModel CreateAircraft(
        AircraftCategory category,
        int crewCount,
        double speedAtSeaLevelMetersPerSecond,
        double rangeKilometers,
        double payloadKilograms,
        double totalWeightKilograms,
        MaterialType materialType = MaterialType.Aluminum)
    {
        var family = new AircraftFamilyDefinition
        {
            Id = "fam.category.check",
            DisplayName = "Category Check Family",
            EngineDefinitionId = "eng.test",
            AirframeDefinitionId = "air.test",
            Category = category,
            LockedEngineType = EngineType.Radial,
            LockedStructureType = StructureType.Monocoque,
            LockedMaterialType = materialType,
            LockedEngineCount = 1,
            BaseEngineParameters = new EngineParameters(),
            BaseAirframeParameters = new AirframeParameters { CrewCount = crewCount, MaterialType = materialType },
        };

        var type = new AircraftTypeDefinition
        {
            Id = "type.category.check",
            FamilyId = family.Id,
            DisplayName = "Category Check Type",
        };

        var engineDefinition = new EngineDefinition
        {
            Id = "eng.test",
            DisplayName = "Test Engine",
            EngineType = EngineType.Radial,
        };

        var engine = new Engine(
            engineDefinition,
            new EngineParameters(),
            new EnginePerformanceReport(
                EngineOutputKind.PowerKilowatts,
                new[]
                {
                    new EngineAltitudePerformance(0d, 100d, 80d),
                    new EngineAltitudePerformance(5000d, 80d, 70d),
                    new EngineAltitudePerformance(10000d, 60d, 60d),
                },
                reliability: 0.9d,
                weight: Mass.FromKilograms(150d),
                cost: 20000d,
                productionManDays: 40d));

        var airframeParameters = new AirframeParameters { CrewCount = crewCount, MaterialType = materialType };
        var airframe = new Airframe(
            new AirframeDefinition
            {
                Id = "air.test",
                DisplayName = "Test Airframe",
                BaseStructuralWeightKilograms = 800d,
                ReferenceWingAreaSquareMeters = 20d,
                BaseDragCoefficient = 0.03d,
                BaseLiftCoefficient = 1.0d,
                BaseReliabilityModifier = 1.0d,
                BaseCost = 50000d,
                BaseProductionManDays = 120d,
            },
            airframeParameters,
            new AirframePerformanceReport(
                Mass.FromKilograms(700d),
                DragCoefficient: 0.04d,
                LiftCoefficient: 1.05d,
                Maneuverability: 70d,
                ProductionCost: 60000d,
                ProductionManDays: 140d,
                ReliabilityModifier: 1.0d,
                ReferenceLiftAtSeaLevel100Ms: Force.FromNewtons(90000d),
                ReferenceDragAtSeaLevel100Ms: Force.FromNewtons(4200d)));

        var performance = new AircraftPerformanceReport(
            new[]
            {
                new AircraftPerformancePoint(0d, speedAtSeaLevelMetersPerSecond),
                new AircraftPerformancePoint(5000d, speedAtSeaLevelMetersPerSecond),
                new AircraftPerformancePoint(10000d, speedAtSeaLevelMetersPerSecond),
            },
            rangeKilometers,
            Mass.FromKilograms(payloadKilograms),
            TotalProductionCost: 100000d,
            TotalProductionManDays: 200d,
            OverallReliability: 0.9d,
            TotalWeight: Mass.FromKilograms(totalWeightKilograms));

        var development = new DevelopmentCostReport(
            ChangedParameterCount: 0,
            ChangeMagnitudeScore: 0d,
            DevelopmentCost: 80000d,
            DevelopmentManDays: 220d);

        return new AircraftModel(family, type, engine, airframe, performance, development);
    }
}
