using System;
using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Data;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Research;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class AircraftIntegrationTests
{
    [Test]
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
    }
    [Test]
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
    }

    [Test]
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
                WingAreaSquareMetersDelta = 12d,
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

        Assert.That(aggressiveAircraft.Development.DevelopmentCost, Is.GreaterThan(mildAircraft.Development.DevelopmentCost));
        Assert.That(aggressiveAircraft.Development.DevelopmentManDays, Is.GreaterThan(mildAircraft.Development.DevelopmentManDays));
        Assert.That(aggressiveAircraft.Development.ChangedParameterCount, Is.GreaterThan(mildAircraft.Development.ChangedParameterCount));
    }

    [Test]
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
            Is.GreaterThan(15d));

        Assert.That(
            boostedAircraft.Performance.OverallReliability - baselineAircraft.Performance.OverallReliability,
            Is.GreaterThan(0.08d));

        Assert.That(
            boostedAircraft.Performance.RangeKilometers,
            Is.GreaterThan(baselineAircraft.Performance.RangeKilometers));
    }

    [Test]
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
            (aircraft.Performance.EmptyAircraftWeightKilograms * WrightTycoonTuning.AircraftPerformance.AircraftWeightWithFuelSafetyMultiplier)
            + fuelMassKilograms;

        Assert.That(
            aircraft.Performance.AircraftWeightWithFuelKilograms,
            Is.EqualTo(expectedAircraftWeightWithFuelKilograms).Within(0.05d));
    }

    [Test]
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

        var expectedReliability = System.Math.Min(0.995d, baselineAircraft.Performance.OverallReliability + effects.ReliabilityDelta);
        var expectedProductionCost = baselineAircraft.Performance.TotalProductionCost * effects.ProductionCostMultiplier;
        var expectedProductionManDays = baselineAircraft.Performance.TotalProductionManDays * effects.ProductionManDaysMultiplier;

        Assert.That(optimizedAircraft.Performance.OverallReliability, Is.EqualTo(expectedReliability).Within(1e-9));
        Assert.That(optimizedAircraft.Performance.TotalProductionCost, Is.EqualTo(expectedProductionCost).Within(0.01d));
        Assert.That(optimizedAircraft.Performance.TotalProductionManDays, Is.EqualTo(expectedProductionManDays).Within(0.01d));
    }

    [Test]
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
            aircraft.Performance.TotalProductionCost * baseMultiplier,
            2,
            MidpointRounding.AwayFromZero);
        return System.Math.Round(baseCost * modifierEffects.ResearchCostMultiplier, 2, MidpointRounding.AwayFromZero);
    }

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

    private static AircraftFactory CreateFactory()
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
        var engineBuilder = new EngineBuilder(new EngineCalculator(), atmosphere);
        var airframeBuilder = new AirframeBuilder(new AirframeCalculator(aero, atmosphere));
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
        AircraftCategory category = AircraftCategory.LightSportAndRecreational) =>
        new()
        {
            Id = id,
            DisplayName = $"{id}-display",
            EngineDefinitionId = "eng.radial.main",
            AirframeDefinitionId = "air.medium.main",
            Category = category,
            LockedEngineType = EngineType.Radial,
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
