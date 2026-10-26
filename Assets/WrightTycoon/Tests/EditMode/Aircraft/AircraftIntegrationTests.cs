using System;
using System.Reflection;
using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Data;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Research;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class AircraftIntegrationTests
{
    [TestCase(0, 0)]
    [TestCase(5, 0)]
    [TestCase(0, 5)]
    [TestCase(5, 5)]
    [TestCase(10, 10)]
    [Category("Extended")]
    public void ResearchSources_AddFamilyAndTypeWithoutDiscountingFinishedEngines(int familyPoints, int typePoints)
    {
        var factory = CreateFactory();
        var rawFamily = CreateFamily("fam.additive", 2);
        var baselineType = new AircraftTypeDefinition { Id = "type.raw", FamilyId = rawFamily.Id, DisplayName = "Raw" };
        var baseline = factory.CreateAircraft(rawFamily, baselineType);
        var family = rawFamily.WithResearchModifiers(new TypeResearchModifierPoints
        {
            ReliabilityPoints = familyPoints, ProductionOptimizationPoints = familyPoints,
        });
        var type = new AircraftTypeDefinition
        {
            Id = "type.bonus", FamilyId = family.Id, DisplayName = "Bonus",
            ResearchModifiers = new TypeResearchModifierPoints
            {
                ReliabilityPoints = typePoints, ProductionOptimizationPoints = typePoints,
            },
        };
        var actual = factory.CreateAircraft(family, type);
        var repeat = factory.CreateAircraft(family, type);
        var points = familyPoints + typePoints;
        Assert.That(actual.Performance.OverallReliability,
            Is.EqualTo(baseline.Performance.OverallReliability + .02d * points).Within(1e-9));
        Assert.That(actual.Performance.AirframeProductionCost,
            Is.EqualTo(baseline.Performance.AirframeProductionCost * (1d - .01d * points)).Within(1e-8));
        Assert.That(actual.Performance.AirframeProductionManDays,
            Is.EqualTo(baseline.Performance.AirframeProductionManDays * (1d - .02d * points)).Within(1e-8));
        Assert.That(actual.Performance.EnginesProductionCost, Is.EqualTo(baseline.Performance.EnginesProductionCost));
        Assert.That(actual.Performance.EnginesProductionManDays, Is.EqualTo(baseline.Performance.EnginesProductionManDays));
        Assert.That(actual.Performance.TotalProductionCost,
            Is.EqualTo(actual.Performance.AirframeProductionCost + actual.Performance.EnginesProductionCost).Within(1e-8));
        Assert.That(actual.Performance.TotalProductionManDays,
            Is.EqualTo(actual.Performance.AirframeProductionManDays + actual.Performance.EnginesProductionManDays).Within(1e-8));
        Assert.That(actual.Performance.CostBreakdown!.Total, Is.EqualTo(actual.Performance.TotalProductionCost).Within(1e-7));
        Assert.That(actual.Performance.ProductionManDaysBreakdown!.Total,
            Is.EqualTo(actual.Performance.TotalProductionManDays).Within(1e-7));
        Assert.That(repeat.Performance.TotalProductionCost, Is.EqualTo(actual.Performance.TotalProductionCost));
        Assert.That(repeat.Performance.OverallReliability, Is.EqualTo(actual.Performance.OverallReliability));
        Assert.That(actual.Performance.TotalWeight, Is.EqualTo(baseline.Performance.TotalWeight));
        Assert.That(actual.Performance.RangeKilometers, Is.EqualTo(baseline.Performance.RangeKilometers));
        Assert.That(actual.Performance.MaxSpeedByAltitude, Is.EqualTo(baseline.Performance.MaxSpeedByAltitude));
        Assert.That(actual.Performance.AverageCruiseFuelBurnKilogramsPerHour,
            Is.EqualTo(baseline.Performance.AverageCruiseFuelBurnKilogramsPerHour));
        if (points > 0) Assert.That(actual.Performance.ExpectedLifeHours, Is.GreaterThan(baseline.Performance.ExpectedLifeHours));
    }

    [TestCase(0, 0)]
    [TestCase(10, 0)]
    [TestCase(0, 10)]
    [TestCase(5, 5)]
    [Category("Extended")]
    public void EngineResearch_IsAppliedOnceToStandaloneAndInstalledFrozenReports(int reliability, int production)
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.engine.research", 2);
        var type = new AircraftTypeDefinition { Id = "type.engine.research", FamilyId = family.Id, DisplayName = "Engine test" };
        var baseline = factory.CreateAircraft(family, type);
        var builder = new EngineBuilder(new EngineCalculator(), new IsaAtmosphereModel());
        var modifiers = new TypeResearchModifierPoints
        {
            ReliabilityPoints = reliability, ProductionOptimizationPoints = production,
        };
        var technology = new HistoricalCampaignTechnologyProgression().ResolveForDate(new SimulationDate(1946, 1));
        var raw = builder.Build(baseline.Engine.Definition, baseline.Engine.Parameters, technology);
        var researched = builder.Build(baseline.Engine.Definition, baseline.Engine.Parameters, technology, modifiers);
        var repeat = builder.Build(baseline.Engine.Definition, baseline.Engine.Parameters, technology, modifiers);
        var installedFamily = family.WithInstalledEngineResearchModifiers(modifiers)
            .WithResearchModifiers(new TypeResearchModifierPoints { ReliabilityPoints = 5, ProductionOptimizationPoints = 5 })
            .WithInstalledEngineTechnologyModifiers(technology);
        var installed = factory.CreateAircraft(installedFamily, type);
        Assert.That(researched.Performance.Reliability, Is.EqualTo(raw.Performance.Reliability + .02d * reliability).Within(1e-9));
        Assert.That(researched.Performance.Cost, Is.EqualTo(raw.Performance.Cost * (1d - .01d * production)).Within(1e-8));
        Assert.That(researched.Performance.ProductionManDays,
            Is.EqualTo(raw.Performance.ProductionManDays * (1d - .02d * production)).Within(1e-8));
        Assert.That(researched.Performance.CostBreakdown.Total, Is.EqualTo(researched.Performance.Cost).Within(1e-7));
        Assert.That(researched.Performance.ProductionManDaysBreakdown.Total,
            Is.EqualTo(researched.Performance.ProductionManDays).Within(1e-7));
        Assert.That(researched.Performance.AltitudePerformance, Is.EqualTo(raw.Performance.AltitudePerformance));
        Assert.That(researched.Performance.Weight, Is.EqualTo(raw.Performance.Weight));
        Assert.That(repeat.Performance.Cost, Is.EqualTo(researched.Performance.Cost));
        Assert.That(repeat.Performance.Reliability, Is.EqualTo(researched.Performance.Reliability));
        Assert.That(installed.Engine.Performance.Cost, Is.EqualTo(researched.Performance.Cost));
        Assert.That(installed.Engine.Performance.ProductionManDays, Is.EqualTo(researched.Performance.ProductionManDays));
        Assert.That(installed.Engine.Performance.Reliability, Is.EqualTo(researched.Performance.Reliability));
        Assert.That(installedFamily.ResearchModifiers.ReliabilityPoints, Is.EqualTo(5));
        modifiers.ReliabilityPoints = 0;
        Assert.That(installedFamily.InstalledEngineResearchModifiers.ReliabilityPoints, Is.EqualTo(reliability));
    }

    [Test]
    public void ReplacingInstalledEngine_PreservesFamilyEffectsAndUsesOnlyNewEngineEffects()
    {
        var family = CreateFamily("fam.swap", 1)
            .WithResearchModifiers(new TypeResearchModifierPoints { ReliabilityPoints = 4 })
            .WithInstalledEngineResearchModifiers(new TypeResearchModifierPoints { ReliabilityPoints = 10 });
        var swapped = family.WithInstalledEngine(family.EngineDefinitionId, family.LockedEngineType,
            family.BaseEngineParameters, researchModifiers: new TypeResearchModifierPoints { ReliabilityPoints = 3 });
        Assert.That(swapped.ResearchModifiers.ReliabilityPoints, Is.EqualTo(4));
        Assert.That(swapped.InstalledEngineResearchModifiers.ReliabilityPoints, Is.EqualTo(3));
        var unresearched = family.WithInstalledEngine(family.EngineDefinitionId, family.LockedEngineType, family.BaseEngineParameters);
        Assert.That(unresearched.InstalledEngineResearchModifiers.ReliabilityPoints, Is.Zero);
    }

    [Test]
    [Category("Extended")]
    public void CombinedEngineeringEffects_DoNotLeakIntoResearchProjectSurcharges()
    {
        var family = new TypeResearchModifierPoints { ReliabilityPoints = 10, ProductionOptimizationPoints = 10, FastDevelopmentPoints = 10 };
        var type = new TypeResearchModifierPoints { ReliabilityPoints = 5, ProductionOptimizationPoints = 5, FastDevelopmentPoints = 5 };
        var engineering = TypeResearchModifierCalculator.CalculateCombined(family, type);
        Assert.That(engineering.ReliabilityDelta, Is.EqualTo(.3d).Within(1e-9));
        Assert.That(engineering.ProductionCostMultiplier, Is.EqualTo(.85d).Within(1e-9));
        Assert.That(engineering.ProductionManDaysMultiplier, Is.EqualTo(.7d).Within(1e-9));
        Assert.That(engineering.ResearchCostMultiplier, Is.EqualTo(1d));
        Assert.That(engineering.ResearchTimeMultiplier, Is.EqualTo(1d));
        var ownQuoteEffects = TypeResearchModifierCalculator.Calculate(type);
        Assert.That(ownQuoteEffects.ResearchCostMultiplier, Is.EqualTo(2d).Within(1e-9));
        Assert.That(ownQuoteEffects.ResearchTimeMultiplier, Is.EqualTo(1.05d).Within(1e-9));
    }

    [Test]
    [Category("Extended")]
    public void AirframePrecisionEngineering_RaisesWholeAircraftProductionAndTypeDevelopmentBurden()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.precision", 2, AircraftCategory.MediumTransportNarrowBody);
        var baselineType = new AircraftTypeDefinition
        {
            Id = "type.precision.base",
            FamilyId = family.Id,
            DisplayName = "Baseline Precision",
            AirframeDelta = new AirframeParameterDelta { PrecisionEngineeringLevelOverride = 0 },
        };
        var preciseType = new AircraftTypeDefinition
        {
            Id = "type.precision.high",
            FamilyId = family.Id,
            DisplayName = "High Precision",
            AirframeDelta = new AirframeParameterDelta { PrecisionEngineeringLevelOverride = 10 },
        };

        var baseline = factory.CreateAircraft(family, baselineType);
        var precise = factory.CreateAircraft(family, preciseType);

        Assert.That(
            precise.Performance.TotalProductionCost / baseline.Performance.TotalProductionCost,
            Is.EqualTo(1.30d).Within(0.000001d));
        Assert.That(
            precise.Performance.TotalProductionManDays / baseline.Performance.TotalProductionManDays,
            Is.EqualTo(1.45d).Within(0.000001d));
        Assert.That(precise.Performance.TotalWeight.Kilograms, Is.LessThan(baseline.Performance.TotalWeight.Kilograms));
        Assert.That(precise.Performance.OverallReliability, Is.LessThan(baseline.Performance.OverallReliability));

        var researchCalculator = new ResearchCalculator();
        var baselineQuote = researchCalculator.CalculateTypeQuote(baseline);
        var preciseQuote = researchCalculator.CalculateTypeQuote(precise);
        Assert.That(preciseQuote.ResearchCost, Is.GreaterThan(baselineQuote.ResearchCost));
        Assert.That(preciseQuote.ResearchTimeMonths, Is.GreaterThan(baselineQuote.ResearchTimeMonths));
    }

    [Test]
    [Category("Extended")]
    public void SpeedWeightFactorCurve_IsContinuousAroundFormerThresholds()
    {
        var factorAt99 = EvaluateSpeedWeightFactor(99d);
        var factorAt100 = EvaluateSpeedWeightFactor(100d);
        var factorAt101 = EvaluateSpeedWeightFactor(101d);

        Assert.That(System.Math.Abs(factorAt100 - factorAt99), Is.LessThan(0.01d));
        Assert.That(System.Math.Abs(factorAt101 - factorAt100), Is.LessThan(0.01d));
    }

    [Test]
    [Category("Extended")]
    public void SpeedWeightFactorCurve_InterpolatesBetweenConfiguredBalancePoints()
    {
        Assert.That(EvaluateSpeedWeightFactor(0d), Is.EqualTo(1.00d).Within(1e-9));
        Assert.That(EvaluateSpeedWeightFactor(100d), Is.EqualTo(0.80d).Within(1e-9));
        Assert.That(EvaluateSpeedWeightFactor(200d), Is.EqualTo(0.875d).Within(1e-9));
        Assert.That(EvaluateSpeedWeightFactor(300d), Is.EqualTo(0.95d).Within(1e-9));
        Assert.That(EvaluateSpeedWeightFactor(1000d), Is.EqualTo(1.05d).Within(1e-9));
        Assert.That(EvaluateSpeedWeightFactor(5000d), Is.EqualTo(1.40d).Within(1e-9));
    }

    [Test]
    [Category("Extended")]
    public void TwinEngineFamily_HasHigherMaxSpeedThanSingleEngineFamily()
    {
        var factory = CreateFactory();
        var singleFamily = CreateFamily("fam.single", engineCount: 1);
        var twinFamily = CreateFamily("fam.twin", engineCount: 2);
        var type = new AircraftTypeDefinition
        {
            Id = "type.base",
            FamilyId = "fam.single",
            DisplayName = "Base Variant",
        };

        var singleAircraft = factory.CreateAircraft(singleFamily, type);

        type = new AircraftTypeDefinition
        {
            Id = "type.base",
            FamilyId = "fam.twin",
            DisplayName = "Base Variant",
        };
        var twinAircraft = factory.CreateAircraft(twinFamily, type);

        Assert.That(
            twinAircraft.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond,
            Is.GreaterThan(singleAircraft.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond));
    }

    [Test]
    public void InstalledEngineTechnologyModifiers_KeepEngineOutputFixedWhenCampaignYearAdvances()
    {
        var progression = new HistoricalCampaignTechnologyProgression();
        var currentDate = new SimulationDate(1946, 1);
        var technologySource = new HistoricalCampaignRuntimeTechnologyModifierSource(
            progression,
            () => currentDate);
        var factory = CreateFactory(technologySource);
        var dynamicFamily = CreateFamily("fam.dynamic.engine", engineCount: 1);
        var fixedFamily = dynamicFamily.WithInstalledEngineTechnologyModifiers(
            progression.ResolveForDate(currentDate));
        var type = new AircraftTypeDefinition
        {
            Id = "type.fixed.engine",
            FamilyId = dynamicFamily.Id,
            DisplayName = "Fixed Engine Variant",
        };

        var initialFixedAircraft = factory.CreateAircraft(fixedFamily, type);
        var initialDynamicAircraft = factory.CreateAircraft(dynamicFamily, type);
        currentDate = new SimulationDate(1990, 1);
        var laterFixedAircraft = factory.CreateAircraft(fixedFamily, type);
        var laterDynamicAircraft = factory.CreateAircraft(dynamicFamily, type);

        var initialFixedOutput = initialFixedAircraft.Engine.Performance.GetPerformanceAtAltitude(0d).Output;
        var laterFixedOutput = laterFixedAircraft.Engine.Performance.GetPerformanceAtAltitude(0d).Output;
        var initialDynamicOutput = initialDynamicAircraft.Engine.Performance.GetPerformanceAtAltitude(0d).Output;
        var laterDynamicOutput = laterDynamicAircraft.Engine.Performance.GetPerformanceAtAltitude(0d).Output;

        Assert.That(laterFixedOutput, Is.EqualTo(initialFixedOutput).Within(1e-6));
        Assert.That(laterDynamicOutput, Is.GreaterThan(initialDynamicOutput));
    }


    [Test]
    [Category("Extended")]
    public void BiplaneWingPosition_HasLowerMaxSpeedThanLowAndHighWing()
    {
        var factory = CreateFactory();
        var lowWingFamily = CreateFamily("fam.wing.low", engineCount: 1, category: AircraftCategory.LightMultiRolePiston);
        lowWingFamily.BaseAirframeParameters.WingPosition = WingPosition.Low;

        var highWingFamily = CreateFamily("fam.wing.high", engineCount: 1, category: AircraftCategory.LightMultiRolePiston);
        highWingFamily.BaseAirframeParameters.WingPosition = WingPosition.High;

        var biplaneFamily = CreateFamily("fam.wing.biplane", engineCount: 1, category: AircraftCategory.LightMultiRolePiston);
        biplaneFamily.BaseAirframeParameters.WingPosition = WingPosition.Biplane;

        var lowWingType = new AircraftTypeDefinition
        {
            Id = "type.wing.low",
            FamilyId = lowWingFamily.Id,
            DisplayName = "Low Wing Variant",
        };

        var highWingType = new AircraftTypeDefinition
        {
            Id = "type.wing.high",
            FamilyId = highWingFamily.Id,
            DisplayName = "High Wing Variant",
        };

        var biplaneType = new AircraftTypeDefinition
        {
            Id = "type.wing.biplane",
            FamilyId = biplaneFamily.Id,
            DisplayName = "Biplane Variant",
        };

        var lowWingAircraft = factory.CreateAircraft(lowWingFamily, lowWingType);
        var highWingAircraft = factory.CreateAircraft(highWingFamily, highWingType);
        var biplaneAircraft = factory.CreateAircraft(biplaneFamily, biplaneType);

        var lowSpeed = lowWingAircraft.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond;
        var highSpeed = highWingAircraft.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond;
        var biplaneSpeed = biplaneAircraft.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond;

        Assert.That(highSpeed, Is.LessThan(lowSpeed));
        Assert.That(biplaneSpeed, Is.LessThan(highSpeed));
        Assert.That(lowSpeed - biplaneSpeed, Is.GreaterThan(8d));
        Assert.That(
            biplaneAircraft.Performance.MaximumTakeoffWeightKilograms,
            Is.GreaterThan(highWingAircraft.Performance.MaximumTakeoffWeightKilograms));
        Assert.That(
            biplaneAircraft.Performance.PayloadCapacity.Kilograms,
            Is.GreaterThan(highWingAircraft.Performance.PayloadCapacity.Kilograms));
    }
    [Test]
    [Category("Extended")]
    public void FamilyLocks_OverrideTypeParameterOverrides()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.locked", engineCount: 2);
        var type = new AircraftTypeDefinition
        {
            Id = "type.override",
            FamilyId = "fam.locked",
            DisplayName = "Override Attempt",
            AirframeDelta = new AirframeParameterDelta
            {
                EngineCountOverride = 4,
                WingShapeOverride = WingShape.Delta,
                WingAreaSquareMetersDelta = 7d,
            },
        };

        var aircraft = factory.CreateAircraft(family, type);

        Assert.That(aircraft.Airframe.Parameters.EngineCount, Is.EqualTo(2));
        Assert.That(aircraft.Airframe.Parameters.StructureType, Is.EqualTo(family.LockedStructureType));
        Assert.That(aircraft.Airframe.Parameters.MaterialType, Is.EqualTo(family.LockedMaterialType));
        Assert.That(aircraft.Airframe.Parameters.WingShape, Is.EqualTo(WingShape.Delta));
        Assert.That(aircraft.Performance.CostBreakdown!.Total, Is.EqualTo(aircraft.Performance.TotalProductionCost).Within(0.000001d));
        Assert.That(
            aircraft.Performance.ProductionManDaysBreakdown!.Total,
            Is.EqualTo(aircraft.Performance.TotalProductionManDays).Within(0.000001d));
    }

    [Test]
    [Category("Extended")]
    public void FamilyEnginePolicy_BlocksIncompatibleEngineFamilies()
    {
        var factory = CreateFactory();
        var family = CreateFamily(
            "fam.engine.policy",
            engineCount: 1,
            category: AircraftCategory.LightMultiRolePiston,
            allowedEngineFamily: AircraftAllowedEngineFamily.Jet);
        var type = new AircraftTypeDefinition
        {
            Id = "type.engine.policy",
            FamilyId = family.Id,
            DisplayName = "Engine Policy",
        };

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateAircraft(family, type));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("engine policy"));
    }

    [Test]
    [Category("Extended")]
    public void FamilyEngineClassification_BlocksIncompatibleEngineBranches()
    {
        var factory = CreateFactory();
        var family = CreateFamily(
            "fam.engine.classification",
            engineCount: 1,
            category: AircraftCategory.LightMultiRolePiston,
            engineClassification: AircraftEngineClassification.Jet);
        var type = new AircraftTypeDefinition
        {
            Id = "type.engine.classification",
            FamilyId = family.Id,
            DisplayName = "Engine Classification",
        };

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateAircraft(family, type));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("engine classification"));
    }

    [Test]
    [Category("Extended")]
    public void TypeWingArea_MustStayWithinTwentyPercentOfFamilyReference()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.wing.envelope", engineCount: 1);
        var type = new AircraftTypeDefinition
        {
            Id = "type.wing.envelope",
            FamilyId = family.Id,
            DisplayName = "Wing Envelope",
            AirframeDelta = new AirframeParameterDelta
            {
                WingAreaSquareMetersDelta = family.BaseAirframeParameters.WingAreaSquareMeters * 0.25d,
            },
        };

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateAircraft(family, type));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("wing area"));
    }

    [Test]
    [Category("Extended")]
    public void LargerTypeDelta_IncreasesDevelopmentCostAndManDays()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.dev", engineCount: 1);

        var mildType = new AircraftTypeDefinition
        {
            Id = "type.mild",
            FamilyId = "fam.dev",
            DisplayName = "Mild Delta",
            EngineDelta = new EngineParameterDelta
            {
                CompressionRatioDelta = 0.2d,
            },
            AirframeDelta = new AirframeParameterDelta
            {
                WingAreaSquareMetersDelta = 1.2d,
            },
        };

        var aggressiveType = new AircraftTypeDefinition
        {
            Id = "type.aggressive",
            FamilyId = "fam.dev",
            DisplayName = "Aggressive Delta",
            EngineDelta = new EngineParameterDelta
            {
                DisplacementLitersDelta = 5d,
                CompressionRatioDelta = 1.5d,
                BoostTypeOverride = BoostType.Turbo,
                OptimizationAltitudeMetersDelta = 2500d,
                CoolingEfficiencyDelta = -0.12d,
            },
            AirframeDelta = new AirframeParameterDelta
            {
                WingAreaSquareMetersDelta = 7d,
                FuelAmountLitersDelta = 600d,
                CrewCountDelta = 2,
                EngineCowlingDelta = 0.25d,
                WingShapeOverride = WingShape.Swept,
                TailTypeOverride = TailType.TTail,
                LandingGearTypeOverride = LandingGearType.Retractable,
            },
        };

        var mildAircraft = factory.CreateAircraft(family, mildType);
        var aggressiveAircraft = factory.CreateAircraft(family, aggressiveType);

        Assert.That(aggressiveAircraft.Development.DevelopmentCost, Is.EqualTo(0d).Within(1e-9d));
        Assert.That(aggressiveAircraft.Development.DevelopmentManDays, Is.EqualTo(0d).Within(1e-9d));
        Assert.That(aggressiveAircraft.Development.ChangedParameterCount, Is.GreaterThan(mildAircraft.Development.ChangedParameterCount));
    }

    [Test]
    [Category("Extended")]
    public void CategoryModifiers_AdjustWeightSpeedReliabilityAndRange()
    {
        var factory = CreateFactory();
        var baselineFamily = CreateFamily("fam.category.base", engineCount: 2, category: AircraftCategory.LightSportAndRecreational);
        var boostedFamily = CreateFamily("fam.category.boosted", engineCount: 2, category: AircraftCategory.MediumTransportNarrowBody);

        var baselineType = new AircraftTypeDefinition
        {
            Id = "type.category.base",
            FamilyId = baselineFamily.Id,
            DisplayName = "Category Baseline",
        };

        var boostedType = new AircraftTypeDefinition
        {
            Id = "type.category.boosted",
            FamilyId = boostedFamily.Id,
            DisplayName = "Category Boosted",
        };

        var baselineAircraft = factory.CreateAircraft(baselineFamily, baselineType);
        var boostedAircraft = factory.CreateAircraft(boostedFamily, boostedType);

        Assert.That(
            boostedAircraft.Performance.TotalWeight.Kilograms - baselineAircraft.Performance.TotalWeight.Kilograms,
            Is.GreaterThan(900d));

        Assert.That(
            boostedAircraft.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond - baselineAircraft.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond,
            Is.GreaterThan(5d));

        Assert.That(
            boostedAircraft.Performance.OverallReliability - baselineAircraft.Performance.OverallReliability,
            Is.GreaterThan(0.04d));

        Assert.That(
            boostedAircraft.Performance.RangeKilometers,
            Is.GreaterThan(baselineAircraft.Performance.RangeKilometers));
    }

    [Test]
    [Category("Extended")]
    public void NavigationEquipment_AddsWeightDragReliabilityAndProductionCost()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.navigation.effects", engineCount: 2, category: AircraftCategory.LightMultiRolePiston);

        var noNavigationType = new AircraftTypeDefinition
        {
            Id = "type.navigation.none",
            FamilyId = family.Id,
            DisplayName = "No Navigation",
            NavigationEquipment = AircraftNavigationEquipment.None,
        };
        var basicNavigationType = new AircraftTypeDefinition
        {
            Id = "type.navigation.basic",
            FamilyId = family.Id,
            DisplayName = "Basic Navigation",
            NavigationEquipment = AircraftNavigationEquipment.BasicNavigationSuite,
        };
        var advancedNavigationType = new AircraftTypeDefinition
        {
            Id = "type.navigation.advanced",
            FamilyId = family.Id,
            DisplayName = "Advanced Navigation",
            NavigationEquipment = AircraftNavigationEquipment.AdvancedNavigationSuite,
        };

        var noNavigation = factory.CreateAircraft(family, noNavigationType);
        var basicNavigation = factory.CreateAircraft(family, basicNavigationType);
        var advancedNavigation = factory.CreateAircraft(family, advancedNavigationType);

        Assert.That(basicNavigation.Performance.TotalWeight.Kilograms, Is.GreaterThan(noNavigation.Performance.TotalWeight.Kilograms));
        Assert.That(advancedNavigation.Performance.TotalWeight.Kilograms, Is.GreaterThan(basicNavigation.Performance.TotalWeight.Kilograms));
        Assert.That(basicNavigation.Performance.PayloadCapacity.Kilograms, Is.LessThan(noNavigation.Performance.PayloadCapacity.Kilograms));
        Assert.That(advancedNavigation.Performance.PayloadCapacity.Kilograms, Is.LessThan(basicNavigation.Performance.PayloadCapacity.Kilograms));
        Assert.That(basicNavigation.Performance.TotalDragAreaSquareMeters, Is.GreaterThan(noNavigation.Performance.TotalDragAreaSquareMeters));
        Assert.That(advancedNavigation.Performance.TotalDragAreaSquareMeters, Is.GreaterThan(basicNavigation.Performance.TotalDragAreaSquareMeters));
        Assert.That(basicNavigation.Performance.OverallReliability, Is.GreaterThan(noNavigation.Performance.OverallReliability));
        Assert.That(advancedNavigation.Performance.OverallReliability, Is.GreaterThan(basicNavigation.Performance.OverallReliability));
        var expectedBasicParts = System.Math.Max(
            ProductionEconomyBalance.Airframe.BasicNavigationMinimumParts,
            noNavigation.Airframe.Performance.ProductionCost * ProductionEconomyBalance.Airframe.BasicNavigationPartsRate);
        var expectedAdvancedParts = System.Math.Max(
            ProductionEconomyBalance.Airframe.AdvancedNavigationMinimumParts,
            noNavigation.Airframe.Performance.ProductionCost * ProductionEconomyBalance.Airframe.AdvancedNavigationPartsRate);
        var expectedBasicManDays = System.Math.Max(
            ProductionEconomyBalance.Airframe.BasicNavigationMinimumManDays,
            noNavigation.Airframe.Performance.ProductionManDays * ProductionEconomyBalance.Airframe.BasicNavigationManDaysRate);
        var expectedAdvancedManDays = System.Math.Max(
            ProductionEconomyBalance.Airframe.AdvancedNavigationMinimumManDays,
            noNavigation.Airframe.Performance.ProductionManDays * ProductionEconomyBalance.Airframe.AdvancedNavigationManDaysRate);
        Assert.That(basicNavigation.Performance.TotalProductionCost, Is.EqualTo(noNavigation.Performance.TotalProductionCost + expectedBasicParts).Within(0.01d));
        Assert.That(advancedNavigation.Performance.TotalProductionCost, Is.EqualTo(noNavigation.Performance.TotalProductionCost + expectedAdvancedParts).Within(0.01d));
        Assert.That(basicNavigation.Performance.TotalProductionManDays, Is.EqualTo(noNavigation.Performance.TotalProductionManDays + expectedBasicManDays).Within(0.01d));
        Assert.That(advancedNavigation.Performance.TotalProductionManDays, Is.EqualTo(noNavigation.Performance.TotalProductionManDays + expectedAdvancedManDays).Within(0.01d));
    }

    [Test]
    [Category("Extended")]
    public void PassengerCapacity_IsComputedFromPayloadUsing250KgPerPassenger()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.passenger", engineCount: 2, category: AircraftCategory.BusinessVipJet);
        var type = new AircraftTypeDefinition
        {
            Id = "type.passenger",
            FamilyId = family.Id,
            DisplayName = "Passenger Variant",
        };

        var aircraft = factory.CreateAircraft(family, type);
        var expectedPassengers = (int)System.Math.Floor(
            aircraft.Performance.PayloadCapacity.Kilograms / WrightTycoonTuning.Categories.PayloadKilogramsPerPassenger);

        Assert.That(aircraft.Performance.PassengerCapacity, Is.EqualTo(expectedPassengers));
    }

    [Test]
    [Category("Extended")]
    public void LongRunway_AllowsHigherMaximumTakeoffWeightThanShortRunway()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.runway", engineCount: 2, category: AircraftCategory.TurbopropRegional);

        var shortRunwayType = new AircraftTypeDefinition
        {
            Id = "type.runway.short",
            FamilyId = family.Id,
            DisplayName = "Short Runway",
            AirframeDelta = new AirframeParameterDelta
            {
                RunwayTypeOverride = RunwayType.Short,
            },
        };

        var longRunwayType = new AircraftTypeDefinition
        {
            Id = "type.runway.long",
            FamilyId = family.Id,
            DisplayName = "Long Runway",
            AirframeDelta = new AirframeParameterDelta
            {
                RunwayTypeOverride = RunwayType.Long,
            },
        };

        var shortRunwayAircraft = factory.CreateAircraft(family, shortRunwayType);
        var longRunwayAircraft = factory.CreateAircraft(family, longRunwayType);

        Assert.That(
            longRunwayAircraft.Performance.MaximumTakeoffWeightKilograms,
            Is.GreaterThan(shortRunwayAircraft.Performance.MaximumTakeoffWeightKilograms));
        Assert.That(
            longRunwayAircraft.Performance.PayloadCapacity.Kilograms,
            Is.GreaterThanOrEqualTo(shortRunwayAircraft.Performance.PayloadCapacity.Kilograms));
    }

    [Test]
    [Category("Extended")]
    public void AircraftWeightWithFuel_UsesTenPercentEmptyWeightMarginPlusFuelMass()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.weight.withfuel", engineCount: 2, category: AircraftCategory.LightMultiRolePiston);
        var type = new AircraftTypeDefinition
        {
            Id = "type.weight.withfuel",
            FamilyId = family.Id,
            DisplayName = "Weight Check",
        };

        var aircraft = factory.CreateAircraft(family, type);
        var fuelMassKilograms = aircraft.Airframe.Parameters.FuelAmountLiters
            * WrightTycoonTuning.AircraftPerformance.FuelDensityKilogramsPerLiter;
        var expectedAircraftWeightWithFuelKilograms =
            (aircraft.Performance.EmptyAircraftWeightKilograms * (1d + WrightTycoonTuning.AircraftPerformance.WetWeightMarginRatio))
            + fuelMassKilograms;

        Assert.That(
            aircraft.Performance.AircraftWeightWithFuelKilograms,
            Is.EqualTo(expectedAircraftWeightWithFuelKilograms).Within(0.05d));
    }

    [Test]
    [Category("Extended")]
    public void UsefulWeight_EqualsMaximumTakeoffWeightMinusAircraftWeightWithFuel()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.usefulweight", engineCount: 1, category: AircraftCategory.SpecialCivil);
        var type = new AircraftTypeDefinition
        {
            Id = "type.usefulweight",
            FamilyId = family.Id,
            DisplayName = "Useful Weight Check",
        };

        var aircraft = factory.CreateAircraft(family, type);
        var expectedUsefulWeightKilograms = System.Math.Max(
            0d,
            aircraft.Performance.MaximumTakeoffWeightKilograms - aircraft.Performance.AircraftWeightWithFuelKilograms);

        Assert.That(
            aircraft.Performance.PayloadCapacity.Kilograms,
            Is.EqualTo(expectedUsefulWeightKilograms).Within(0.05d));
    }

    [Test]
    [Category("Extended")]
    public void PayloadLimitCapsUsefulWeightAndReducesMaximumTakeoffWeight()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.payload.limit", engineCount: 2, category: AircraftCategory.BusinessVipJet);

        var baselineType = new AircraftTypeDefinition
        {
            Id = "type.payload.limit.baseline",
            FamilyId = family.Id,
            DisplayName = "No Payload Limit",
        };

        var limitedType = new AircraftTypeDefinition
        {
            Id = "type.payload.limit.capped",
            FamilyId = family.Id,
            DisplayName = "Payload Limited",
            AirframeDelta = new AirframeParameterDelta
            {
                PayloadLimitKilogramsOverride = 1000d,
            },
        };

        var baselineAircraft = factory.CreateAircraft(family, baselineType);
        var limitedAircraft = factory.CreateAircraft(family, limitedType);

        Assert.That(limitedAircraft.Performance.PayloadCapacity.Kilograms, Is.EqualTo(1000d).Within(0.05d));
        Assert.That(
            limitedAircraft.Performance.MaximumTakeoffWeightKilograms,
            Is.LessThan(baselineAircraft.Performance.MaximumTakeoffWeightKilograms));
    }

    [Test]
    [Category("Extended")]
    public void OptimalCruiseSpeed_IsPositiveAndWithinMaxSpeedEnvelope()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.cruise", engineCount: 2, category: AircraftCategory.MediumTransportNarrowBody);
        var type = new AircraftTypeDefinition
        {
            Id = "type.cruise",
            FamilyId = family.Id,
            DisplayName = "Cruise Variant",
        };

        var aircraft = factory.CreateAircraft(family, type);
        var cruiseAltitude = aircraft.Performance.OptimalCruiseAltitudeMeters;
        var maxSpeedAtCruise = 0d;
        for (var i = 0; i < aircraft.Performance.MaxSpeedByAltitude.Count; i++)
        {
            var point = aircraft.Performance.MaxSpeedByAltitude[i];
            if (System.Math.Abs(point.AltitudeMeters - cruiseAltitude) < 1e-6)
            {
                maxSpeedAtCruise = point.MaxSpeedMetersPerSecond;
                break;
            }
        }

        Assert.That(aircraft.Performance.OptimalCruiseSpeedMetersPerSecond, Is.GreaterThan(0d));
        Assert.That(maxSpeedAtCruise, Is.GreaterThan(0d));
        Assert.That(aircraft.Performance.OptimalCruiseSpeedMetersPerSecond, Is.LessThanOrEqualTo(maxSpeedAtCruise + 1e-6));
        Assert.That(aircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour, Is.GreaterThan(0d));
    }

    [Test]
    [Category("Extended")]
    public void MoreFuel_IncreasesRangeAtOptimalCruise()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.cruise.range", engineCount: 2, category: AircraftCategory.BusinessVipJet);

        var lowFuelType = new AircraftTypeDefinition
        {
            Id = "type.cruise.range.low",
            FamilyId = family.Id,
            DisplayName = "Low Fuel",
            AirframeDelta = new AirframeParameterDelta
            {
                FuelAmountLitersDelta = -500d,
            },
        };

        var highFuelType = new AircraftTypeDefinition
        {
            Id = "type.cruise.range.high",
            FamilyId = family.Id,
            DisplayName = "High Fuel",
            AirframeDelta = new AirframeParameterDelta
            {
                FuelAmountLitersDelta = 500d,
            },
        };

        var lowFuelAircraft = factory.CreateAircraft(family, lowFuelType);
        var highFuelAircraft = factory.CreateAircraft(family, highFuelType);

        Assert.That(highFuelAircraft.Performance.RangeKilometers, Is.GreaterThan(lowFuelAircraft.Performance.RangeKilometers));
    }

    [Test]
    [Category("Extended")]
    public void HighBypassJet_MaxSpeedIsCappedToTransportEnvelope()
    {
        var calculator = new AircraftPerformanceCalculator(new IsaAtmosphereModel(), new AerodynamicsCalculator());

        var engine = new Engine(
            new EngineDefinition
            {
                Id = "eng.jet.cap",
                DisplayName = "High Bypass Jet",
                EngineType = EngineType.Turbofan,
            },
            new EngineParameters
            {
                BypassRatio = 10d,
                TurbineDiameterMeters = 3.2d,
                CompressionRatio = 45d,
                CoolingEfficiency = 0.85d,
                MaximumSpeed = 5000d,
            },
            new EnginePerformanceReport(
                EngineOutputKind.ThrustNewtons,
                new[]
                {
                    new EngineAltitudePerformance(0d, 282000d, 6500d),
                    new EngineAltitudePerformance(5000d, 220000d, 5200d),
                    new EngineAltitudePerformance(10000d, 180000d, 4100d),
                },
                reliability: 0.93d,
                weight: Mass.FromKilograms(6200d),
                cost: 30000000d,
                productionManDays: 180d,
                engineLengthMeters: 6.5d,
                engineFrontalAreaSquareMeters: 6.4d));

        var airframe = new Airframe(
            new AirframeDefinition
            {
                Id = "air.jet.cap",
                DisplayName = "Large Widebody Frame",
                BaseStructuralWeightKilograms = 120000d,
                ReferenceWingAreaSquareMeters = 541d,
                BaseDragCoefficient = 0.022d,
                BaseLiftCoefficient = 0.62d,
                BaseReliabilityModifier = 1d,
                BaseCost = 200000000d,
                BaseProductionManDays = 1200d,
            },
            new AirframeParameters
            {
                EngineCount = 4,
                StructureType = StructureType.Monocoque,
                MaterialType = MaterialType.Aluminum,
                WingPosition = WingPosition.Low,
                WingShape = WingShape.Swept,
                WingAreaSquareMeters = 541d,
                TailType = TailType.Classic,
                FlapType = FlapType.Fowler,
                LandingGearType = LandingGearType.Retractable,
                EngineCowling = 0.6d,
                FuelAmountLiters = 50000d,
                CrewCount = 4,
            },
            new AirframePerformanceReport(
                TotalWeight: Mass.FromKilograms(180000d),
                DragCoefficient: 0.022d,
                LiftCoefficient: 0.62d,
                Maneuverability: 30d,
                ProductionCost: 300000000d,
                ProductionManDays: 2400d,
                ReliabilityModifier: 1d,
                ReferenceLiftAtSeaLevel100Ms: Force.FromNewtons(2200000d),
                ReferenceDragAtSeaLevel100Ms: Force.FromNewtons(36000d),
                AircraftLengthMeters: 70d,
                AirframeFrontalAreaSquareMeters: 18d,
                EffectiveDragAreaSquareMeters: 0.396d,
                AerodynamicCleanlinessPercent: 12d));

        var performance = calculator.Calculate(engine, airframe, AircraftCategory.HeavyTransportWideBody);
        var highestSpeed = 0d;
        for (var index = 0; index < performance.MaxSpeedByAltitude.Count; index++)
        {
            highestSpeed = System.Math.Max(highestSpeed, performance.MaxSpeedByAltitude[index].MaxSpeedMetersPerSecond);
        }

        var cap = WrightTycoonTuning.AircraftPerformance.JetSpeedEnvelopeHighBypassLimitMetersPerSecond
            * WrightTycoonTuning.AircraftPerformance.JetSpeedEnvelopeCategoryHeavyTransportFactor;
        Assert.That(highestSpeed, Is.LessThanOrEqualTo(cap + 1e-6));
    }

    [Test]
    [Category("Extended")]
    public void MediumBypassJet_MaxSpeedEnvelope_KeepsAltitudeSpeedProfileWhenCapped()
    {
        var calculator = new AircraftPerformanceCalculator(new IsaAtmosphereModel(), new AerodynamicsCalculator());

        var engine = new Engine(
            new EngineDefinition
            {
                Id = "eng.jet.cap.profile",
                DisplayName = "Medium Bypass Jet",
                EngineType = EngineType.Turbofan,
            },
            new EngineParameters
            {
                BypassRatio = 5d,
                TurbineDiameterMeters = 2.4d,
                CompressionRatio = 32d,
                CoolingEfficiency = 0.85d,
                MaximumSpeed = 8000d,
            },
            new EnginePerformanceReport(
                EngineOutputKind.ThrustNewtons,
                new[]
                {
                    new EngineAltitudePerformance(0d, 360000d, 7800d),
                    new EngineAltitudePerformance(5000d, 240000d, 5900d),
                    new EngineAltitudePerformance(10000d, 130000d, 4200d),
                },
                reliability: 0.92d,
                weight: Mass.FromKilograms(4600d),
                cost: 22000000d,
                productionManDays: 170d,
                engineLengthMeters: 5.6d,
                engineFrontalAreaSquareMeters: 4.1d));

        var airframe = new Airframe(
            new AirframeDefinition
            {
                Id = "air.jet.cap.profile",
                DisplayName = "Medium Tactical Frame",
                BaseStructuralWeightKilograms = 42000d,
                ReferenceWingAreaSquareMeters = 210d,
                BaseDragCoefficient = 0.027d,
                BaseLiftCoefficient = 0.82d,
                BaseReliabilityModifier = 0.98d,
                BaseCost = 120000000d,
                BaseProductionManDays = 980d,
            },
            new AirframeParameters
            {
                EngineCount = 2,
                StructureType = StructureType.Monocoque,
                MaterialType = MaterialType.Aluminum,
                WingPosition = WingPosition.Low,
                WingShape = WingShape.Swept,
                WingAreaSquareMeters = 210d,
                TailType = TailType.Classic,
                FlapType = FlapType.Fowler,
                LandingGearType = LandingGearType.Retractable,
                EngineCowling = 0.6d,
                FuelAmountLiters = 16000d,
                CrewCount = 2,
            },
            new AirframePerformanceReport(
                TotalWeight: Mass.FromKilograms(56000d),
                DragCoefficient: 0.027d,
                LiftCoefficient: 0.82d,
                Maneuverability: 52d,
                ProductionCost: 165000000d,
                ProductionManDays: 1400d,
                ReliabilityModifier: 0.97d,
                ReferenceLiftAtSeaLevel100Ms: Force.FromNewtons(1100000d),
                ReferenceDragAtSeaLevel100Ms: Force.FromNewtons(33000d),
                AircraftLengthMeters: 32d,
                AirframeFrontalAreaSquareMeters: 10.6d,
                EffectiveDragAreaSquareMeters: 0.2862d,
                AerodynamicCleanlinessPercent: 14d));

        var performance = calculator.Calculate(engine, airframe, AircraftCategory.MilitaryTactical);
        var cap = WrightTycoonTuning.AircraftPerformance.JetSpeedEnvelopeMediumBypassLimitMetersPerSecond
            * WrightTycoonTuning.AircraftPerformance.JetSpeedEnvelopeCategoryMilitaryTacticalFactor;

        var maxSpeed = 0d;
        for (var index = 0; index < performance.MaxSpeedByAltitude.Count; index++)
        {
            maxSpeed = System.Math.Max(maxSpeed, performance.MaxSpeedByAltitude[index].MaxSpeedMetersPerSecond);
        }

        var seaLevelSpeed = performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond;
        var fiveKilometerSpeed = performance.MaxSpeedByAltitude[1].MaxSpeedMetersPerSecond;
        var tenKilometerSpeed = performance.MaxSpeedByAltitude[2].MaxSpeedMetersPerSecond;
        var hasAltitudeVariation = System.Math.Abs(seaLevelSpeed - fiveKilometerSpeed) > 0.5d
            || System.Math.Abs(fiveKilometerSpeed - tenKilometerSpeed) > 0.5d
            || System.Math.Abs(seaLevelSpeed - tenKilometerSpeed) > 0.5d;

        Assert.That(maxSpeed, Is.LessThanOrEqualTo(cap + 1e-6));
        Assert.That(hasAltitudeVariation, Is.True);
    }

    [Test]
    [Category("Extended")]
    public void LargeWingAndWeight_AirframeLengthScalesIntoLargeAircraftRange()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = new AirframeDefinition
        {
            Id = "air.large.length",
            DisplayName = "Large Length Calibration",
            BaseStructuralWeightKilograms = 120000d,
            ReferenceWingAreaSquareMeters = 541d,
            BaseDragCoefficient = 0.022d,
            BaseLiftCoefficient = 0.62d,
            BaseReliabilityModifier = 1d,
            BaseCost = 200000000d,
            BaseProductionManDays = 1200d,
        };

        var large = calculator.Calculate(
            definition,
            new AirframeParameters
            {
                EngineCount = 4,
                StructureType = StructureType.Monocoque,
                MaterialType = MaterialType.Aluminum,
                WingPosition = WingPosition.Low,
                WingShape = WingShape.Swept,
                WingAreaSquareMeters = 845d,
                TailType = TailType.Classic,
                FlapType = FlapType.Fowler,
                LandingGearType = LandingGearType.Retractable,
                EngineCowling = 0.6d,
                FuelAmountLiters = 50000d,
                CrewCount = 4,
            });

        var medium = calculator.Calculate(
            definition,
            new AirframeParameters
            {
                EngineCount = 2,
                StructureType = StructureType.Monocoque,
                MaterialType = MaterialType.Aluminum,
                WingPosition = WingPosition.Low,
                WingShape = WingShape.Swept,
                WingAreaSquareMeters = 122.6d,
                TailType = TailType.Classic,
                FlapType = FlapType.Fowler,
                LandingGearType = LandingGearType.Retractable,
                EngineCowling = 0.6d,
                FuelAmountLiters = 18000d,
                CrewCount = 2,
            });

        Assert.That(large.AircraftLengthMeters, Is.GreaterThan(55d));
        Assert.That(large.AircraftLengthMeters, Is.GreaterThan(medium.AircraftLengthMeters + 20d));
    }


    [Test]
    public void TypeResearchModifiers_AdjustAircraftReliabilityAndProductionValues()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.type.modifiers", engineCount: 2, category: AircraftCategory.BusinessVipJet);

        var baselineType = new AircraftTypeDefinition
        {
            Id = "type.type.modifiers.base",
            FamilyId = family.Id,
            DisplayName = "Baseline",
        };

        var optimizedType = new AircraftTypeDefinition
        {
            Id = "type.type.modifiers.optimized",
            FamilyId = family.Id,
            DisplayName = "Optimized",
            ResearchModifiers = new TypeResearchModifierPoints
            {
                ReliabilityPoints = 5,
                ProductionOptimizationPoints = 6,
            },
        };

        var baselineAircraft = factory.CreateAircraft(family, baselineType);
        var optimizedAircraft = factory.CreateAircraft(family, optimizedType);
        var effects = TypeResearchModifierCalculator.Calculate(optimizedType.ResearchModifiers);

        var expectedReliability = baselineAircraft.Performance.OverallReliability + effects.ReliabilityDelta;
        var expectedProductionCost = baselineAircraft.Performance.AirframeProductionCost * effects.ProductionCostMultiplier
            + baselineAircraft.Performance.EnginesProductionCost;
        var expectedProductionManDays = baselineAircraft.Performance.AirframeProductionManDays * effects.ProductionManDaysMultiplier
            + baselineAircraft.Performance.EnginesProductionManDays;

        Assert.That(optimizedAircraft.Performance.OverallReliability, Is.EqualTo(expectedReliability).Within(1e-9));
        Assert.That(optimizedAircraft.Performance.TotalProductionCost, Is.EqualTo(expectedProductionCost).Within(0.01d));
        Assert.That(optimizedAircraft.Performance.TotalProductionManDays, Is.EqualTo(expectedProductionManDays).Within(0.01d));
    }

    [Test]
    [Category("Extended")]
    public void TypeResearchModifiers_AdjustResearchTypeQuoteCostAndTime()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.quote.modifiers", engineCount: 2, category: AircraftCategory.BusinessVipJet);
        var calculator = new ResearchCalculator();

        var baselineType = new AircraftTypeDefinition
        {
            Id = "type.quote.base",
            FamilyId = family.Id,
            DisplayName = "Quote Baseline",
        };

        var modifiedType = new AircraftTypeDefinition
        {
            Id = "type.quote.modified",
            FamilyId = family.Id,
            DisplayName = "Quote Modified",
            ResearchModifiers = new TypeResearchModifierPoints
            {
                ReliabilityPoints = 4,
                ProductionOptimizationPoints = 3,
                FastDevelopmentPoints = 5,
            },
        };

        var baselineAircraft = factory.CreateAircraft(family, baselineType);
        var modifiedAircraft = factory.CreateAircraft(family, modifiedType);

        var baselineQuote = calculator.CalculateTypeQuote(baselineAircraft);
        var modifiedQuote = calculator.CalculateTypeQuote(modifiedAircraft);
        var modifierEffects = TypeResearchModifierCalculator.Calculate(modifiedType.ResearchModifiers);

        var expectedMonths = ComputeExpectedTypeResearchMonths(modifiedAircraft, modifierEffects);
        var expectedCost = ComputeExpectedTypeResearchCost(modifiedAircraft, modifierEffects);

        Assert.That(modifiedQuote.ResearchTimeMonths, Is.EqualTo(expectedMonths));
        Assert.That(modifiedQuote.ResearchCost, Is.EqualTo(expectedCost).Within(0.01d));
        Assert.That(modifiedQuote.ResearchTimeMonths, Is.LessThanOrEqualTo(baselineQuote.ResearchTimeMonths));
        Assert.That(modifiedQuote.ResearchCost, Is.GreaterThan(baselineQuote.ResearchCost));
    }

    [Test]
    [Category("Extended")]
    public void ResearchModifiers_AdjustEngineQuoteCostAndTime()
    {
        var calculator = new ResearchCalculator();
        var engine = new Engine(
            new EngineDefinition
            {
                Id = "eng.quote.mod",
                DisplayName = "Quoted Engine",
                EngineType = EngineType.Radial,
            },
            new EngineParameters
            {
                DisplacementLiters = 16d,
                CylinderCount = 14,
                CompressionRatio = 8.2d,
                BoostType = BoostType.None,
                OptimizationAltitudeMeters = 1800d,
                CoolingEfficiency = 0.78d,
                MaximumSpeed = 2600d,
            },
            new EnginePerformanceReport(
                EngineOutputKind.PowerKilowatts,
                new[]
                {
                    new EngineAltitudePerformance(0d, 980d, 0d),
                    new EngineAltitudePerformance(2000d, 870d, 0d),
                },
                reliability: 0.88d,
                weight: Mass.FromKilograms(320d),
                cost: 44000d,
                productionManDays: 78d,
                engineLengthMeters: 1.9d,
                engineFrontalAreaSquareMeters: 1.1d));

        var points = new TypeResearchModifierPoints
        {
            ReliabilityPoints = 3,
            ProductionOptimizationPoints = 2,
            FastDevelopmentPoints = 4,
        };

        var baselineQuote = calculator.CalculateEngineQuote(engine);
        var modifiedQuote = calculator.CalculateEngineQuote(engine, points);
        var effects = TypeResearchModifierCalculator.Calculate(points);

        Assert.That(modifiedQuote.ResearchTimeMonths, Is.EqualTo(ApplyResearchTimeMultiplier(baselineQuote.ResearchTimeMonths, effects.ResearchTimeMultiplier)));
        Assert.That(modifiedQuote.ResearchCost, Is.EqualTo(ApplyResearchCostMultiplier(baselineQuote.ResearchCost, effects.ResearchCostMultiplier)).Within(0.01d));
        Assert.That(modifiedQuote.ResearchTimeMonths, Is.LessThanOrEqualTo(baselineQuote.ResearchTimeMonths));
        Assert.That(modifiedQuote.ResearchCost, Is.GreaterThan(baselineQuote.ResearchCost));
    }

    [Test]
    [Category("Extended")]
    public void ResearchModifiers_AdjustFamilyQuoteCostAndTime()
    {
        var factory = CreateFactory();
        var family = CreateFamily("fam.quote.family.mod", engineCount: 2, category: AircraftCategory.BusinessVipJet);
        var type = new AircraftTypeDefinition
        {
            Id = "type.quote.family.mod",
            FamilyId = family.Id,
            DisplayName = "Family Quote Variant",
        };

        var aircraft = factory.CreateAircraft(family, type);
        var calculator = new ResearchCalculator();
        var points = new TypeResearchModifierPoints
        {
            ReliabilityPoints = 2,
            ProductionOptimizationPoints = 3,
            FastDevelopmentPoints = 3,
        };

        var baselineQuote = calculator.CalculateFamilyQuote(aircraft);
        var modifiedQuote = calculator.CalculateFamilyQuote(aircraft, points);
        var effects = TypeResearchModifierCalculator.Calculate(points);

        Assert.That(modifiedQuote.ResearchTimeMonths, Is.EqualTo(ApplyResearchTimeMultiplier(baselineQuote.ResearchTimeMonths, effects.ResearchTimeMultiplier)));
        Assert.That(modifiedQuote.ResearchCost, Is.EqualTo(ApplyResearchCostMultiplier(baselineQuote.ResearchCost, effects.ResearchCostMultiplier)).Within(0.01d));
        Assert.That(modifiedQuote.ResearchTimeMonths, Is.LessThanOrEqualTo(baselineQuote.ResearchTimeMonths));
        Assert.That(modifiedQuote.ResearchCost, Is.GreaterThan(baselineQuote.ResearchCost));
    }


    [Test]
    [Category("Extended")]
    public void DeltaWing_HighSpeedDragModifier_IncreasesTopSpeedAbove500KilometersPerHour()
    {
        var calculator = new AircraftPerformanceCalculator(new IsaAtmosphereModel(), new AerodynamicsCalculator());

        var engine = new Engine(
            new EngineDefinition
            {
                Id = "eng.delta.drag",
                DisplayName = "Delta Drag Jet",
                EngineType = EngineType.Turbojet,
            },
            new EngineParameters
            {
                BypassRatio = 0.2d,
                TurbineDiameterMeters = 1.8d,
                CompressionRatio = 16d,
                CoolingEfficiency = 0.82d,
                MaximumSpeed = 4000d,
            },
            new EnginePerformanceReport(
                EngineOutputKind.ThrustNewtons,
                new[]
                {
                    new EngineAltitudePerformance(0d, 160000d, 5100d),
                    new EngineAltitudePerformance(5000d, 120000d, 4300d),
                    new EngineAltitudePerformance(10000d, 92000d, 3600d),
                },
                reliability: 0.89d,
                weight: Mass.FromKilograms(2900d),
                cost: 12000000d,
                productionManDays: 140d,
                engineLengthMeters: 4.8d,
                engineFrontalAreaSquareMeters: 2.8d));

        var definition = new AirframeDefinition
        {
            Id = "air.delta.drag",
            DisplayName = "Delta Drag Test Frame",
            BaseStructuralWeightKilograms = 26000d,
            ReferenceWingAreaSquareMeters = 160d,
            BaseDragCoefficient = 0.029d,
            BaseLiftCoefficient = 0.84d,
            BaseReliabilityModifier = 0.98d,
            BaseCost = 90000000d,
            BaseProductionManDays = 700d,
        };

        var baselineParameters = new AirframeParameters
        {
            EngineCount = 2,
            StructureType = StructureType.Monocoque,
            MaterialType = MaterialType.Aluminum,
            WingPosition = WingPosition.Low,
            WingShape = WingShape.Straight,
            WingAreaSquareMeters = 160d,
            TailType = TailType.Classic,
            FlapType = FlapType.Fowler,
            LandingGearType = LandingGearType.Retractable,
            EngineCowling = 0.6d,
            FuelAmountLiters = 12000d,
            CrewCount = 2,
            RunwayType = RunwayType.Long,
        };

        var baselineAirframePerformance = new AirframePerformanceReport(
            TotalWeight: Mass.FromKilograms(42000d),
            DragCoefficient: 0.029d,
            LiftCoefficient: 0.88d,
            Maneuverability: 44d,
            ProductionCost: 115000000d,
            ProductionManDays: 980d,
            ReliabilityModifier: 0.96d,
            ReferenceLiftAtSeaLevel100Ms: Force.FromNewtons(820000d),
            ReferenceDragAtSeaLevel100Ms: Force.FromNewtons(38000d),
            AircraftLengthMeters: 28d,
            AirframeFrontalAreaSquareMeters: 9.2d,
            EffectiveDragAreaSquareMeters: 0.2668d,
            AerodynamicCleanlinessPercent: 10d);

        var straightParameters = baselineParameters.Clone();
        straightParameters.WingShape = WingShape.Straight;
        var deltaParameters = baselineParameters.Clone();
        deltaParameters.WingShape = WingShape.Delta;

        var straightAirframe = new Airframe(definition, straightParameters, baselineAirframePerformance);
        var deltaAirframe = new Airframe(definition, deltaParameters, baselineAirframePerformance);

        var straightPerformance = calculator.Calculate(engine, straightAirframe, AircraftCategory.MilitaryTactical);
        var deltaPerformance = calculator.Calculate(engine, deltaAirframe, AircraftCategory.MilitaryTactical);

        var straightTopSpeedMetersPerSecond = 0d;
        var deltaTopSpeedMetersPerSecond = 0d;
        for (var index = 0; index < straightPerformance.MaxSpeedByAltitude.Count; index++)
        {
            straightTopSpeedMetersPerSecond = System.Math.Max(
                straightTopSpeedMetersPerSecond,
                straightPerformance.MaxSpeedByAltitude[index].MaxSpeedMetersPerSecond);
            deltaTopSpeedMetersPerSecond = System.Math.Max(
                deltaTopSpeedMetersPerSecond,
                deltaPerformance.MaxSpeedByAltitude[index].MaxSpeedMetersPerSecond);
        }

        var straightTopSpeedKilometersPerHour = straightTopSpeedMetersPerSecond * 3.6d;
        var deltaTopSpeedKilometersPerHour = deltaTopSpeedMetersPerSecond * 3.6d;

        Assert.That(straightTopSpeedKilometersPerHour, Is.GreaterThan(500d));
        Assert.That(deltaTopSpeedKilometersPerHour, Is.GreaterThan(straightTopSpeedKilometersPerHour));
    }

    [Test]
    [Category("Extended")]
    public void Naca4DigitLightProfile_HighSpeedDragPenalty_ReducesTopSpeedAtHighSpeed()
    {
        var calculator = new AircraftPerformanceCalculator(new IsaAtmosphereModel(), new AerodynamicsCalculator());

        var engine = new Engine(
            new EngineDefinition
            {
                Id = "eng.naca.drag",
                DisplayName = "NACA Drag Jet",
                EngineType = EngineType.Turbojet,
            },
            new EngineParameters
            {
                BypassRatio = 0.2d,
                TurbineDiameterMeters = 1.8d,
                CompressionRatio = 16d,
                CoolingEfficiency = 0.82d,
                MaximumSpeed = 4000d,
            },
            new EnginePerformanceReport(
                EngineOutputKind.ThrustNewtons,
                new[]
                {
                    new EngineAltitudePerformance(0d, 160000d, 5100d),
                    new EngineAltitudePerformance(5000d, 120000d, 4300d),
                    new EngineAltitudePerformance(10000d, 92000d, 3600d),
                },
                reliability: 0.89d,
                weight: Mass.FromKilograms(2900d),
                cost: 12000000d,
                productionManDays: 140d,
                engineLengthMeters: 4.8d,
                engineFrontalAreaSquareMeters: 2.8d));

        var definition = new AirframeDefinition
        {
            Id = "air.naca.drag",
            DisplayName = "NACA Drag Test Frame",
            BaseStructuralWeightKilograms = 26000d,
            ReferenceWingAreaSquareMeters = 160d,
            BaseDragCoefficient = 0.029d,
            BaseLiftCoefficient = 0.84d,
            BaseReliabilityModifier = 0.98d,
            BaseCost = 90000000d,
            BaseProductionManDays = 700d,
        };

        var baselineParameters = new AirframeParameters
        {
            EngineCount = 2,
            StructureType = StructureType.Monocoque,
            MaterialType = MaterialType.Aluminum,
            WingPosition = WingPosition.Low,
            WingShape = WingShape.Straight,
            WingProfile = WingProfileType.Standard,
            WingAreaSquareMeters = 160d,
            TailType = TailType.Classic,
            FlapType = FlapType.Fowler,
            LandingGearType = LandingGearType.Retractable,
            EngineCowling = 0.6d,
            FuelAmountLiters = 12000d,
            CrewCount = 2,
            RunwayType = RunwayType.Long,
        };

        var baselineAirframePerformance = new AirframePerformanceReport(
            TotalWeight: Mass.FromKilograms(42000d),
            DragCoefficient: 0.029d,
            LiftCoefficient: 0.88d,
            Maneuverability: 44d,
            ProductionCost: 115000000d,
            ProductionManDays: 980d,
            ReliabilityModifier: 0.96d,
            ReferenceLiftAtSeaLevel100Ms: Force.FromNewtons(820000d),
            ReferenceDragAtSeaLevel100Ms: Force.FromNewtons(38000d),
            AircraftLengthMeters: 28d,
            AirframeFrontalAreaSquareMeters: 9.2d,
            EffectiveDragAreaSquareMeters: 0.2668d,
            AerodynamicCleanlinessPercent: 10d);

        var standardParameters = baselineParameters.Clone();
        standardParameters.WingProfile = WingProfileType.Standard;
        var nacaParameters = baselineParameters.Clone();
        nacaParameters.WingProfile = WingProfileType.Naca4DigitLight;

        var standardAirframe = new Airframe(definition, standardParameters, baselineAirframePerformance);
        var nacaAirframe = new Airframe(definition, nacaParameters, baselineAirframePerformance);

        var standardPerformance = calculator.Calculate(engine, standardAirframe, AircraftCategory.MilitaryTactical);
        var nacaPerformance = calculator.Calculate(engine, nacaAirframe, AircraftCategory.MilitaryTactical);

        var standardTopSpeedMetersPerSecond = 0d;
        var nacaTopSpeedMetersPerSecond = 0d;
        for (var index = 0; index < standardPerformance.MaxSpeedByAltitude.Count; index++)
        {
            standardTopSpeedMetersPerSecond = System.Math.Max(
                standardTopSpeedMetersPerSecond,
                standardPerformance.MaxSpeedByAltitude[index].MaxSpeedMetersPerSecond);
            nacaTopSpeedMetersPerSecond = System.Math.Max(
                nacaTopSpeedMetersPerSecond,
                nacaPerformance.MaxSpeedByAltitude[index].MaxSpeedMetersPerSecond);
        }

        var standardTopSpeedKilometersPerHour = standardTopSpeedMetersPerSecond * 3.6d;
        var nacaTopSpeedKilometersPerHour = nacaTopSpeedMetersPerSecond * 3.6d;

        Assert.That(standardTopSpeedKilometersPerHour, Is.GreaterThan(1000d));
        Assert.That(nacaTopSpeedKilometersPerHour, Is.LessThan(standardTopSpeedKilometersPerHour * 0.9d));
    }

    private static int ComputeExpectedTypeResearchMonths(WrightTycoon.Core.Aircraft.Aircraft aircraft, TypeResearchModifierEffects modifierEffects)
    {
        var complexity = ComputeTypeResearchComplexity(aircraft);
        var baseMonths = (int)System.Math.Round(
            Lerp(ResearchBalance.Type.TimeMonthsMin, ResearchBalance.Type.TimeMonthsMax, complexity),
            MidpointRounding.AwayFromZero);

        var expectedMonths = (int)System.Math.Round(
            baseMonths * modifierEffects.ResearchTimeMultiplier,
            MidpointRounding.AwayFromZero);
        return System.Math.Max(1, expectedMonths);
    }

    private static double ComputeExpectedTypeResearchCost(WrightTycoon.Core.Aircraft.Aircraft aircraft, TypeResearchModifierEffects modifierEffects)
    {
        var complexity = ComputeTypeResearchComplexity(aircraft);
        var baseMultiplier = Lerp(ResearchBalance.Type.CostMultiplierMin, ResearchBalance.Type.CostMultiplierMax, complexity);
        var baseCost = System.Math.Round(
            aircraft.Performance.CompleteProductionCost * baseMultiplier,
            2,
            MidpointRounding.AwayFromZero);
        return System.Math.Round(baseCost * modifierEffects.ResearchCostMultiplier, 2, MidpointRounding.AwayFromZero);
    }

    private static int ApplyResearchTimeMultiplier(int baseMonths, double multiplier)
    {
        var expectedMonths = (int)System.Math.Round(baseMonths * multiplier, MidpointRounding.AwayFromZero);
        return System.Math.Max(1, expectedMonths);
    }

    private static double ApplyResearchCostMultiplier(double baseCost, double multiplier) =>
        System.Math.Round(baseCost * multiplier, 2, MidpointRounding.AwayFromZero);

    private static double ComputeTypeResearchComplexity(WrightTycoon.Core.Aircraft.Aircraft aircraft)
    {
        var maxSpeedKmh = 0d;
        if (aircraft.Performance.MaxSpeedByAltitude.Count > 0)
        {
            maxSpeedKmh = aircraft.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond * 3.6d;
        }

        var magnitudeNormalized = Normalize(
            aircraft.Development.ChangeMagnitudeScore,
            ResearchBalance.Type.ChangeMagnitudeMin,
            ResearchBalance.Type.ChangeMagnitudeMax);
        var changedCountNormalized = Normalize(
            aircraft.Development.ChangedParameterCount,
            ResearchBalance.Type.ChangedCountMin,
            ResearchBalance.Type.ChangedCountMax);
        var weightNormalized = Normalize(
            aircraft.Performance.TotalWeight.Kilograms,
            ResearchBalance.Type.WeightMinKilograms,
            ResearchBalance.Type.WeightMaxKilograms);
        var speedNormalized = Normalize(
            maxSpeedKmh,
            ResearchBalance.Type.SpeedMinKilometersPerHour,
            ResearchBalance.Type.SpeedMaxKilometersPerHour);

        var complexity = (magnitudeNormalized * ResearchBalance.Type.ComplexityWeightMagnitude)
            + (changedCountNormalized * ResearchBalance.Type.ComplexityWeightChangedCount)
            + (weightNormalized * ResearchBalance.Type.ComplexityWeightWeight)
            + (speedNormalized * ResearchBalance.Type.ComplexityWeightSpeed);
        return Clamp(complexity, 0d, 1d);
    }

    private static double Normalize(double value, double minValue, double maxValue)
    {
        if (maxValue - minValue <= 1e-9d)
        {
            return 0d;
        }

        return Clamp((value - minValue) / (maxValue - minValue), 0d, 1d);
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private static double Clamp(double value, double minValue, double maxValue)
    {
        if (value < minValue)
        {
            return minValue;
        }

        if (value > maxValue)
        {
            return maxValue;
        }

        return value;
    }

    private static double EvaluateSpeedWeightFactor(double speedKilometersPerHour)
    {
        var method = typeof(AircraftPerformanceCalculator).GetMethod(
            "CalculateSpeedWeightFactor",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "Expected private speed-weight evaluator method to exist.");

        var result = method!.Invoke(null, new object[] { speedKilometersPerHour });
        Assert.That(result, Is.TypeOf<double>());
        return (double)result!;
    }

    private static AircraftFactory CreateFactory(IHistoricalCampaignTechnologyModifierSource? technologySource = null)
    {
        var engineRepo = new EngineDefinitionRepository(new[]
        {
            new EngineDefinition
            {
                Id = "eng.radial.main",
                DisplayName = "Mainline Radial",
                EngineType = EngineType.Radial,
                BaseVolumetricEfficiency = 0.89d,
                BasePowerPerLiterKilowatts = 34d,
                PropFuelConsumptionPerKilowattHour = 0.25d,
                BaseReliability = 0.9d,
                BaseWeightKilograms = 285d,
                WeightPerDisplacementLiter = 12.2d,
                BaseCost = 38000d,
                CostPerOutputUnit = 112d,
                BaseProductionManDays = 70d,
                ComplexityFactor = 1.05d,
            },
        });

        var airframeRepo = new AirframeDefinitionRepository(new[]
        {
            new AirframeDefinition
            {
                Id = "air.medium.main",
                DisplayName = "Mainline Medium Frame",
                BaseStructuralWeightKilograms = 1250d,
                ReferenceWingAreaSquareMeters = 36d,
                BaseDragCoefficient = 0.032d,
                BaseLiftCoefficient = 1.08d,
                BaseReliabilityModifier = 1d,
                BaseCost = 68000d,
                BaseProductionManDays = 150d,
            },
        });

        var atmosphere = new IsaAtmosphereModel();
        var aero = new AerodynamicsCalculator();
        var engineBuilder = new EngineBuilder(new EngineCalculator(), atmosphere, technologySource);
        var airframeBuilder = new AirframeBuilder(new AirframeCalculator(aero, atmosphere), technologySource);
        var performanceCalculator = new AircraftPerformanceCalculator(atmosphere, aero);
        var developmentCalculator = new DevelopmentCostCalculator();

        return new AircraftFactory(
            engineBuilder,
            airframeBuilder,
            performanceCalculator,
            developmentCalculator,
            engineRepo,
            airframeRepo);
    }

    private static AircraftFamilyDefinition CreateFamily(
        string id,
        int engineCount,
        AircraftCategory category = AircraftCategory.LightSportAndRecreational,
        AircraftAllowedEngineFamily allowedEngineFamily = AircraftAllowedEngineFamily.Any,
        AircraftEngineClassification engineClassification = AircraftEngineClassification.Any) =>
        new()
        {
            Id = id,
            DisplayName = $"{id}-display",
            EngineDefinitionId = "eng.radial.main",
            AirframeDefinitionId = "air.medium.main",
            Category = category,
            LockedEngineType = EngineType.Radial,
            AllowedEngineFamily = allowedEngineFamily,
            EngineClassification = engineClassification,
            LockedStructureType = StructureType.Monocoque,
            LockedMaterialType = MaterialType.Aluminum,
            LockedEngineCount = engineCount,
            BaseDevelopmentCost = 92000d,
            BaseDevelopmentManDays = 245d,
            BaseEngineParameters = new EngineParameters
            {
                DisplacementLiters = 17d,
                CompressionRatio = 8.4d,
                BoostType = BoostType.None,
                OptimizationAltitudeMeters = 1800d,
                CoolingEfficiency = 0.76d,
            },
            BaseAirframeParameters = new AirframeParameters
            {
                EngineCount = engineCount,
                StructureType = StructureType.Monocoque,
                MaterialType = MaterialType.Aluminum,
                WingPosition = WingPosition.Low,
                WingShape = WingShape.Straight,
                WingAreaSquareMeters = 36d,
                TailType = TailType.Classic,
                FlapType = FlapType.Fowler,
                LandingGearType = LandingGearType.SemiRetractable,
                EngineCowling = 0.55d,
                FuelAmountLiters = 1500d,
                CrewCount = 2,
            },
        };
}




