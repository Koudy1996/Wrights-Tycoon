using System;
using NUnit.Framework;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Physics;

namespace WrightTycoon.Core.Tests.Engines;

[TestFixture]
public sealed class EngineLifecycleDesignTests
{
    private static EngineParameters Parameters(int interval = 500, int durability = 0) => new()
    {
        DisplacementLiters = 12, CylinderCount = 12, CompressionRatio = 8,
        OptimizationAltitudeMeters = 1500, MaximumSpeed = 2500, CoolingEfficiency = .8,
        ServiceIntervalHours = interval, DurabilityLevel = durability,
    };

    private static Engine Build(int interval = 500, int durability = 0, HistoricalCampaignTechnologyModifiers? technology = null) =>
        new EngineBuilder(new EngineCalculator(), new IsaAtmosphereModel()).Build(new EngineDefinition
        { Id = "engine.lifecycle", DisplayName = "Lifecycle", EngineType = EngineType.Radial, BaseReliability = .9 },
            Parameters(interval, durability), technology);

    [Test]
    public void CoolingBonusCanRaiseEngineReliabilityAboveOneHundredPercent()
    {
        var parameters = Parameters();
        parameters.CylinderCount = 6;
        parameters.CompressionRatio = 5d;
        parameters.CoolingEfficiency = 1d;
        var engine = new EngineBuilder(new EngineCalculator(), new IsaAtmosphereModel()).Build(new EngineDefinition
        { Id = "engine.overflow", DisplayName = "Overflow", EngineType = EngineType.Inline, BaseReliability = 1d, ComplexityFactor = 0d }, parameters);
        Assert.That(engine.Performance.Reliability, Is.GreaterThan(1d));
        var terms = EngineMaintenancePolicy.Create(engine, "engine.overflow", "parts.overflow");
        Assert.That(terms.Reliability, Is.EqualTo(engine.Performance.Reliability));
        Assert.That(terms.PartsPerService, Is.EqualTo(1));
        Assert.DoesNotThrow(terms.Validate);
    }

    [Test]
    public void DurabilityImprovesLifeWithExplicitMassMaterialsAndWorkPenalties()
    {
        var baseline = Build();
        var durable = Build(durability: 10);
        var baselineTerms = EngineMaintenancePolicy.Create(baseline, "base", "parts");
        var durableTerms = EngineMaintenancePolicy.Create(durable, "durable", "parts");
        Assert.That(durableTerms.ReplacementLifeHours, Is.GreaterThan(baselineTerms.ReplacementLifeHours));
        Assert.That(durable.Performance.Weight.Kilograms / baseline.Performance.Weight.Kilograms, Is.EqualTo(1.2).Within(1e-10));
        Assert.That(durable.Performance.Cost / baseline.Performance.Cost, Is.EqualTo(1.35).Within(1e-10));
        Assert.That(durable.Performance.ProductionManDays / baseline.Performance.ProductionManDays, Is.EqualTo(1.35).Within(1e-10));
        Assert.That(durable.Performance.CostBreakdown.Total, Is.EqualTo(durable.Performance.Cost).Within(1e-9));
        Assert.That(durable.Performance.ProductionManDaysBreakdown.Total, Is.EqualTo(durable.Performance.ProductionManDays).Within(1e-9));
        Assert.That(durable.Performance.EngineFrontalAreaSquareMeters, Is.EqualTo(baseline.Performance.EngineFrontalAreaSquareMeters));
        Assert.That(durable.Performance.GetPerformanceAtAltitude(0).Output, Is.EqualTo(baseline.Performance.GetPerformanceAtAltitude(0).Output));
        var previous = baselineTerms.ReplacementLifeHours;
        for (var level = 1; level <= 10; level++)
        {
            var life = EngineMaintenancePolicy.Create(Build(durability: level), "engine", "parts").ReplacementLifeHours;
            Assert.That(life, Is.GreaterThanOrEqualTo(previous)); previous = life;
        }
    }

