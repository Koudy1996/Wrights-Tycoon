using NUnit.Framework;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Physics;

namespace WrightTycoon.Core.Tests.Campaign;

[TestFixture]
public sealed class HistoricalCampaignTechnologyProgressionTests
{
    [Test]
    public void Progression_UsesNeutralBaselineAt2020()
    {
        var progression = new HistoricalCampaignTechnologyProgression();

        var modifiers = progression.ResolveForYear(2020);

        Assert.That(modifiers.EnginePerformanceMultiplier, Is.EqualTo(1d).Within(1e-9d));
        Assert.That(modifiers.PistonEnginePerformanceMultiplier, Is.EqualTo(1d).Within(1e-9d));
        Assert.That(modifiers.AirframeEfficiencyMultiplier, Is.EqualTo(1d).Within(1e-9d));
        Assert.That(modifiers.AerodynamicsEfficiencyMultiplier, Is.EqualTo(1d).Within(1e-9d));
        Assert.That(modifiers.FuelEfficiencyMultiplier, Is.EqualTo(1d).Within(1e-9d));
        Assert.That(modifiers.PistonFuelEfficiencyMultiplier, Is.EqualTo(1d).Within(1e-9d));
    }

    [Test]
    public void Progression_InterpolatesDeterministicallyForIntermediateYear()
    {
        var progression = new HistoricalCampaignTechnologyProgression();

        var first = progression.ResolveForYear(1960);
        var second = progression.ResolveForYear(1960);

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first.EnginePerformanceMultiplier, Is.EqualTo(0.72d).Within(1e-9d));
        Assert.That(first.PistonEnginePerformanceMultiplier, Is.EqualTo(0.895d).Within(1e-9d));
        Assert.That(first.AirframeEfficiencyMultiplier, Is.EqualTo(0.825d).Within(1e-9d));
        Assert.That(first.AerodynamicsEfficiencyMultiplier, Is.EqualTo(0.825d).Within(1e-9d));
        Assert.That(first.FuelEfficiencyMultiplier, Is.EqualTo(0.63d).Within(1e-9d));
        Assert.That(first.PistonFuelEfficiencyMultiplier, Is.EqualTo(0.845d).Within(1e-9d));
    }

    [Test]
    public void Progression_ClampsYearsOutsideSupportedRange()
    {
        var progression = new HistoricalCampaignTechnologyProgression();

        var beforeRange = progression.ResolveForYear(1900);
        var afterRange = progression.ResolveForYear(2100);

        Assert.That(beforeRange.EnginePerformanceMultiplier, Is.EqualTo(0.60d).Within(1e-9d));
        Assert.That(beforeRange.PistonEnginePerformanceMultiplier, Is.EqualTo(0.85d).Within(1e-9d));
        Assert.That(beforeRange.AirframeEfficiencyMultiplier, Is.EqualTo(0.78d).Within(1e-9d));
        Assert.That(beforeRange.AerodynamicsEfficiencyMultiplier, Is.EqualTo(0.78d).Within(1e-9d));
        Assert.That(beforeRange.FuelEfficiencyMultiplier, Is.EqualTo(0.50d).Within(1e-9d));
        Assert.That(beforeRange.PistonFuelEfficiencyMultiplier, Is.EqualTo(0.80d).Within(1e-9d));

        Assert.That(afterRange.EnginePerformanceMultiplier, Is.EqualTo(1d).Within(1e-9d));
        Assert.That(afterRange.PistonEnginePerformanceMultiplier, Is.EqualTo(1d).Within(1e-9d));
        Assert.That(afterRange.AirframeEfficiencyMultiplier, Is.EqualTo(1d).Within(1e-9d));
        Assert.That(afterRange.AerodynamicsEfficiencyMultiplier, Is.EqualTo(1d).Within(1e-9d));
        Assert.That(afterRange.FuelEfficiencyMultiplier, Is.EqualTo(1d).Within(1e-9d));
        Assert.That(afterRange.PistonFuelEfficiencyMultiplier, Is.EqualTo(1d).Within(1e-9d));
    }

    [Test]
    public void EngineCalculator_UsesPistonSpecificTechnologyMultipliers_WithoutAffectingJets()
    {
        var atmosphere = new IsaAtmosphereModel();
        var calculator = new EngineCalculator();

        var pistonDefinition = new EngineDefinition
        {
            Id = "eng.piston.tech.split",
            DisplayName = "Piston split tech test",
            EngineType = EngineType.Piston,
            BasePowerPerLiterKilowatts = 36d,
            PropFuelConsumptionPerKilowattHour = 0.25d,
            BaseReliability = 0.82d,
            BaseWeightKilograms = 220d,
            WeightPerDisplacementLiter = 10d,
            BaseCost = 24000d,
            CostPerOutputUnit = 90d,
            BaseProductionManDays = 52d,
            ComplexityFactor = 1d,
        };
        var pistonParameters = new EngineParameters
        {
            DisplacementLiters = 14d,
            CylinderCount = 12,
            CompressionRatio = 8.4d,
            MaximumSpeed = 2600d,
            OptimizationAltitudeMeters = 1600d,
            CoolingEfficiency = 0.75d,
            BoostType = BoostType.None,
        };

        var jetDefinition = new EngineDefinition
        {
            Id = "eng.jet.tech.split",
            DisplayName = "Jet split tech test",
            EngineType = EngineType.Turbojet,
            BaseJetThrustPerSquareMeterNewtons = 95000d,
            JetFuelConsumptionPerKiloNewtonHour = 70d,
            BaseReliability = 0.82d,
            BaseWeightKilograms = 420d,
            WeightPerTurbineSquareMeter = 365d,
            BaseCost = 120000d,
            CostPerOutputUnit = 145d,
            BaseProductionManDays = 90d,
            ComplexityFactor = 1.2d,
        };
        var jetParameters = new EngineParameters
        {
            TurbineDiameterMeters = 1.7d,
            BypassRatio = 0.1d,
            CompressionRatio = 20d,
            MaximumSpeed = 42000d,
            CoolingEfficiency = 0.82d,
        };

        var neutral = new HistoricalCampaignTechnologyModifiers(1d, 1d, 1d, 1d, 1d, 1d);
        var split = new HistoricalCampaignTechnologyModifiers(1d, 1d, 1d, 1d, 0.8d, 0.6d);

        var baselinePiston = calculator.Calculate(pistonDefinition, pistonParameters, atmosphere, neutral).GetPerformanceAtAltitude(0d);
        var splitPiston = calculator.Calculate(pistonDefinition, pistonParameters, atmosphere, split).GetPerformanceAtAltitude(0d);
        var baselineJet = calculator.Calculate(jetDefinition, jetParameters, atmosphere, neutral).GetPerformanceAtAltitude(0d);
        var splitJet = calculator.Calculate(jetDefinition, jetParameters, atmosphere, split).GetPerformanceAtAltitude(0d);

        Assert.That(splitPiston.Output, Is.LessThan(baselinePiston.Output));
        Assert.That(splitPiston.FuelConsumptionKilogramsPerHour, Is.GreaterThan(baselinePiston.FuelConsumptionKilogramsPerHour));
        Assert.That(splitJet.Output, Is.EqualTo(baselineJet.Output).Within(1e-9d));
        Assert.That(splitJet.FuelConsumptionKilogramsPerHour, Is.EqualTo(baselineJet.FuelConsumptionKilogramsPerHour).Within(1e-9d));
    }

    [Test]
    public void EngineAndAirframeCalculators_ApplyTechnologyModifiers()
    {
        var atmosphere = new IsaAtmosphereModel();

        var engineDefinition = new EngineDefinition
        {
            Id = "eng.tech.test",
            DisplayName = "Tech Test Engine",
            EngineType = EngineType.Turbofan,
            BaseJetThrustPerSquareMeterNewtons = 98000d,
            JetFuelConsumptionPerKiloNewtonHour = 68d,
            BaseReliability = 0.82d,
            BaseWeightKilograms = 450d,
            WeightPerTurbineSquareMeter = 365d,
            BaseCost = 128000d,
            CostPerOutputUnit = 152d,
            BaseProductionManDays = 98d,
            ComplexityFactor = 1.3d,
        };

        var engineParameters = new EngineParameters
        {
            TurbineDiameterMeters = 2.0d,
            BypassRatio = 10d,
            CompressionRatio = 42d,
            MaximumSpeed = 14000d,
            CoolingEfficiency = 0.82d,
        };

        var engineCalculator = new EngineCalculator();
        var baselineEngine = engineCalculator.Calculate(engineDefinition, engineParameters, atmosphere);
        var outputMalusEngine = engineCalculator.Calculate(
            engineDefinition,
            engineParameters,
            atmosphere,
            new HistoricalCampaignTechnologyModifiers(0.80d, 1d, 1d, 1d));
        var fuelEfficiencyMalusEngine = engineCalculator.Calculate(
            engineDefinition,
            engineParameters,
            atmosphere,
            new HistoricalCampaignTechnologyModifiers(1d, 1d, 1d, 0.80d));

        Assert.That(
            outputMalusEngine.GetPerformanceAtAltitude(0d).Output,
            Is.LessThan(baselineEngine.GetPerformanceAtAltitude(0d).Output));
        Assert.That(
            fuelEfficiencyMalusEngine.GetPerformanceAtAltitude(0d).FuelConsumptionKilogramsPerHour,
            Is.GreaterThan(baselineEngine.GetPerformanceAtAltitude(0d).FuelConsumptionKilogramsPerHour));

        var airframeCalculator = new AirframeCalculator(new AerodynamicsCalculator(), atmosphere);
        var airframeDefinition = new AirframeDefinition
        {
            Id = "air.tech.test",
            DisplayName = "Tech Test Airframe",
            BaseStructuralWeightKilograms = 1150d,
            ReferenceWingAreaSquareMeters = 34d,
            BaseDragCoefficient = 0.031d,
            BaseLiftCoefficient = 1.05d,
            BaseReliabilityModifier = 1.0d,
            BaseCost = 62000d,
            BaseProductionManDays = 135d,
        };

        var airframeParameters = new AirframeParameters
        {
            EngineCount = 2,
            StructureType = StructureType.Monocoque,
            MaterialType = MaterialType.Aluminum,
            WingPosition = WingPosition.Low,
            WingShape = WingShape.Straight,
            WingAreaSquareMeters = 34d,
            TailType = TailType.Classic,
            FlapType = FlapType.Fowler,
            LandingGearType = LandingGearType.SemiRetractable,
            EngineCowling = 0.5d,
            FuelAmountLiters = 1400d,
            CrewCount = 2,
        };

        var baselineAirframe = airframeCalculator.Calculate(airframeDefinition, airframeParameters);
        var historicalAirframe = airframeCalculator.Calculate(
            airframeDefinition,
            airframeParameters,
            new HistoricalCampaignTechnologyModifiers(1d, 0.80d, 0.80d, 1d));

        Assert.That(historicalAirframe.TotalWeight.Kilograms, Is.GreaterThan(baselineAirframe.TotalWeight.Kilograms));
        Assert.That(historicalAirframe.DragCoefficient, Is.GreaterThan(baselineAirframe.DragCoefficient));
        Assert.That(historicalAirframe.LiftCoefficient, Is.LessThan(baselineAirframe.LiftCoefficient));
    }
}
