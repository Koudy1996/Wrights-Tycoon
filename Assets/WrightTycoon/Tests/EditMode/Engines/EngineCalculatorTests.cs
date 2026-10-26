using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Research;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Tests.Engines;

[TestFixture]
public sealed class EngineCalculatorTests
{
    [TestCase(1946, false)]
    [TestCase(1949, false)]
    [TestCase(1950, true)]
    [TestCase(1960, true)]
    [Category("Extended")]
    public void AfterburnerUnlocksIn1950(int year, bool expected)
    {
        Assert.That(EngineDesignerBalance.Engine.IsAfterburnerAvailable(year), Is.EqualTo(expected));
    }

    [Test]
    [Category("Extended")]
    public void PrecisionEngineering_UsesDiminishingBenefitsAndEscalatingPenalties()
    {
        var levelOne = PrecisionEngineeringBalance.GetEffects(1);
        var levelTen = PrecisionEngineeringBalance.GetEffects(10);

        Assert.That(levelOne.OutputMultiplier, Is.EqualTo(1.02d).Within(0.000001d));
        Assert.That(levelOne.ProductionCostMultiplier, Is.EqualTo(1.02d).Within(0.000001d));
        Assert.That(levelOne.ProductionManDaysMultiplier, Is.EqualTo(1.03d).Within(0.000001d));
        Assert.That(levelOne.DevelopmentCostMultiplier, Is.EqualTo(1.04d).Within(0.000001d));
        Assert.That(levelTen.OutputMultiplier, Is.EqualTo(1.15d).Within(0.000001d));
        Assert.That(levelTen.WeightMultiplier, Is.EqualTo(0.85d).Within(0.000001d));
        Assert.That(levelTen.ProductionCostMultiplier, Is.EqualTo(1.30d).Within(0.000001d));
        Assert.That(levelTen.ProductionManDaysMultiplier, Is.EqualTo(1.45d).Within(0.000001d));
        Assert.That(levelTen.ReliabilityMultiplier, Is.EqualTo(0.85d).Within(0.000001d));
        Assert.That(levelTen.DevelopmentTimeMultiplier, Is.EqualTo(1.60d).Within(0.000001d));
    }

    [Test]
    public void PrecisionEngineering_ImprovesEnginePerformanceButRaisesProductionAndDevelopmentBurden()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreatePistonDefinition();
        var baseline = new EngineParameters
        {
            DisplacementLiters = 18d,
            CompressionRatio = 8.2d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 1500d,
            CoolingEfficiency = 0.78d,
            PrecisionEngineeringLevel = 0,
        };
        var precise = baseline.Clone();
        precise.PrecisionEngineeringLevel = 10;

        var baselineReport = calculator.Calculate(definition, baseline, atmosphere);
        var preciseReport = calculator.Calculate(definition, precise, atmosphere);
        var baselinePoint = baselineReport.GetPerformanceAtAltitude(0d);
        var precisePoint = preciseReport.GetPerformanceAtAltitude(0d);

        Assert.That(precisePoint.Output / baselinePoint.Output, Is.EqualTo(1.15d).Within(0.000001d));
        Assert.That(precisePoint.FuelConsumptionKilogramsPerHour, Is.EqualTo(baselinePoint.FuelConsumptionKilogramsPerHour).Within(0.000001d));
        Assert.That(preciseReport.Weight.Kilograms, Is.LessThan(baselineReport.Weight.Kilograms));
        Assert.That(preciseReport.Cost, Is.GreaterThan(baselineReport.Cost));
        Assert.That(preciseReport.ProductionManDays, Is.GreaterThan(baselineReport.ProductionManDays));
        Assert.That(preciseReport.Reliability, Is.LessThan(baselineReport.Reliability));

