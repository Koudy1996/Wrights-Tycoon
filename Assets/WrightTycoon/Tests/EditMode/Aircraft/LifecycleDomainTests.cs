using System;
using WrightTycoon.Core.Balance;
using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Data;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Physics;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class LifecycleDomainTests
{
    [TestCase(0d)]
    [TestCase(.4d)]
    [TestCase(.8d)]
    [TestCase(.99d)]
    [TestCase(1d)]
    [Category("Extended")]
    public void Lifetime_PreservesReliabilityCurveThroughOneHundredPercent(double reliability)
    {
        var expected = 30000d * System.Math.Max(.5d, System.Math.Pow(reliability / .8d, 1.5d));
        Assert.That(LifecycleCalculator.CalculateExpectedLifeHours(AircraftCategory.LightCargo,
            reliability, 1000, 0), Is.EqualTo(expected).Within(1e-8));
    }

    [Test]
    public void Lifetime_ReliabilityTailIsValueAndSlopeContinuousAtOneHundredPercent()
    {
        const double step = 1e-6d;
        var atOne = LifecycleCalculator.CalculateExpectedLifeHours(AircraftCategory.LightCargo, 1d, 1000, 0);
        var below = LifecycleCalculator.CalculateExpectedLifeHours(AircraftCategory.LightCargo, 1d - step, 1000, 0);
        var above = LifecycleCalculator.CalculateExpectedLifeHours(AircraftCategory.LightCargo, 1d + step, 1000, 0);
        var expectedSlope = atOne * 1.5d;
        Assert.That(atOne, Is.EqualTo(30000d * System.Math.Pow(1d / .8d, 1.5d)).Within(1e-8));
        Assert.That((above - atOne) / step, Is.EqualTo(expectedSlope).Within(.01d));
        Assert.That((atOne - below) / step, Is.EqualTo(expectedSlope).Within(.1d));
    }

    [TestCase(1.01d)]
    [TestCase(1.1d)]
    [TestCase(1.2d)]
    [TestCase(1.6d)]
    [TestCase(2d)]
    [Category("Extended")]
    public void Lifetime_ReliabilitySurplusGrowsLinearlyWithoutCeiling(double reliability)
    {
        const AircraftCategory category = AircraftCategory.LightCargo;
        var atOne = LifecycleCalculator.CalculateExpectedLifeHours(category, 1d, 1000, 0);
        var life = LifecycleCalculator.CalculateExpectedLifeHours(category, reliability, 1000, 0);
        var next = LifecycleCalculator.CalculateExpectedLifeHours(category, reliability + .1d, 1000, 0);
        Assert.That(life, Is.EqualTo(atOne * (1d + 1.5d * (reliability - 1d))).Within(1e-7));
        Assert.That(next - life, Is.EqualTo(atOne * .15d).Within(1e-7));
        Assert.That(LifecycleCalculator.CalculateExpectedLifeHours(category, reliability, 500, 5),
            Is.EqualTo(life * System.Math.Pow(2d, .2d) * 1.4d).Within(1e-7));
    }

    [Test]
    public void Lifetime_IsMonotonicInDurabilityReliabilityAndServiceFrequency()
    {
        const AircraftCategory category = AircraftCategory.LightMultiRolePiston;
        var baseline = LifecycleCalculator.CalculateExpectedLifeHours(category, 0.8d, 1000, 0);
        Assert.That(baseline, Is.EqualTo(30000d).Within(1e-8));
        var previous = baseline;
        for (var level = 1; level <= 10; level++)
        {
            var life = LifecycleCalculator.CalculateExpectedLifeHours(category, 0.8d, 1000, level);
            Assert.That(life, Is.GreaterThan(previous));
            previous = life;
        }
        Assert.That(previous / baseline, Is.EqualTo(1.8d).Within(1e-8));
        Assert.That(LifecycleCalculator.CalculateExpectedLifeHours(category, 0.9d, 1000, 0), Is.GreaterThan(baseline));
        Assert.That(LifecycleCalculator.CalculateExpectedLifeHours(category, 0.8d, 500, 0), Is.InRange(baseline, baseline * 1.2d));
        Assert.That(LifecycleCalculator.CalculateExpectedLifeHours(category, 0.8d, 8000, 0), Is.InRange(baseline * 0.65d, baseline));
        Assert.That(LifecycleCalculator.CalculateExpectedLifeHours(AircraftCategory.HeavyTransportWideBody, 0.9d, 1000, 0), Is.GreaterThan(baseline));
    }

    [Test]
    public void Lifetime_UsesUniversalBaselineRegardlessOfMarketClassification()
    {
        foreach (AircraftCategory category in Enum.GetValues(typeof(AircraftCategory)))
        {
            Assert.That(LifecycleCalculator.CalculateExpectedLifeHours(category, .8d, 1000, 0),
                Is.EqualTo(30000d).Within(1e-8), category.ToString());
            Assert.That(LifecycleCalculator.CalculateExpectedLifeHours(category, .8d, 500, 5),
                Is.EqualTo(48245.33091d).Within(.001d), category.ToString());
        }
    }

    [TestCase(AircraftCategory.LightSportAndRecreational, 7200d, .75d)]
    [TestCase(AircraftCategory.LightMultiRolePiston, 10800d, .8d)]
    [TestCase(AircraftCategory.SpecialCivil, 15000d, .8d)]
    [TestCase(AircraftCategory.TurbopropRegional, 24000d, .85d)]
    [TestCase(AircraftCategory.HeavyTransportWideBody, 48000d, .9d)]
    [Category("Extended")]
    public void UniversalDesignLife_DoesNotChangeContractMinimumsOrAccidentReferences(
        AircraftCategory category, double requiredLife, double accidentReferenceReliability)
    {
        Assert.That(LifecycleContractPolicy.RequiredLife(category), Is.EqualTo(requiredLife));
        Assert.That(LifecycleBalance.ReferenceReliability(category), Is.EqualTo(accidentReferenceReliability));
    }

    [TestCase(0d, 2500d, 500, 5)]
    [TestCase(999d, 1d, 1000, 1)]
    [TestCase(1000d, 0d, 1000, 0)]
    [TestCase(1000d, 1000d, 1000, 1)]
    [TestCase(499d, 2501d, 500, 6)]
    [Category("Extended")]
    public void ServiceThresholds_CountAllCrossings(double start, double flown, int interval, int expected)
    {
        Assert.That(LifecycleCalculator.CountServiceEvents(start, flown, interval), Is.EqualTo(expected));
    }

    [Test]
    [Category("Extended")]
    public void PublicInputs_RejectNonFiniteValuesAndUnsupportedIntervals()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LifecycleCalculator.CalculateExpectedLifeHours(AircraftCategory.LightCargo, double.NaN, 1000, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LifecycleCalculator.ValidateServiceInterval(1500));
        Assert.Throws<ArgumentOutOfRangeException>(() => LifecycleCalculator.ValidateDurability(11));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OperatingProfile("test", double.PositiveInfinity, 1d).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceOfferSnapshot(1000, 120, -1d, 0d, 0d).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => LifecycleCalculator.CountServiceEvents(double.MaxValue, double.MaxValue, 500));
    }

    [Test]
    public void FactoryDurability_ChangesStructureOnlyAndUsesFinalResearchReliability()
    {
        var factory = CreateFactory();
        var family = CreateFamily();
        var baseline = factory.CreateAircraft(family, CreateType(0));
        var durable = factory.CreateAircraft(family, CreateType(10));
        Assert.That(durable.Performance.EmptyAircraftWeightKilograms, Is.GreaterThan(baseline.Performance.EmptyAircraftWeightKilograms));
        Assert.That(durable.Performance.AirframeProductionCost / baseline.Performance.AirframeProductionCost, Is.EqualTo(1.35d).Within(1e-8));
        Assert.That(durable.Performance.AirframeProductionManDays / baseline.Performance.AirframeProductionManDays, Is.EqualTo(1.35d).Within(1e-8));
        Assert.That(durable.Performance.EnginesProductionCost, Is.EqualTo(baseline.Performance.EnginesProductionCost));
        Assert.That(durable.Performance.EnginesProductionManDays, Is.EqualTo(baseline.Performance.EnginesProductionManDays));
        Assert.That(durable.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond, Is.LessThan(baseline.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond));
        Assert.That(durable.Performance.CostBreakdown!.Total, Is.EqualTo(durable.Performance.TotalProductionCost).Within(1e-7));
        Assert.That(durable.Performance.ProductionManDaysBreakdown!.Total, Is.EqualTo(durable.Performance.TotalProductionManDays).Within(1e-7));
        var researched = factory.CreateAircraft(family, CreateType(10, 10));
        Assert.That(researched.Performance.OverallReliability, Is.GreaterThan(durable.Performance.OverallReliability));
        Assert.That(researched.Performance.ExpectedLifeHours, Is.EqualTo(LifecycleCalculator.CalculateExpectedLifeHours(
            family.Category, researched.Performance.OverallReliability, 1000, 10)).Within(1e-7));
        Assert.That(researched.Performance.ExpectedLifeHours, Is.GreaterThan(durable.Performance.ExpectedLifeHours));
        var frozen = AircraftLifecycleSpecification.FromAircraft(researched);
        var changedProgram = frozen with { ServiceIntervalHours = 500 };
        Assert.That(frozen.ServiceIntervalHours, Is.EqualTo(1000));
        Assert.That(changedProgram.ServiceIntervalHours, Is.EqualTo(500));
    }

    [Test]
    public void ZeroDurability_PreservesOriginalPerformanceOverload()
    {
        var aircraft = CreateFactory().CreateAircraft(CreateFamily(), CreateType(0));
        var calculator = new AircraftPerformanceCalculator(new IsaAtmosphereModel(), new AerodynamicsCalculator());
        var original = calculator.Calculate(aircraft.Engine, aircraft.Airframe, aircraft.FamilyDefinition.Category,
            AircraftNavigationEquipment.None, AircraftCategoryRoleRequirement.NoRoleModule);
        Assert.That(aircraft.Performance.TotalWeight, Is.EqualTo(original.TotalWeight));
        Assert.That(aircraft.Performance.TotalProductionCost, Is.EqualTo(original.TotalProductionCost));
        Assert.That(aircraft.Performance.RangeKilometers, Is.EqualTo(original.RangeKilometers));
    }

    [Test]
    public void EngineReliabilitySurplusIsNotMultipliedByTheNumberOfEngines()
    {
        var calculate = typeof(AircraftPerformanceCalculator).GetMethod("CalculateOverallReliability",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var single = (double)calculate.Invoke(null, new object[] { 1.2d, 1d, 1, 0d })!;
        var multi = (double)calculate.Invoke(null, new object[] { 1.2d, 1d, 4, 0d })!;
        Assert.That(single, Is.EqualTo(1.2d).Within(1e-9));
        Assert.That(multi, Is.EqualTo(single));
    }

    [TestCase(1.05d)]
    [TestCase(1.8d)]
    public void ReliabilityBonusesCanOverflowThroughDesignServiceAndGovernmentMetrics(double airframeReliability)
    {
        var factory = CreateFactory(airframeReliability);
        var aircraft = factory.CreateAircraft(CreateFamily(), CreateType(0, 10));
        Assert.That(aircraft.Performance.OverallReliability, Is.GreaterThan(1d));
        var specification = AircraftLifecycleSpecification.FromAircraft(aircraft, 20);
        Assert.That(specification.Reliability, Is.EqualTo(aircraft.Performance.OverallReliability + .2d).Within(1e-9));
        Assert.DoesNotThrow(specification.Validate);
        var offer = LifecycleCostCalculator.CreateEngineLifecycleServiceOffer(aircraft, specification.Reliability);
        Assert.DoesNotThrow(offer.Validate);
        Assert.That(offer.SparePartsPerService, Is.EqualTo(1));
        Assert.That(offer.DowntimeCostPerHour, Is.Zero);
        var metrics = new WrightTycoon.Core.GovernmentContracts.GovernmentContractAircraftMetrics(
            AircraftCategory.LightMultiRolePiston, 100d, 100d, 100d, 1000d, 500d,
            specification.Reliability, 30d, 2, 500d, 100000d);
        Assert.DoesNotThrow(metrics.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(() => LifecycleCalculator.ValidateReliability(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => LifecycleCalculator.ValidateReliability(-.1d));
    }

    [TestCase(0d)]
    [TestCase(.99d)]
    [TestCase(1d)]
    [TestCase(1.01d)]
    [TestCase(1.1d)]
    [TestCase(1.2d)]
    [TestCase(1.6d)]
    [Category("Extended")]
    public void EngineLifetime_KeepsLinearReliabilityRoundingAndMinimum(double reliability)
    {
        var baseline = CreateFactory().CreateAircraft(CreateFamily(), CreateType(0)).Engine;
        var report = baseline.Performance;
        var engine = new Engine(baseline.Definition, baseline.Parameters, new EnginePerformanceReport(
            report.OutputKind, report.AltitudePerformance, reliability, report.Weight, report.Cost,
            report.ProductionManDays, report.EngineLengthMeters, report.EngineFrontalAreaSquareMeters,
            report.CostBreakdown, report.ProductionManDaysBreakdown));
        var serviceFactor = System.Math.Max(.65d, System.Math.Min(1.2d,
            System.Math.Pow(500d / engine.Parameters.ServiceIntervalHours, .2d)));
        var rawLife = 4000d * (.5d + .5d * reliability) * serviceFactor
            * (1d + .08d * engine.Parameters.DurabilityLevel)
            / EngineMaintenancePolicy.CalculatePerformanceStress(engine);
        var expected = System.Math.Max(2000, (int)(System.Math.Round(rawLife / 100d, MidpointRounding.AwayFromZero) * 100d));
        var terms = EngineMaintenancePolicy.Create(engine, "engine.lifecycle", "parts.lifecycle");
        Assert.That(terms.ReplacementLifeHours, Is.EqualTo(expected));
        Assert.That(terms.ReplacementLifeHours % 100, Is.Zero);
        if (reliability == 0d) Assert.That(terms.ReplacementLifeHours, Is.EqualTo(2000));
        if (reliability >= 1d) Assert.That(terms.PartsPerService, Is.EqualTo(1));
        Assert.DoesNotThrow(terms.Validate);
    }

    [Test]
    public void DeliveredSpecificationUsesSameCompanyQualityAsContractEligibility()
    {
        var aircraft = CreateFactory().CreateAircraft(CreateFamily(), CreateType(0));
        var specification = AircraftLifecycleSpecification.FromAircraft(aircraft, 10);
        Assert.That(specification.Reliability, Is.GreaterThan(aircraft.Performance.OverallReliability));
        Assert.That(specification.ExpectedLifeHours, Is.EqualTo(LifecycleCalculator.CalculateExpectedLifeHours(
            aircraft.FamilyDefinition.Category,
            WrightTycoon.Core.Headquarters.HeadquartersBalance.ApplyCompanyReliabilityBonus(aircraft.Performance.OverallReliability, 10),
            aircraft.TypeDefinition.ServiceIntervalHours, aircraft.TypeDefinition.ConstructionDurabilityLevel)));
    }

    [Test]
    public void MaintenanceSellingPricesDoNotFallWithFamilyTypeOrEngineProductionResearch()
    {
        var factory = CreateFactory();
        var family = CreateFamily();
        var baseline = factory.CreateAircraft(family, CreateType(0));
        var researchedFamily = family.WithResearchModifiers(new TypeResearchModifierPoints { ProductionOptimizationPoints = 6 })
            .WithInstalledEngineResearchModifiers(new TypeResearchModifierPoints { ProductionOptimizationPoints = 7 });
        var researched = factory.CreateAircraft(researchedFamily, new AircraftTypeDefinition
        {
            Id = "type.lifecycle", FamilyId = "family.lifecycle", DisplayName = "Researched",
            ResearchModifiers = new TypeResearchModifierPoints { ProductionOptimizationPoints = 10 },
        });
        Assert.That(researched.Performance.CompleteProductionCost, Is.LessThan(baseline.Performance.CompleteProductionCost));
        var before = LifecycleCostCalculator.CreateEngineLifecycleServiceOffer(baseline);
        var after = LifecycleCostCalculator.CreateEngineLifecycleServiceOffer(researched);
        Assert.That(after.SparePartUnitPrice, Is.EqualTo(before.SparePartUnitPrice).Within(1e-7));
        Assert.That(after.EngineMaintenance!.PartUnitPrice, Is.EqualTo(before.EngineMaintenance!.PartUnitPrice).Within(1e-7));
        Assert.That(after.EngineMaintenance.ReplacementUnitPrice, Is.EqualTo(before.EngineMaintenance.ReplacementUnitPrice).Within(1e-7));
    }

    private static AircraftTypeDefinition CreateType(int durability, int reliabilityPoints = 0) => new()
    {
        Id = "type.lifecycle", FamilyId = "family.lifecycle", DisplayName = "Lifecycle",
        ConstructionDurabilityLevel = durability,
        ResearchModifiers = new TypeResearchModifierPoints { ReliabilityPoints = reliabilityPoints },
    };

    private static AircraftFactory CreateFactory(double airframeReliability = .5d)
    {
        var atmosphere = new IsaAtmosphereModel();
        var aero = new AerodynamicsCalculator();
        return new AircraftFactory(new EngineBuilder(new EngineCalculator(), atmosphere),
            new AirframeBuilder(new AirframeCalculator(aero, atmosphere)),
            new AircraftPerformanceCalculator(atmosphere, aero), new DevelopmentCostCalculator(),
            new EngineDefinitionRepository(new[] { new EngineDefinition
            {
                Id = "engine.lifecycle", DisplayName = "Engine", EngineType = EngineType.Radial,
                BaseVolumetricEfficiency = 0.89d, BasePowerPerLiterKilowatts = 34d,
                PropFuelConsumptionPerKilowattHour = 0.25d, BaseReliability = 0.85d,
                BaseWeightKilograms = 285d, WeightPerDisplacementLiter = 12.2d,
                BaseCost = 38000d, CostPerOutputUnit = 112d, BaseProductionManDays = 70d, ComplexityFactor = 1.05d,
            } }), new AirframeDefinitionRepository(new[] { new AirframeDefinition
            {
                Id = "airframe.lifecycle", DisplayName = "Airframe", BaseStructuralWeightKilograms = 1250d,
                ReferenceWingAreaSquareMeters = 36d, BaseDragCoefficient = 0.032d, BaseLiftCoefficient = 1.08d,
                BaseReliabilityModifier = airframeReliability, BaseCost = 68000d, BaseProductionManDays = 150d,
            } }));
    }

    private static AircraftFamilyDefinition CreateFamily() => new()
    {
        Id = "family.lifecycle", DisplayName = "Lifecycle family", EngineDefinitionId = "engine.lifecycle",
        AirframeDefinitionId = "airframe.lifecycle", Category = AircraftCategory.LightMultiRolePiston,
        LockedEngineType = EngineType.Radial, LockedStructureType = StructureType.Monocoque,
        LockedMaterialType = MaterialType.Aluminum, LockedEngineCount = 1,
        BaseDevelopmentCost = 92000d, BaseDevelopmentManDays = 245d,
        BaseEngineParameters = new EngineParameters { DisplacementLiters = 17d, CompressionRatio = 8.4d,
            OptimizationAltitudeMeters = 1800d, CoolingEfficiency = 0.76d },
        BaseAirframeParameters = new AirframeParameters { EngineCount = 1, StructureType = StructureType.Monocoque,
            MaterialType = MaterialType.Aluminum, WingAreaSquareMeters = 36d, FuelAmountLiters = 1500d,
            CrewCount = 2, EngineCowling = 0.55d },
    };
}
