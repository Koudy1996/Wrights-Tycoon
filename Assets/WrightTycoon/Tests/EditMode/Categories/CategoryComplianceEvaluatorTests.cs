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
            totalWeightKilograms: 900d,
            constructionStandard: AircraftConstructionStandard.Ultralight);

        var result = evaluator.Evaluate(aircraft);

        Assert.That(result.IsCompliant, Is.True);
        Assert.That(result.UnmetRequirements.Count, Is.EqualTo(0));
    }

    [Test]
    public void Evaluate_UsesHighestSpeedAcrossAllAltitudeSamples()
    {
        var evaluator = new CategoryComplianceEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.LightSportAndRecreational,
            crewCount: 2,
            speedAtSeaLevelMetersPerSecond: 10d,
            rangeKilometers: 520d,
            payloadKilograms: 260d,
            totalWeightKilograms: 900d,
            constructionStandard: AircraftConstructionStandard.Ultralight,
            speedAtFiveKilometersMetersPerSecond: 24d,
            speedAtTenKilometersMetersPerSecond: 20d);

        var result = evaluator.Evaluate(aircraft);

        Assert.That(result.IsCompliant, Is.True);
        Assert.That(string.Join(" | ", result.UnmetRequirements), Does.Not.Contain("Speed"));
    }

    [Test]
    [Category("Extended")]
    public void Evaluate_RejectsSpeedWhenEveryAltitudeSampleIsBelowMinimum()
    {
        var evaluator = new CategoryComplianceEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.LightSportAndRecreational,
            crewCount: 2,
            speedAtSeaLevelMetersPerSecond: 10d,
            rangeKilometers: 520d,
            payloadKilograms: 260d,
            totalWeightKilograms: 900d,
            constructionStandard: AircraftConstructionStandard.Ultralight,
            speedAtFiveKilometersMetersPerSecond: 15d,
            speedAtTenKilometersMetersPerSecond: 20d);

        var result = evaluator.Evaluate(aircraft);

        Assert.That(result.IsCompliant, Is.False);
        Assert.That(result.UnmetRequirements, Has.Some.Contains("Speed"));
    }

    [Test]
    public void Evaluate_ReturnsDetailedUnmetRequirements_WhenAircraftMissesMinimumsOrWeightCeiling()
    {
        var evaluator = new CategoryComplianceEvaluator();
        var category = AircraftCategory.LightSportAndRecreational;
        var aircraft = CreateAircraft(
            category,
            crewCount: 0,
            speedAtSeaLevelMetersPerSecond: 10d,
            rangeKilometers: 20d,
            payloadKilograms: 25d,
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

    [Test]
    public void Evaluate_UsesCertifiedRangeLimitedByNavigationEquipment()
    {
        var evaluator = new CategoryComplianceEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.HeavyTransportWideBody,
            crewCount: 4,
            speedAtSeaLevelMetersPerSecond: 220d,
            rangeKilometers: 9000d,
            payloadKilograms: 50000d,
            totalWeightKilograms: 90000d,
            maneuverability: 10d,
            navigationEquipment: AircraftNavigationEquipment.BasicNavigationSuite,
            hasPassengerCabin: true);

        var result = evaluator.Evaluate(aircraft);

        Assert.That(result.IsCompliant, Is.False);
        Assert.That(result.UnmetRequirements, Has.Some.Contains("Certified range"));
    }

    [Test]
    public void Evaluate_AllowsLongRangeCategoryWithAdvancedNavigation()
    {
        var evaluator = new CategoryComplianceEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.HeavyTransportWideBody,
            crewCount: 4,
            speedAtSeaLevelMetersPerSecond: 220d,
            rangeKilometers: 9000d,
            payloadKilograms: 50000d,
            totalWeightKilograms: 90000d,
            maneuverability: 10d,
            navigationEquipment: AircraftNavigationEquipment.AdvancedNavigationSuite,
            hasPassengerCabin: true);

        var result = evaluator.Evaluate(aircraft);

        Assert.That(result.IsCompliant, Is.True);
    }

    [Test]
    public void Evaluate_RequiresPassengerCabinForPassengerCategories()
    {
        var evaluator = new CategoryComplianceEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.RegionalJet,
            crewCount: 2,
            speedAtSeaLevelMetersPerSecond: 195d,
            rangeKilometers: 3000d,
            payloadKilograms: 10000d,
            totalWeightKilograms: 20000d,
            maneuverability: 40d,
            navigationEquipment: AircraftNavigationEquipment.BasicNavigationSuite);

        var result = evaluator.Evaluate(aircraft);

        Assert.That(result.IsCompliant, Is.False);
        Assert.That(result.UnmetRequirements, Has.Some.EqualTo("Passenger cabin required"));
    }

    [Test]
    [Category("Extended")]
    public void Evaluate_RejectsUltralightConstructionForCivilPassengerCategory()
    {
        var evaluator = new CategoryComplianceEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.RegionalJet,
            crewCount: 2,
            speedAtSeaLevelMetersPerSecond: 195d,
            rangeKilometers: 3000d,
            payloadKilograms: 10000d,
            totalWeightKilograms: 20000d,
            maneuverability: 40d,
            constructionStandard: AircraftConstructionStandard.Ultralight,
            navigationEquipment: AircraftNavigationEquipment.BasicNavigationSuite,
            hasPassengerCabin: true);

        var result = evaluator.Evaluate(aircraft);

        Assert.That(result.IsCompliant, Is.False);
        Assert.That(result.UnmetRequirements, Has.Some.Contains("Civil or army-grade construction"));
    }

    [Test]
    public void QualificationEvaluator_ReturnsEveryCategorySatisfiedByCargoType()
    {
        var evaluator = new AircraftCategoryQualificationEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.LightMultiRolePiston,
            crewCount: 3,
            speedAtSeaLevelMetersPerSecond: 112d,
            rangeKilometers: 2000d,
            payloadKilograms: 10000d,
            totalWeightKilograms: 20000d,
            maneuverability: 30d,
            navigationEquipment: AircraftNavigationEquipment.BasicNavigationSuite,
            hasCargoSpace: true);

        var categories = evaluator.GetQualifiedCategories(aircraft);

        Assert.That(categories, Has.Member(AircraftCategory.MediumCargo));
        Assert.That(categories, Has.No.Member(AircraftCategory.RegionalJet));
    }

    [Test]
    [Category("Extended")]
    public void QualificationEvaluator_IgnoresLegacyFamilyCategory_WhenPassengerTypeMatchesAnotherMarket()
    {
        var evaluator = new AircraftCategoryQualificationEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.LightSportAndRecreational,
            crewCount: 2,
            speedAtSeaLevelMetersPerSecond: 195d,
            rangeKilometers: 3000d,
            payloadKilograms: 5000d,
            totalWeightKilograms: 6000d,
            maneuverability: 40d,
            navigationEquipment: AircraftNavigationEquipment.BasicNavigationSuite,
            hasPassengerCabin: true);

        var categories = evaluator.GetQualifiedCategories(aircraft);

        Assert.That(categories, Has.Member(AircraftCategory.BusinessVipJet));
        Assert.That(categories, Has.No.Member(AircraftCategory.LightSportAndRecreational));
    }

    [Test]
    public void QualificationEvaluator_AllowsOnePassengerTypeToQualifyForMultipleMarkets()
    {
        var evaluator = new AircraftCategoryQualificationEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.LightSportAndRecreational,
            crewCount: 2,
            speedAtSeaLevelMetersPerSecond: 180d,
            rangeKilometers: 3000d,
            payloadKilograms: 9000d,
            totalWeightKilograms: 20000d,
            maneuverability: 40d,
            navigationEquipment: AircraftNavigationEquipment.BasicNavigationSuite,
            hasPassengerCabin: true);

        var categories = evaluator.GetQualifiedCategories(aircraft);

        Assert.That(categories, Has.Member(AircraftCategory.TurbopropRegional));
        Assert.That(categories, Has.Member(AircraftCategory.RegionalJet));
        Assert.That(categories, Has.Member(AircraftCategory.BusinessVipJet));
    }

    [Test]
    public void QualificationEvaluator_UsesHistoricalProfilesAndKeepsMultipleMatches()
    {
        var evaluator = new AircraftCategoryQualificationEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.LightSportAndRecreational,
            crewCount: 4,
            speedAtSeaLevelMetersPerSecond: 566.3d / 3.6d,
            rangeKilometers: 4449.4d,
            payloadKilograms: 5932.6d,
            totalWeightKilograms: 12676.4d,
            maneuverability: 59.2d,
            navigationEquipment: AircraftNavigationEquipment.AdvancedNavigationSuite,
            hasPassengerCabin: true);

        var baseCategories = evaluator.GetQualifiedCategories(aircraft);
        var categoriesIn1946 = evaluator.GetQualifiedCategories(aircraft, 1946);

        Assert.That(baseCategories, Has.No.Member(AircraftCategory.BusinessVipJet));
        Assert.That(categoriesIn1946, Has.Member(AircraftCategory.BusinessVipJet));
        Assert.That(categoriesIn1946, Has.Member(AircraftCategory.TurbopropRegional));
        Assert.That(categoriesIn1946.Count, Is.GreaterThan(1));
    }

    [Test]
    public void QualificationEvaluator_RequiresArmyGradeCombatEquipmentAndWeaponPayloadForBomber()
    {
        var evaluator = new AircraftCategoryQualificationEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.Bomber,
            crewCount: 4,
            speedAtSeaLevelMetersPerSecond: 225d,
            rangeKilometers: 5000d,
            payloadKilograms: 20000d,
            totalWeightKilograms: 80000d,
            maneuverability: 30d,
            constructionStandard: AircraftConstructionStandard.ArmyGrade,
            navigationEquipment: AircraftNavigationEquipment.AdvancedNavigationSuite,
            weaponPayloadKilograms: 10000d,
            hasCombatEquipment: true);

        var categories = evaluator.GetQualifiedCategories(aircraft);

        Assert.That(categories, Has.Member(AircraftCategory.Bomber));
    }

    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(false, false, false)]
    [TestCase(true, true, false)]
    [Category("Extended")]
    public void Evaluate_MilitarySupport_AllowsExactlyCargoOrCombatModule(
        bool hasCargoSpace,
        bool hasCombatEquipment,
        bool expectedCompliance)
    {
        var evaluator = new CategoryComplianceEvaluator();
        var aircraft = CreateAircraft(
            AircraftCategory.MilitaryStrategicSupport,
            crewCount: 3,
            speedAtSeaLevelMetersPerSecond: 500d / 3.6d,
            rangeKilometers: 3000d,
            payloadKilograms: 9000d,
            totalWeightKilograms: 20000d,
            maneuverability: 8d,
            constructionStandard: AircraftConstructionStandard.ArmyGrade,
            navigationEquipment: AircraftNavigationEquipment.AdvancedNavigationSuite,
            hasCargoSpace: hasCargoSpace,
            hasCombatEquipment: hasCombatEquipment);

        var result = evaluator.Evaluate(aircraft);

        Assert.That(result.IsCompliant, Is.EqualTo(expectedCompliance));
    }

    private static AircraftModel CreateAircraft(
        AircraftCategory category,
        int crewCount,
        double speedAtSeaLevelMetersPerSecond,
        double rangeKilometers,
        double payloadKilograms,
        double totalWeightKilograms,
        MaterialType materialType = MaterialType.Aluminum,
        double maneuverability = 70d,
        AircraftConstructionStandard constructionStandard = AircraftConstructionStandard.Civil,
        AircraftNavigationEquipment navigationEquipment = AircraftNavigationEquipment.None,
        bool hasPassengerCabin = false,
        bool hasCargoSpace = false,
        double weaponPayloadKilograms = 0d,
        bool hasSpecialEquipment = false,
        bool hasCombatEquipment = false,
        double? speedAtFiveKilometersMetersPerSecond = null,
        double? speedAtTenKilometersMetersPerSecond = null)
    {
        var family = new AircraftFamilyDefinition
        {
            Id = "fam.category.check",
            DisplayName = "Category Check Family",
            EngineDefinitionId = "eng.test",
            AirframeDefinitionId = "air.test",
            Category = category,
            LockedEngineType = EngineType.Radial,
            ConstructionStandard = constructionStandard,
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
            NavigationEquipment = navigationEquipment,
            HasPassengerCabin = hasPassengerCabin,
            HasCargoSpace = hasCargoSpace,
            WeaponPayloadKilograms = weaponPayloadKilograms,
            HasSpecialEquipment = hasSpecialEquipment,
            HasCombatEquipment = hasCombatEquipment,
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
                Maneuverability: maneuverability,
                ProductionCost: 60000d,
                ProductionManDays: 140d,
                ReliabilityModifier: 1.0d,
                ReferenceLiftAtSeaLevel100Ms: Force.FromNewtons(90000d),
                ReferenceDragAtSeaLevel100Ms: Force.FromNewtons(4200d)));

        var performance = new AircraftPerformanceReport(
            new[]
            {
                new AircraftPerformancePoint(0d, speedAtSeaLevelMetersPerSecond),
                new AircraftPerformancePoint(5000d, speedAtFiveKilometersMetersPerSecond ?? speedAtSeaLevelMetersPerSecond),
                new AircraftPerformancePoint(10000d, speedAtTenKilometersMetersPerSecond ?? speedAtSeaLevelMetersPerSecond),
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