        var researchCalculator = new ResearchCalculator();
        var baselineQuote = researchCalculator.CalculateEngineQuote(new Engine(definition, baseline, baselineReport));
        var preciseQuote = researchCalculator.CalculateEngineQuote(new Engine(definition, precise, preciseReport));
        Assert.That(preciseQuote.ResearchCost, Is.GreaterThan(baselineQuote.ResearchCost));
        Assert.That(preciseQuote.ResearchTimeMonths, Is.GreaterThan(baselineQuote.ResearchTimeMonths));
    }

    [Test]
    [Category("Extended")]
    public void BoostedPistonEngine_IncreasesPowerAndFuelButReducesReliability()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreatePistonDefinition();

        var baseline = new EngineParameters
        {
            DisplacementLiters = 18d,
            CompressionRatio = 8.2d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 1500d,
            CoolingEfficiency = 0.78d,
        };

        var boosted = baseline.Clone();
        boosted.BoostType = BoostType.Turbo;

        var baseReport = calculator.Calculate(definition, baseline, atmosphere);
        var boostedReport = calculator.Calculate(definition, boosted, atmosphere);

        Assert.That(boostedReport.GetPerformanceAtAltitude(0d).Output, Is.GreaterThan(baseReport.GetPerformanceAtAltitude(0d).Output));
        Assert.That(boostedReport.GetPerformanceAtAltitude(0d).FuelConsumptionKilogramsPerHour, Is.GreaterThan(baseReport.GetPerformanceAtAltitude(0d).FuelConsumptionKilogramsPerHour));
        Assert.That(boostedReport.Reliability, Is.LessThan(baseReport.Reliability));
    }

    [Test]
    [Category("Extended")]
    public void DirectInjection_ImprovesSpecificFuelConsumptionByTenPercent()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreatePistonDefinition();
        var carburetor = new EngineParameters
        {
            DisplacementLiters = 18d,
            CompressionRatio = 8.2d,
            BoostType = BoostType.None,
            PistonInjectionType = PistonInjectionType.Carburetor,
            OptimizationAltitudeMeters = 1500d,
            CoolingEfficiency = 0.78d,
        };
        var directInjection = carburetor.Clone();
        directInjection.PistonInjectionType = PistonInjectionType.DirectInjection;

        var carburetorPoint = calculator.Calculate(definition, carburetor, atmosphere).GetPerformanceAtAltitude(0d);
        var directInjectionPoint = calculator.Calculate(definition, directInjection, atmosphere).GetPerformanceAtAltitude(0d);
        var carburetorSpecificFuel = carburetorPoint.FuelConsumptionKilogramsPerHour / carburetorPoint.Output;
        var directInjectionSpecificFuel = directInjectionPoint.FuelConsumptionKilogramsPerHour / directInjectionPoint.Output;

        Assert.That(directInjectionSpecificFuel / carburetorSpecificFuel, Is.EqualTo(0.9d).Within(0.000001d));
        Assert.That(directInjectionPoint.FuelConsumptionKilogramsPerHour, Is.GreaterThan(carburetorPoint.FuelConsumptionKilogramsPerHour));
    }

    [Test]
    [Category("Extended")]
    public void BoostHardware_AppliesConfiguredSpecificFuelConsumptionEffects()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreatePistonDefinition();
        var naturallyAspirated = new EngineParameters
        {
            DisplacementLiters = 18d,
            CompressionRatio = 8.2d,
            BoostType = BoostType.None,
            PistonInjectionType = PistonInjectionType.DirectInjection,
            OptimizationAltitudeMeters = 1500d,
            CoolingEfficiency = 0.78d,
        };
        var supercharged = naturallyAspirated.Clone();
        supercharged.BoostType = BoostType.Supercharger;
        var turbocharged = naturallyAspirated.Clone();
        turbocharged.BoostType = BoostType.Turbo;

        var naturallyAspiratedPoint = calculator.Calculate(definition, naturallyAspirated, atmosphere).GetPerformanceAtAltitude(0d);
        var superchargedPoint = calculator.Calculate(definition, supercharged, atmosphere).GetPerformanceAtAltitude(0d);
        var turbochargedPoint = calculator.Calculate(definition, turbocharged, atmosphere).GetPerformanceAtAltitude(0d);
        var naturallyAspiratedSpecificFuel = naturallyAspiratedPoint.FuelConsumptionKilogramsPerHour / naturallyAspiratedPoint.Output;
        var superchargedSpecificFuel = superchargedPoint.FuelConsumptionKilogramsPerHour / superchargedPoint.Output;
        var turbochargedSpecificFuel = turbochargedPoint.FuelConsumptionKilogramsPerHour / turbochargedPoint.Output;

        Assert.That(superchargedSpecificFuel / naturallyAspiratedSpecificFuel, Is.EqualTo(1.08d).Within(0.000001d));
        Assert.That(turbochargedSpecificFuel / naturallyAspiratedSpecificFuel, Is.EqualTo(0.98d).Within(0.000001d));
        Assert.That(turbochargedPoint.FuelConsumptionKilogramsPerHour, Is.GreaterThan(naturallyAspiratedPoint.FuelConsumptionKilogramsPerHour));

        var turbochargedReport = calculator.Calculate(definition, turbocharged, atmosphere);
        Assert.That(turbochargedReport.CostBreakdown.Total, Is.EqualTo(turbochargedReport.Cost).Within(0.000001d));
        Assert.That(
            turbochargedReport.ProductionManDaysBreakdown.Total,
            Is.EqualTo(turbochargedReport.ProductionManDays).Within(0.000001d));
    }

    [Test]
    public void TurbojetThrust_ScalesWithTurbineDiameterSquared()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateTurbojetDefinition();

        var small = new EngineParameters
        {
            MaximumSpeed = 48000d,
            CompressionRatio = 12d,
            TurbineDiameterMeters = 1.0d,
            BypassRatio = 0d,
            CoolingEfficiency = 0.8d,
        };

        var large = small.Clone();
        large.TurbineDiameterMeters = 2.0d;

        var smallReport = calculator.Calculate(definition, small, atmosphere);
        var largeReport = calculator.Calculate(definition, large, atmosphere);
        var ratio = largeReport.GetPerformanceAtAltitude(0d).Output / smallReport.GetPerformanceAtAltitude(0d).Output;

        Assert.That(ratio, Is.EqualTo(4d).Within(0.03d));
    }

    [Test]
    public void JetAfterburner_KeepsSpecificFuelConsumptionButRaisesMaximumFuelFlow()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateTurbojetDefinition();

        var withoutAfterburner = new EngineParameters
        {
            MaximumSpeed = 48000d,
            CompressionRatio = 12d,
            TurbineDiameterMeters = 1.0d,
            BypassRatio = 0d,
            CoolingEfficiency = 0.8d,
            HasAfterburner = false,
        };

        var withAfterburner = withoutAfterburner.Clone();
        withAfterburner.HasAfterburner = true;

        var noAfterburnerReport = calculator.Calculate(definition, withoutAfterburner, atmosphere);
        var afterburnerReport = calculator.Calculate(definition, withAfterburner, atmosphere);

        var noAfterburnerAtSeaLevel = noAfterburnerReport.GetPerformanceAtAltitude(0d);
        var afterburnerAtSeaLevel = afterburnerReport.GetPerformanceAtAltitude(0d);

        var outputRatio = afterburnerAtSeaLevel.Output / noAfterburnerAtSeaLevel.Output;
        var fuelFlowRatio = afterburnerAtSeaLevel.FuelConsumptionKilogramsPerHour / noAfterburnerAtSeaLevel.FuelConsumptionKilogramsPerHour;
        var noAfterburnerSpecificFuel = noAfterburnerAtSeaLevel.FuelConsumptionKilogramsPerHour / noAfterburnerAtSeaLevel.Output;
        var afterburnerSpecificFuel = afterburnerAtSeaLevel.FuelConsumptionKilogramsPerHour / afterburnerAtSeaLevel.Output;

        Assert.That(outputRatio, Is.EqualTo(1.5d).Within(0.01d));
        Assert.That(fuelFlowRatio, Is.EqualTo(outputRatio).Within(0.01d));
        Assert.That(afterburnerSpecificFuel, Is.EqualTo(noAfterburnerSpecificFuel).Within(0.0000001d));
        Assert.That(afterburnerReport.Weight.Kilograms, Is.GreaterThan(noAfterburnerReport.Weight.Kilograms));
        Assert.That(afterburnerReport.Cost, Is.GreaterThan(noAfterburnerReport.Cost));
        Assert.That(afterburnerReport.ProductionManDays, Is.GreaterThan(noAfterburnerReport.ProductionManDays));
    }
    [Test]
    [Category("Extended")]
    public void Turbojet_BypassRatioDoesNotChangePerformance()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateTurbojetDefinition();

        var reference = new EngineParameters
        {
            MaximumSpeed = 48000d,
            CompressionRatio = 12d,
            TurbineDiameterMeters = 1.0d,
            BypassRatio = 0d,
            CoolingEfficiency = 0.8d,
            HasAfterburner = false,
        };

        var legacyBypass = reference.Clone();
        legacyBypass.BypassRatio = 22d;

        var referenceReport = calculator.Calculate(definition, reference, atmosphere);
        var legacyReport = calculator.Calculate(definition, legacyBypass, atmosphere);

        var referenceAtSeaLevel = referenceReport.GetPerformanceAtAltitude(0d);
        var legacyAtSeaLevel = legacyReport.GetPerformanceAtAltitude(0d);

        Assert.That(legacyAtSeaLevel.Output, Is.EqualTo(referenceAtSeaLevel.Output).Within(0.0001d));
        Assert.That(legacyAtSeaLevel.FuelConsumptionKilogramsPerHour, Is.EqualTo(referenceAtSeaLevel.FuelConsumptionKilogramsPerHour).Within(0.0001d));
        Assert.That(legacyReport.Reliability, Is.EqualTo(referenceReport.Reliability).Within(0.000001d));
    }

    [TestCase(0.5d, 0.96d)]
    [TestCase(1d, 1d)]
    [TestCase(1.5d, 1.03d)]
    [TestCase(2d, 1.05d)]
    [TestCase(3d, 1.09d)]
    [TestCase(4d, 1.12d)]
    [TestCase(5d, 1.14d)]
    [TestCase(8d, 1.19d)]
    [TestCase(10d, 1.21d)]
    [TestCase(15d, 1.24d)]
    [TestCase(20d, 1.25d)]
    [Category("Extended")]
    public void TurbofanBypassRatio_UsesCalibratedThrustCorrectionTable(double bypassRatio, double expectedOutputRatio)
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateTurbofanDefinition();
        var referenceParameters = CreateReferenceTurbofanParameters();
        var comparedParameters = referenceParameters.Clone();
        comparedParameters.BypassRatio = bypassRatio;

        var referencePoint = calculator.Calculate(definition, referenceParameters, atmosphere).GetPerformanceAtAltitude(0d);
        var comparedPoint = calculator.Calculate(definition, comparedParameters, atmosphere).GetPerformanceAtAltitude(0d);

        Assert.That(comparedPoint.Output / referencePoint.Output, Is.EqualTo(expectedOutputRatio).Within(0.000001d));
    }

    [TestCase(0.5d, 1.05d)]
    [TestCase(1d, 1d)]
    [TestCase(1.5d, 0.98d)]
    [TestCase(2d, 0.96d)]
    [TestCase(3d, 0.93d)]
    [TestCase(4d, 0.91d)]
    [TestCase(5d, 0.89d)]
    [TestCase(8d, 0.85d)]
    [TestCase(10d, 0.83d)]
    [TestCase(15d, 0.81d)]
    [TestCase(20d, 0.80d)]
    [Category("Extended")]
    public void TurbofanBypassRatio_UsesCalibratedSpecificFuelCorrectionTable(double bypassRatio, double expectedSpecificFuelRatio)
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateTurbofanDefinition();
        var referenceParameters = CreateReferenceTurbofanParameters();
        var comparedParameters = referenceParameters.Clone();
        comparedParameters.BypassRatio = bypassRatio;

        var referencePoint = calculator.Calculate(definition, referenceParameters, atmosphere).GetPerformanceAtAltitude(0d);
        var comparedPoint = calculator.Calculate(definition, comparedParameters, atmosphere).GetPerformanceAtAltitude(0d);
        var referenceSpecificFuel = referencePoint.FuelConsumptionKilogramsPerHour / referencePoint.Output;
        var comparedSpecificFuel = comparedPoint.FuelConsumptionKilogramsPerHour / comparedPoint.Output;

        Assert.That(comparedSpecificFuel / referenceSpecificFuel, Is.EqualTo(expectedSpecificFuelRatio).Within(0.000001d));
    }

    [Test]
    [Category("Extended")]
    public void TurbofanBypassRatio_DoesNotApplyAdditionalAltitudeRetentionBonus()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateTurbofanDefinition();
        var referenceParameters = CreateReferenceTurbofanParameters();
        var highBypassParameters = referenceParameters.Clone();
        highBypassParameters.BypassRatio = 20d;

        var referenceReport = calculator.Calculate(definition, referenceParameters, atmosphere);
        var highBypassReport = calculator.Calculate(definition, highBypassParameters, atmosphere);
        var seaLevelRatio = highBypassReport.GetPerformanceAtAltitude(0d).Output
            / referenceReport.GetPerformanceAtAltitude(0d).Output;
        var tenKilometerRatio = highBypassReport.GetPerformanceAtAltitude(10000d).Output
            / referenceReport.GetPerformanceAtAltitude(10000d).Output;

        Assert.That(seaLevelRatio, Is.EqualTo(1.25d).Within(0.000001d));
        Assert.That(tenKilometerRatio, Is.EqualTo(seaLevelRatio).Within(0.000001d));
    }

    [Test]
    [Category("Extended")]
    public void TurbofanBypassRatio_HigherRatioIncreasesWeightAtFixedInputs()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateTurbofanDefinition();
        var bypassRatios = new[] { 0.5d, 1d, 2d, 4d, 8d, 12d, 20d };
        var weights = bypassRatios
            .Select(bypassRatio =>
            {
                var parameters = CreateReferenceTurbofanParameters();
                parameters.BypassRatio = bypassRatio;
                return calculator.Calculate(definition, parameters, atmosphere).Weight.Kilograms;
            })
            .ToArray();

        for (var index = 1; index < weights.Length; index++)
        {
            Assert.That(
                weights[index],
                Is.GreaterThan(weights[index - 1]),
                $"Expected bypass ratio {bypassRatios[index]:0.0} to be heavier than {bypassRatios[index - 1]:0.0}.");
        }
    }

    [Test]
    [Category("Extended")]
    public void InlineEngine_MoreThanEightCylinders_ThrowsArgumentOutOfRangeException()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateInlinePistonDefinition();
        var parameters = new EngineParameters
        {
            DisplacementLiters = 10d,
            CylinderCount = 9,
            CompressionRatio = 9d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 1500d,
            MaximumSpeed = 2600d,
            CoolingEfficiency = 0.7d,
        };

        Assert.Throws<System.ArgumentOutOfRangeException>(() => calculator.Calculate(definition, parameters, atmosphere));
    }

    [Test]
    [Category("Extended")]
    public void BoostHardware_IncreasesEngineFrontalArea()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateInlinePistonDefinition();

        var naturallyAspirated = new EngineParameters
        {
            DisplacementLiters = 9.8d,
            CylinderCount = 8,
            CompressionRatio = 8.7d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 1400d,
            MaximumSpeed = 2700d,
            CoolingEfficiency = 0.7d,
        };

        var supercharged = naturallyAspirated.Clone();
        supercharged.BoostType = BoostType.Supercharger;
        var turbocharged = naturallyAspirated.Clone();
        turbocharged.BoostType = BoostType.Turbo;

        var naturallyAspiratedReport = calculator.Calculate(definition, naturallyAspirated, atmosphere);
        var superchargedReport = calculator.Calculate(definition, supercharged, atmosphere);
        var turbochargedReport = calculator.Calculate(definition, turbocharged, atmosphere);

        Assert.That(superchargedReport.EngineFrontalAreaSquareMeters, Is.GreaterThan(naturallyAspiratedReport.EngineFrontalAreaSquareMeters));
        Assert.That(turbochargedReport.EngineFrontalAreaSquareMeters, Is.GreaterThan(superchargedReport.EngineFrontalAreaSquareMeters));
    }

    [Test]
    [Category("Extended")]
    public void RadialEngine_HasLargerFrontalAreaButLowerWeightThanInlineAndV()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();

        var inlineDefinition = CreatePistonDefinitionForArrangement(EngineType.Inline);
        var vDefinition = CreatePistonDefinitionForArrangement(EngineType.V);
        var radialDefinition = CreatePistonDefinitionForArrangement(EngineType.Radial);

        var parameters = new EngineParameters
        {
            DisplacementLiters = 16d,
            CylinderCount = 8,
            CompressionRatio = 8.6d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 1800d,
            MaximumSpeed = 2600d,
            CoolingEfficiency = 0.72d,
        };

        var inlineReport = calculator.Calculate(inlineDefinition, parameters, atmosphere);
        var vReport = calculator.Calculate(vDefinition, parameters, atmosphere);
        var radialReport = calculator.Calculate(radialDefinition, parameters, atmosphere);

        Assert.That(radialReport.EngineFrontalAreaSquareMeters, Is.GreaterThan(vReport.EngineFrontalAreaSquareMeters));
        Assert.That(vReport.EngineFrontalAreaSquareMeters, Is.GreaterThan(inlineReport.EngineFrontalAreaSquareMeters));
        Assert.That(radialReport.Weight.Kilograms, Is.LessThan(vReport.Weight.Kilograms));
        Assert.That(radialReport.Weight.Kilograms, Is.LessThan(inlineReport.Weight.Kilograms));
        Assert.That(radialReport.Cost / inlineReport.Cost, Is.EqualTo(0.90d).Within(0.000001d));
        Assert.That(radialReport.ProductionManDays / inlineReport.ProductionManDays, Is.EqualTo(0.95d).Within(0.000001d));
    }

    [Test]
    [Category("Extended")]
    public void EfficiencyOptimization_ScalesProductionBurdenLinearly()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreatePistonDefinitionForArrangement(EngineType.Inline);
        var baseline = new EngineParameters
        {
            DisplacementLiters = 8d,
            CylinderCount = 6,
            CompressionRatio = 8d,
            MaximumSpeed = 2500d,
            OptimizationAltitudeMeters = 1500d,
            CoolingEfficiency = 0.75d,
            PistonInjectionType = PistonInjectionType.Carburetor,
            EfficiencyOptimizationsPercent = 0d,
        };
        var midpoint = baseline.Clone();
        midpoint.EfficiencyOptimizationsPercent = 15d;
        var maximum = baseline.Clone();
        maximum.EfficiencyOptimizationsPercent = 30d;

        var baselineReport = calculator.Calculate(definition, baseline, atmosphere);
        var midpointReport = calculator.Calculate(definition, midpoint, atmosphere);
        var maximumReport = calculator.Calculate(definition, maximum, atmosphere);

        Assert.That(midpointReport.Cost / baselineReport.Cost, Is.EqualTo(1.06d).Within(0.000001d));
        Assert.That(maximumReport.Cost / baselineReport.Cost, Is.EqualTo(1.12d).Within(0.000001d));
        Assert.That(midpointReport.ProductionManDays / baselineReport.ProductionManDays, Is.EqualTo(1.10d).Within(0.000001d));
        Assert.That(maximumReport.ProductionManDays / baselineReport.ProductionManDays, Is.EqualTo(1.20d).Within(0.000001d));
    }

    [Test]
    [Category("Extended")]
    public void EngineResearch_RangesFromSimpleThirtyMonthToAdvancedEightyMonthPrograms()
    {
        var calculator = new EngineCalculator();
        var research = new ResearchCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var simpleDefinition = CreatePistonDefinitionForArrangement(EngineType.Inline);
        var simpleParameters = new EngineParameters
        {
            DisplacementLiters = 2.4d,
            CylinderCount = 4,
            CompressionRatio = 7d,
            MaximumSpeed = 2500d,
            OptimizationAltitudeMeters = 1500d,
            CoolingEfficiency = 0.75d,
            PistonInjectionType = PistonInjectionType.Carburetor,
        };
        var simpleReport = calculator.Calculate(simpleDefinition, simpleParameters, atmosphere);
        var simpleQuote = research.CalculateEngineQuote(new Engine(simpleDefinition, simpleParameters, simpleReport));

        var advancedDefinition = CreateTurbofanDefinition();
        var advancedParameters = new EngineParameters
        {
            TurbineDiameterMeters = 3d,
            BypassRatio = 12d,
            CompressionRatio = 50d,
            MaximumSpeed = 100000d,
            OptimizationAltitudeMeters = 12000d,
            CoolingEfficiency = 1d,
            EfficiencyOptimizationsPercent = 30d,
            PrecisionEngineeringLevel = 10,
            HasAfterburner = true,
        };
        var advancedReport = calculator.Calculate(advancedDefinition, advancedParameters, atmosphere);
        var advancedQuote = research.CalculateEngineQuote(new Engine(advancedDefinition, advancedParameters, advancedReport));

        Assert.That(simpleQuote.ResearchTimeMonths, Is.InRange(30, 35));
        Assert.That(advancedQuote.ResearchTimeMonths, Is.EqualTo(80));
        Assert.That(simpleQuote.ResearchCost / simpleReport.CompleteProductionCost, Is.InRange(8d, 12d));
        Assert.That(advancedQuote.ResearchCost / advancedReport.CompleteProductionCost, Is.InRange(8d, 12.001d));
    }

    [Test]
    [Category("Extended")]
    public void LowBypassTurbofan_ScalesPoorlyAbove150kNewton()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateTurbofanDefinition();

        var lowBypassSmall = new EngineParameters
        {
            TurbineDiameterMeters = 2.2d,
            BypassRatio = 2d,
            CompressionRatio = 38d,
            MaximumSpeed = 52000d,
            CoolingEfficiency = 0.82d,
        };
        var lowBypassLarge = lowBypassSmall.Clone();
        lowBypassLarge.TurbineDiameterMeters = 2.8d;

        var highBypassSmall = lowBypassSmall.Clone();
        highBypassSmall.BypassRatio = 12d;
        var highBypassLarge = highBypassSmall.Clone();
        highBypassLarge.TurbineDiameterMeters = 2.8d;

        var lowSmallOutput = calculator.Calculate(definition, lowBypassSmall, atmosphere).GetPerformanceAtAltitude(0d).Output;
        var lowLargeOutput = calculator.Calculate(definition, lowBypassLarge, atmosphere).GetPerformanceAtAltitude(0d).Output;
        var highSmallOutput = calculator.Calculate(definition, highBypassSmall, atmosphere).GetPerformanceAtAltitude(0d).Output;
        var highLargeOutput = calculator.Calculate(definition, highBypassLarge, atmosphere).GetPerformanceAtAltitude(0d).Output;

        var lowGrowth = lowLargeOutput / lowSmallOutput;
        var highGrowth = highLargeOutput / highSmallOutput;

        Assert.That(lowSmallOutput, Is.GreaterThan(150000d));
        Assert.That(lowGrowth, Is.LessThan(highGrowth));
    }

    [Test]
    public void EngineOutput_DropsFromSeaLevelToTenKilometers()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreatePistonDefinition();
        var parameters = new EngineParameters
        {
            DisplacementLiters = 16d,
            CylinderCount = 14,
            MaximumSpeed = 2600d,
            CompressionRatio = 8.6d,
            BoostType = BoostType.Supercharger,
            OptimizationAltitudeMeters = 3000d,
            CoolingEfficiency = 0.74d,
        };

        var report = calculator.Calculate(definition, parameters, atmosphere);
        var seaLevel = report.GetPerformanceAtAltitude(0d).Output;
        var tenKm = report.GetPerformanceAtAltitude(10000d).Output;

        Assert.That(tenKm, Is.LessThan(seaLevel));
    }

    [Test]
    public void PistonCylinderCountAndSpeed_IncreasePowerOutput()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreatePistonDefinition();

        var conservative = new EngineParameters
        {
            DisplacementLiters = 12d,
            CylinderCount = 6,
            MaximumSpeed = 1500d,
            CompressionRatio = 7.5d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 2000d,
            CoolingEfficiency = 0.8d,
        };

        var aggressive = conservative.Clone();
        aggressive.CylinderCount = 24;
        aggressive.MaximumSpeed = 4200d;

        var conservativeReport = calculator.Calculate(definition, conservative, atmosphere);
        var aggressiveReport = calculator.Calculate(definition, aggressive, atmosphere);

        Assert.That(aggressiveReport.GetPerformanceAtAltitude(0d).Output, Is.GreaterThan(conservativeReport.GetPerformanceAtAltitude(0d).Output));
    }

    [Test]
    [Category("Extended")]
    public void PistonCompression_IncreasesPowerAndFuelFlowButImprovesSpecificFuelConsumption()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateInlinePistonDefinition();

        var lowCompression = new EngineParameters
        {
            DisplacementLiters = 8.5d,
            CylinderCount = 6,
            MaximumSpeed = 2700d,
            CompressionRatio = 3d,
            BoostType = BoostType.None,
            PistonInjectionType = PistonInjectionType.DirectInjection,
            OptimizationAltitudeMeters = 1800d,
            CoolingEfficiency = 0.45d,
        };

        var highCompression = lowCompression.Clone();
        highCompression.CompressionRatio = 12d;

        var lowReport = calculator.Calculate(definition, lowCompression, atmosphere);
        var highReport = calculator.Calculate(definition, highCompression, atmosphere);

        var lowSeaLevel = lowReport.GetPerformanceAtAltitude(0d);
        var highSeaLevel = highReport.GetPerformanceAtAltitude(0d);
        var powerRatio = highSeaLevel.Output / lowSeaLevel.Output;
        var fuelFlowRatio = highSeaLevel.FuelConsumptionKilogramsPerHour / lowSeaLevel.FuelConsumptionKilogramsPerHour;
        var lowSpecificFuel = lowSeaLevel.FuelConsumptionKilogramsPerHour / lowSeaLevel.Output;
        var highSpecificFuel = highSeaLevel.FuelConsumptionKilogramsPerHour / highSeaLevel.Output;

        Assert.That(powerRatio, Is.EqualTo(2d).Within(0.000001d));
        Assert.That(fuelFlowRatio, Is.GreaterThan(1d));
        Assert.That(fuelFlowRatio, Is.EqualTo(1.6d).Within(0.000001d));
        Assert.That(highSpecificFuel, Is.LessThan(lowSpecificFuel));
        Assert.That(highSpecificFuel / lowSpecificFuel, Is.EqualTo(0.8d).Within(0.000001d));
    }

    [Test]
    [Category("Extended")]
    public void PistonCompressionOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreatePistonDefinition();
        var parameters = new EngineParameters
        {
            DisplacementLiters = 13d,
            CylinderCount = 8,
            MaximumSpeed = 2200d,
            CompressionRatio = 18d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 2500d,
            CoolingEfficiency = 0.76d,
        };

        Assert.Throws<System.ArgumentOutOfRangeException>(() => calculator.Calculate(definition, parameters, atmosphere));
    }

    [Test]
    [Category("Extended")]
    public void OptimizationHeight_HasStrongVisibleImpactOnPistonOutput()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new ConstantDensityAtmosphereModel();
        var definition = CreatePistonDefinition();

        var nearOptimized = new EngineParameters
        {
            DisplacementLiters = 16d,
            CylinderCount = 12,
            MaximumSpeed = 2600d,
            CompressionRatio = 8.5d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 1000d,
            CoolingEfficiency = 0.78d,
        };

        var badlyOptimized = nearOptimized.Clone();
        badlyOptimized.OptimizationAltitudeMeters = 10000d;

        var nearReport = calculator.Calculate(definition, nearOptimized, atmosphere);
        var badReport = calculator.Calculate(definition, badlyOptimized, atmosphere);

        Assert.That(
            nearReport.GetPerformanceAtAltitude(0d).Output,
            Is.GreaterThan(badReport.GetPerformanceAtAltitude(0d).Output * 1.2d));
    }

    [Test]
    [Category("Extended")]
    public void TurboAndSupercharger_ReduceOptimizationMismatchPenalty()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new ConstantDensityAtmosphereModel();
        var definition = CreatePistonDefinition();

        var naturallyAspirated = new EngineParameters
        {
            DisplacementLiters = 18d,
            CylinderCount = 14,
            MaximumSpeed = 2700d,
            CompressionRatio = 8.4d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 10000d,
            CoolingEfficiency = 0.76d,
        };

        var supercharged = naturallyAspirated.Clone();
        supercharged.BoostType = BoostType.Supercharger;

        var turbocharged = naturallyAspirated.Clone();
        turbocharged.BoostType = BoostType.Turbo;

        var naReport = calculator.Calculate(definition, naturallyAspirated, atmosphere);
        var scReport = calculator.Calculate(definition, supercharged, atmosphere);
        var turboReport = calculator.Calculate(definition, turbocharged, atmosphere);

        var naOutput = naReport.GetPerformanceAtAltitude(0d).Output;
        var scOutput = scReport.GetPerformanceAtAltitude(0d).Output;
        var turboOutput = turboReport.GetPerformanceAtAltitude(0d).Output;

        Assert.That(scOutput, Is.GreaterThan(naOutput));
        Assert.That(turboOutput, Is.GreaterThan(scOutput));
    }

    [Test]
    [Category("Extended")]
    public void JetWeight_CalibratesNearLargeCivilAndFighterAnchors()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateTurbofanDefinition();

        var largeCivil = new EngineParameters
        {
            TurbineDiameterMeters = 3.2d,
            BypassRatio = 9d,
            CompressionRatio = 42d,
            MaximumSpeed = 2800d,
            CoolingEfficiency = 0.85d,
        };

        var fighter = new EngineParameters
        {
            TurbineDiameterMeters = 0.97d,
            BypassRatio = 0.5d,
            CompressionRatio = 36d,
            MaximumSpeed = 11200d,
            CoolingEfficiency = 0.85d,
        };

        var largeReport = calculator.Calculate(definition, largeCivil, atmosphere);
        var fighterReport = calculator.Calculate(definition, fighter, atmosphere);

        Assert.That(largeReport.Weight.Kilograms, Is.InRange(7000d, 9500d));
        Assert.That(fighterReport.Weight.Kilograms, Is.InRange(1300d, 2200d));
        Assert.That(largeReport.Weight.Kilograms, Is.GreaterThan(fighterReport.Weight.Kilograms));
    }

    [Test]
    [Category("Extended")]
    public void JetWeight_HighBypassMidSizeStaysLighterThanVeryLargeWidebodyFan()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateTurbofanDefinition();

        var widebodyFan = new EngineParameters
        {
            TurbineDiameterMeters = 3.2d,
            BypassRatio = 9d,
            CompressionRatio = 42d,
            MaximumSpeed = 2800d,
            CoolingEfficiency = 0.85d,
        };

        var gearedMidSize = new EngineParameters
        {
            TurbineDiameterMeters = 2.04d,
            BypassRatio = 12.5d,
            CompressionRatio = 42d,
            MaximumSpeed = 3250d,
            CoolingEfficiency = 0.87d,
        };

        var widebodyReport = calculator.Calculate(definition, widebodyFan, atmosphere);
        var gearedReport = calculator.Calculate(definition, gearedMidSize, atmosphere);

        Assert.That(gearedReport.Weight.Kilograms, Is.InRange(1400d, 3800d));
        Assert.That(gearedReport.Weight.Kilograms, Is.LessThan(widebodyReport.Weight.Kilograms * 0.5d));
    }

    [Test]
    [Category("Extended")]
    public void PistonWeight_ModelSeparatesLightGeneralAviationFromLargeRadial()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var smallDefinition = CreateInlinePistonDefinition();
        var largeDefinition = CreatePistonDefinition();

        var lightGeneralAviation = new EngineParameters
        {
            DisplacementLiters = 9.3d,
            CylinderCount = 6,
            CompressionRatio = 8.5d,
            BoostType = BoostType.None,
            MaximumSpeed = 2700d,
            OptimizationAltitudeMeters = 1200d,
            CoolingEfficiency = 0.45d,
        };

        var largeRadial = new EngineParameters
        {
            DisplacementLiters = 46d,
            CylinderCount = 18,
            CompressionRatio = 6.75d,
            BoostType = BoostType.Turbo,
            MaximumSpeed = 2700d,
            OptimizationAltitudeMeters = 7600d,
            CoolingEfficiency = 0.3d,
        };

        var lightReport = calculator.Calculate(smallDefinition, lightGeneralAviation, atmosphere);
        var radialReport = calculator.Calculate(largeDefinition, largeRadial, atmosphere);

        Assert.That(lightReport.Weight.Kilograms, Is.InRange(180d, 420d));
        Assert.That(radialReport.Weight.Kilograms, Is.InRange(1200d, 2300d));
        Assert.That(radialReport.Weight.Kilograms, Is.GreaterThan(lightReport.Weight.Kilograms * 3.5d));
    }

    [Test]
    [Category("Extended")]
    public void PistonRpmAroundSixThousand_IsAllowedByUpdatedScale()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateInlinePistonDefinition();
        var parameters = new EngineParameters
        {
            DisplacementLiters = 4.8d,
            CylinderCount = 4,
            CompressionRatio = 10.2d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 1800d,
            MaximumSpeed = 6000d,
            CoolingEfficiency = 0.35d,
        };

        Assert.DoesNotThrow(() => calculator.Calculate(definition, parameters, atmosphere));
    }

    [Test]
    [Category("Extended")]
    public void JetAltitudeOutputRetention_IsHigherThanLinearDensityDrop()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateTurbofanDefinition();
        var parameters = new EngineParameters
        {
            TurbineDiameterMeters = 2.0d,
            BypassRatio = 9.5d,
            CompressionRatio = 42d,
            MaximumSpeed = 14500d,
            CoolingEfficiency = 0.78d,
        };

        var report = calculator.Calculate(definition, parameters, atmosphere);
        var seaLevel = report.GetPerformanceAtAltitude(0d).Output;
        var tenKm = report.GetPerformanceAtAltitude(10000d).Output;
        var retention = tenKm / seaLevel;
        var linearDensityDrop = atmosphere.GetDensityRatioAtAltitude(10000d);

        Assert.That(retention, Is.GreaterThan(linearDensityDrop));
        Assert.That(retention, Is.LessThan(1d));
    }

    [Test]
    [Category("Extended")]
    public void Carburetor_ReducesPowerButLowersCostAndProductionTime()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreateInlinePistonDefinition();

        var directInjection = new EngineParameters
        {
            DisplacementLiters = 8.5d,
            CylinderCount = 6,
            CompressionRatio = 9.2d,
            BoostType = BoostType.None,
            PistonInjectionType = PistonInjectionType.DirectInjection,
            OptimizationAltitudeMeters = 1800d,
            MaximumSpeed = 2700d,
            CoolingEfficiency = 0.45d,
        };

        var carburetor = directInjection.Clone();
        carburetor.PistonInjectionType = PistonInjectionType.Carburetor;

        var directReport = calculator.Calculate(definition, directInjection, atmosphere);
        var carbReport = calculator.Calculate(definition, carburetor, atmosphere);

        Assert.That(carbReport.GetPerformanceAtAltitude(0d).Output, Is.LessThan(directReport.GetPerformanceAtAltitude(0d).Output));
        Assert.That(carbReport.Cost, Is.LessThan(directReport.Cost));
        Assert.That(carbReport.ProductionManDays, Is.LessThan(directReport.ProductionManDays));
        Assert.That(carbReport.EngineLengthMeters, Is.GreaterThan(0d));
        Assert.That(carbReport.EngineFrontalAreaSquareMeters, Is.GreaterThan(0d));
    }


    [Test]
    [Category("Extended")]
    public void Turbocharger_HasMuchStrongerPowerGainAtHighAltitudeThanAtSeaLevel()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreatePistonDefinition();

        var naturallyAspirated = new EngineParameters
        {
            DisplacementLiters = 16d,
            CylinderCount = 12,
            MaximumSpeed = 2600d,
            CompressionRatio = 8.4d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 5000d,
            CoolingEfficiency = 0.78d,
        };

        var turbocharged = naturallyAspirated.Clone();
        turbocharged.BoostType = BoostType.Turbo;

        var naturallyAspiratedReport = calculator.Calculate(definition, naturallyAspirated, atmosphere);
        var turbochargedReport = calculator.Calculate(definition, turbocharged, atmosphere);

        var seaLevelGain = turbochargedReport.GetPerformanceAtAltitude(0d).Output
            / naturallyAspiratedReport.GetPerformanceAtAltitude(0d).Output;
        var highAltitudeGain = turbochargedReport.GetPerformanceAtAltitude(10000d).Output
            / naturallyAspiratedReport.GetPerformanceAtAltitude(10000d).Output;

        Assert.That(highAltitudeGain, Is.GreaterThan(seaLevelGain * 1.2d));
    }

    [Test]
    [Category("Extended")]
    public void Supercharger_RetainsMoreOutputNearOptimizationAltitudeThanAtHighAltitude()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();
        var definition = CreatePistonDefinition();

        var supercharged = new EngineParameters
        {
            DisplacementLiters = 15d,
            CylinderCount = 10,
            MaximumSpeed = 2500d,
            CompressionRatio = 8.3d,
            BoostType = BoostType.Supercharger,
            OptimizationAltitudeMeters = 5000d,
            CoolingEfficiency = 0.76d,
        };

        var report = calculator.Calculate(definition, supercharged, atmosphere);
        var lowAltitudeOutput = report.GetPerformanceAtAltitude(0d).Output;
        var optimizedAltitudeOutput = report.GetPerformanceAtAltitude(5000d).Output;
        var highAltitudeOutput = report.GetPerformanceAtAltitude(10000d).Output;

        Assert.That(lowAltitudeOutput, Is.GreaterThan(optimizedAltitudeOutput));
        Assert.That(optimizedAltitudeOutput, Is.GreaterThan(highAltitudeOutput));
    }

    [Test]
    [Category("Extended")]
    public void Turbojet_HasHigherSpecificFuelFlowThanComparableTurbofan()
    {
        var calculator = new EngineCalculator();
        var atmosphere = new IsaAtmosphereModel();

        var turbojetDefinition = new EngineDefinition
        {
            Id = "eng.turbojet.compare",
            DisplayName = "Turbojet Compare",
            EngineType = EngineType.Turbojet,
            BaseJetThrustPerSquareMeterNewtons = 95000d,
            JetFuelConsumptionPerKiloNewtonHour = 68d,
            BaseReliability = 0.8d,
            BaseWeightKilograms = 500d,
            WeightPerTurbineSquareMeter = 320d,
            BaseCost = 120000d,
            CostPerOutputUnit = 145d,
            BaseProductionManDays = 90d,
            ComplexityFactor = 1.25d,
        };

        var turbofanDefinition = new EngineDefinition
        {
            Id = "eng.turbofan.compare",
            DisplayName = "Turbofan Compare",
            EngineType = EngineType.Turbofan,
            BaseJetThrustPerSquareMeterNewtons = 95000d,
            JetFuelConsumptionPerKiloNewtonHour = 68d,
            BaseReliability = 0.8d,
            BaseWeightKilograms = 500d,
            WeightPerTurbineSquareMeter = 320d,
            BaseCost = 120000d,
            CostPerOutputUnit = 145d,
            BaseProductionManDays = 90d,
            ComplexityFactor = 1.25d,
        };

        var turbojetParameters = new EngineParameters
        {
            MaximumSpeed = 45000d,
            CompressionRatio = 16d,
            TurbineDiameterMeters = 1.4d,
            BypassRatio = 0d,
            CoolingEfficiency = 0.8d,
        };

        var turbofanParameters = turbojetParameters.Clone();
        turbofanParameters.BypassRatio = 4d;

        var turbojetReport = calculator.Calculate(turbojetDefinition, turbojetParameters, atmosphere);
        var turbofanReport = calculator.Calculate(turbofanDefinition, turbofanParameters, atmosphere);

        var turbojetSeaLevel = turbojetReport.GetPerformanceAtAltitude(0d);
        var turbofanSeaLevel = turbofanReport.GetPerformanceAtAltitude(0d);
        var turbojetSpecificFuel = turbojetSeaLevel.FuelConsumptionKilogramsPerHour / turbojetSeaLevel.Output;
        var turbofanSpecificFuel = turbofanSeaLevel.FuelConsumptionKilogramsPerHour / turbofanSeaLevel.Output;

        Assert.That(turbojetSpecificFuel, Is.GreaterThan(turbofanSpecificFuel));
    }
    private static EngineDefinition CreatePistonDefinition() =>
        new()
        {
            Id = "eng.piston.large",
            DisplayName = "Large Piston Core",
            EngineType = EngineType.Radial,
            BaseVolumetricEfficiency = 0.87d,
            BasePowerPerLiterKilowatts = 33d,
            PropFuelConsumptionPerKilowattHour = 0.26d,
            BaseReliability = 0.9d,
            BaseWeightKilograms = 280d,
            WeightPerDisplacementLiter = 12d,
            BaseCost = 36000d,
            CostPerOutputUnit = 105d,
            BaseProductionManDays = 66d,
            ComplexityFactor = 1.05d,
        };

    private static EngineDefinition CreatePistonDefinitionForArrangement(EngineType engineType)
    {
        var definition = CreateInlinePistonDefinition();
        return new EngineDefinition
        {
            Id = $"eng.arrangement.{engineType.ToString().ToLowerInvariant()}",
            DisplayName = $"{engineType} Test Piston",
            EngineType = engineType,
            BaseVolumetricEfficiency = definition.BaseVolumetricEfficiency,
            BasePowerPerLiterKilowatts = definition.BasePowerPerLiterKilowatts,
            BaseJetThrustPerSquareMeterNewtons = definition.BaseJetThrustPerSquareMeterNewtons,
            PropFuelConsumptionPerKilowattHour = definition.PropFuelConsumptionPerKilowattHour,
            JetFuelConsumptionPerKiloNewtonHour = definition.JetFuelConsumptionPerKiloNewtonHour,
            BaseReliability = definition.BaseReliability,
            BaseWeightKilograms = definition.BaseWeightKilograms,
            WeightPerDisplacementLiter = definition.WeightPerDisplacementLiter,
            WeightPerTurbineSquareMeter = definition.WeightPerTurbineSquareMeter,
            BaseCost = definition.BaseCost,
            CostPerOutputUnit = definition.CostPerOutputUnit,
            BaseProductionManDays = definition.BaseProductionManDays,
            ComplexityFactor = definition.ComplexityFactor,
        };
    }

    private static EngineDefinition CreateTurbojetDefinition() =>
        new()
        {
            Id = "eng.turbojet.early",
            DisplayName = "Early Turbojet Core",
            EngineType = EngineType.Turbojet,
            BaseJetThrustPerSquareMeterNewtons = 94000d,
            JetFuelConsumptionPerKiloNewtonHour = 73d,
            BaseReliability = 0.78d,
            BaseWeightKilograms = 430d,
            WeightPerTurbineSquareMeter = 350d,
            BaseCost = 98000d,
            CostPerOutputUnit = 145d,
            BaseProductionManDays = 88d,
            ComplexityFactor = 1.25d,
        };

    private static EngineDefinition CreateTurbofanDefinition() =>
        new()
        {
            Id = "eng.turbofan.reference",
            DisplayName = "Reference Turbofan",
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

    private static EngineParameters CreateReferenceTurbofanParameters() =>
        new()
        {
            TurbineDiameterMeters = 1d,
            BypassRatio = 1d,
            CompressionRatio = 20d,
            MaximumSpeed = 50000d,
            CoolingEfficiency = 0.8d,
        };

    private static EngineDefinition CreateInlinePistonDefinition() =>
        new()
        {
            Id = "eng.inline.reference",
            DisplayName = "Reference Inline Piston",
            EngineType = EngineType.Inline,
            BaseVolumetricEfficiency = 0.9d,
            BasePowerPerLiterKilowatts = 33.5d,
            PropFuelConsumptionPerKilowattHour = 0.24d,
            BaseReliability = 0.91d,
            BaseWeightKilograms = 255d,
            WeightPerDisplacementLiter = 11.8d,
            BaseCost = 36000d,
            CostPerOutputUnit = 108d,
            BaseProductionManDays = 64d,
            ComplexityFactor = 0.98d,
        };

    private sealed class ConstantDensityAtmosphereModel : IAtmosphereModel
    {
        private static readonly Density SeaLevel = Density.FromKilogramsPerCubicMeter(1.225d);
        private readonly IReadOnlyList<AtmosphereSample> _samples = new[]
        {
            new AtmosphereSample(0d, SeaLevel, 1d),
            new AtmosphereSample(5000d, SeaLevel, 1d),
            new AtmosphereSample(10000d, SeaLevel, 1d),
        };

        public Density SeaLevelDensity => SeaLevel;

        public Density GetDensityAtAltitude(double altitudeMeters) => SeaLevel;

        public double GetDensityRatioAtAltitude(double altitudeMeters) => 1d;

        public IReadOnlyList<AtmosphereSample> GetEvaluationSamples() => _samples;
    }
}