    [Test]
    public void LongerServiceIntervalsLowerLifeButKeepTheSamePartsPerService()
    {
        var baseline = EngineMaintenancePolicy.Create(Build(), "engine", "parts");
        Assert.That(baseline.ServiceIntervalHours, Is.EqualTo(500));
        Assert.That(baseline.ReplacementLifeHours, Is.InRange(2000, 4000));
        var previousLife = int.MaxValue;
        foreach (var interval in EngineMaintenancePolicy.ServiceIntervalsHours)
        {
            var offer = EngineMaintenancePolicy.Create(Build(interval), "engine", "parts");
            Assert.That(offer.ServiceIntervalHours, Is.EqualTo(interval));
            Assert.That(offer.PartsPerService, Is.EqualTo(baseline.PartsPerService));
            Assert.That(offer.PartsPerService, Is.EqualTo(FleetServiceBalance.RequiredSpareParts(offer.Reliability)));
            Assert.That(offer.PartUnitPrice, Is.EqualTo(baseline.PartUnitPrice));
            Assert.That(offer.ReplacementLifeHours, Is.LessThanOrEqualTo(previousLife));
            Assert.That(offer.ReplacementLifeHours, Is.GreaterThanOrEqualTo(interval));
            previousLife = offer.ReplacementLifeHours;
        }
        var medium = EngineMaintenancePolicy.Create(Build(1000), "engine", "parts");
        var longInterval = EngineMaintenancePolicy.Create(Build(2000), "engine", "parts");
        Assert.That(medium.ReplacementLifeHours, Is.LessThan(baseline.ReplacementLifeHours));
        Assert.That(longInterval.PartsPerService, Is.EqualTo(baseline.PartsPerService));
        Assert.That((double)medium.PartsPerService / medium.ServiceIntervalHours,
            Is.LessThan((double)baseline.PartsPerService / baseline.ServiceIntervalHours));
    }

    [Test]
    public void IncreasedTechnologyOutputRaisesStressWithoutPretendingItImprovesEngineLife()
    {
        var baseline = Build();
        var higherOutput = Build(technology: new HistoricalCampaignTechnologyModifiers(2, 1, 1, 1));
        Assert.That(higherOutput.Performance.Reliability, Is.EqualTo(baseline.Performance.Reliability));
        Assert.That(EngineMaintenancePolicy.CalculatePerformanceStress(higherOutput),
            Is.GreaterThan(EngineMaintenancePolicy.CalculatePerformanceStress(baseline)));
        Assert.That(EngineMaintenancePolicy.Create(higherOutput, "high", "parts").ReplacementLifeHours,
            Is.LessThan(EngineMaintenancePolicy.Create(baseline, "base", "parts").ReplacementLifeHours));
    }

    [Test]
    [Category("Extended")]
    public void ParametersCloneNewSelectionsAndKeepCompatibleDefaults()
    {
        var defaults = new EngineParameters();
        Assert.That(defaults.ServiceIntervalHours, Is.EqualTo(500));
        Assert.That(defaults.DurabilityLevel, Is.Zero);
        var original = Parameters(2000, 7);
        var copy = original.Clone();
        Assert.That(copy.ServiceIntervalHours, Is.EqualTo(2000));
        Assert.That(copy.DurabilityLevel, Is.EqualTo(7));
        copy.DurabilityLevel = 1; copy.ServiceIntervalHours = 200;
        Assert.That(original.DurabilityLevel, Is.EqualTo(7));
        Assert.That(original.ServiceIntervalHours, Is.EqualTo(2000));
    }

    [TestCase(200)]
    [TestCase(500)]
    [TestCase(1000)]
    [TestCase(2000)]
    [Category("Extended")]
    public void NewSelectableServiceIntervalsAreValid(int interval)
    {
        Assert.DoesNotThrow(() => Parameters(interval).Validate());
    }

    [TestCase(0)]
    [TestCase(300)]
    [TestCase(4000)]
    [TestCase(8000)]
    [Category("Extended")]
    public void NewSelectionsRejectUnsupportedIntervals(int interval)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Parameters(interval).Validate());
    }

    [Test]
    [Category("Extended")]
    public void FrozenLegacyIntervalsAndNonMultipleLifeRemainReadableWhileNewPartsAreBounded()
    {
        var legacy = new EngineMaintenanceSpecification("engine", "parts", .8, 4000, 8000, 2, 100, 10000);
        Assert.DoesNotThrow(legacy.Validate);
        Assert.DoesNotThrow(() => (legacy with { ServiceIntervalHours = 500, ReplacementLifeHours = 2300, PartsPerService = 72 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (legacy with { PartsPerService = 257 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (legacy with { ReplacementLifeHours = 3999 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => Parameters(durability: -1).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => Parameters(durability: 11).Validate());
    }
}
