using System;
using System.Collections.Generic;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Core.Fleet;

/// <summary>Frozen terms for one exact installed engine version; a shared parts group never implies interchangeable engines.</summary>
public sealed record EngineMaintenanceSpecification(string EngineDesignId, string PartsGroupId, double Reliability,
    int ServiceIntervalHours, int ReplacementLifeHours, int PartsPerService, double PartUnitPrice, double ReplacementUnitPrice)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EngineDesignId) || string.IsNullOrWhiteSpace(PartsGroupId))
            throw new ArgumentException("Engine and compatible-parts group identities are required.");
        LifecycleCalculator.ValidateReliability(Reliability);
        LifecycleCalculator.ValidateServiceInterval(ServiceIntervalHours);
        if (ReplacementLifeHours < ServiceIntervalHours)
            throw new ArgumentOutOfRangeException(nameof(ReplacementLifeHours));
        if (PartsPerService < 1 || PartsPerService > EngineMaintenancePolicy.MaximumPartsPerService) throw new ArgumentOutOfRangeException(nameof(PartsPerService));
        LifecycleCalculator.RequireFiniteNonNegative(PartUnitPrice, nameof(PartUnitPrice));
        LifecycleCalculator.RequireFiniteNonNegative(ReplacementUnitPrice, nameof(ReplacementUnitPrice));
    }
}

/// <summary>Game balance for routine parts and whole-engine replacement. No overhaul/repair-core loop.</summary>
public static class EngineMaintenancePolicy
{
    public const int DefaultServiceIntervalHours = 500;
    public const int ReferenceReplacementLifeHours = 4000;
    public const int ReplacementLifeStepHours = 500;
    public const double MinimumReliabilityLifetimeFactor = .5d;
    public const double ReliabilityLifetimeFactor = .5d;
    public const double EnginePartProductionCostFraction = .01d;
    public const double EnginePartContributionFraction = .5d;
    public const double ReplacementPriceMultiplier = 2d;
    public const double DurabilityMassPerLevel = .02d;
    public const double DurabilityProductionPerLevel = .035d;
    public const double DurabilityLifetimePerLevel = .08d;
    public const int MaximumPartsPerService = 256;
    public const int MinimumReplacementLifeHours = 2000;
    public const int LifetimeRoundingStepHours = 100;
    public const double IntervalLifetimeExponent = .2d;
    public static IReadOnlyList<int> ServiceIntervalsHours { get; } = Array.AsReadOnly(new[] { 200, 500, 1000, 2000 });

    public static void ValidateSelectableServiceInterval(int intervalHours)
    {
        foreach (var interval in ServiceIntervalsHours)
            if (intervalHours == interval) return;
        throw new ArgumentOutOfRangeException(nameof(intervalHours));
    }

    public static EngineMaintenanceSpecification Create(Engine engine, string designId, string groupId, Engine? referenceEngine = null)
    {
        if (engine == null) throw new ArgumentNullException(nameof(engine));
        engine.Parameters.Validate();
        var reliability = engine.Performance.Reliability;
        LifecycleCalculator.ValidateReliability(reliability);
        var completeCost = engine.Performance.CompleteProductionCost;
        LifecycleCalculator.RequireFiniteNonNegative(completeCost, nameof(engine));
        var interval = engine.Parameters.ServiceIntervalHours;
        var serviceFactor = System.Math.Max(.65d, System.Math.Min(1.2d,
            System.Math.Pow((double)DefaultServiceIntervalHours / interval, IntervalLifetimeExponent)));
        var stress = CalculatePerformanceStress(engine);
        var rawLife = ReferenceReplacementLifeHours * (MinimumReliabilityLifetimeFactor + ReliabilityLifetimeFactor * reliability)
            * serviceFactor * (1d + engine.Parameters.DurabilityLevel * DurabilityLifetimePerLevel) / stress;
        var life = System.Math.Max(MinimumReplacementLifeHours, (int)(System.Math.Round(
            rawLife / LifetimeRoundingStepHours, MidpointRounding.AwayFromZero) * LifetimeRoundingStepHours));
        // The schedule changes service frequency, not the parts consumed at each routine service.
        var quantity = FleetServiceBalance.RequiredSpareParts(reliability);
        var result = new EngineMaintenanceSpecification(designId, groupId, reliability, interval,
            life, quantity,
            FleetServiceBalance.EnginePartUnitPrice(referenceEngine ?? engine),
            FleetServiceBalance.ReplacementEngineUnitPrice(referenceEngine ?? engine));
        result.Validate();
        return result;
    }

    /// <summary>Output above the definition's reference loading includes historical technology/precision gains.</summary>
    public static double CalculatePerformanceStress(Engine engine)
    {
        if (engine is null) throw new ArgumentNullException(nameof(engine));
        var p = engine.Parameters;
        var jet = engine.Definition.EngineType is EngineType.Turbojet or EngineType.Turbofan;
        var referenceOutput = jet
            ? engine.Definition.BaseJetThrustPerSquareMeterNewtons * p.TurbineDiameterMeters * p.TurbineDiameterMeters
            : engine.Definition.BasePowerPerLiterKilowatts * p.DisplacementLiters;
        var output = engine.Performance.GetPerformanceAtAltitude(0d).Output;
        LifecycleCalculator.RequireFiniteNonNegative(output, nameof(engine));
        var outputExcess = referenceOutput > 0d ? System.Math.Max(0d, output / referenceOutput - 1d) : 0d;
        var rpmExcess = System.Math.Max(0d, p.MaximumSpeed / (jet ? 50000d : 2500d) - 1d);
        var compressionExcess = System.Math.Max(0d, p.CompressionRatio / (jet ? 12d : 8d) - 1d);
        return System.Math.Min(2d, 1d + .1d * outputExcess + .2d * rpmExcess + .15d * compressionExcess
            + (jet && p.HasAfterburner ? .25d : 0d));
    }
}
