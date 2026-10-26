using System;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Core.Contracts;

public sealed record EngineOwnershipCost(double RoutinePartsCost, double ReplacementCost, int RoutineCycles, int Replacements)
{
    public double TotalCost => RoutinePartsCost + ReplacementCost;
}

public static class EngineOwnershipCostCalculator
{
    public static EngineOwnershipCost Calculate(double hours, EngineMaintenanceSpecification engine, int engineCount)
    {
        LifecycleCalculator.RequireFiniteNonNegative(hours, nameof(hours));
        if (engine is null) throw new ArgumentNullException(nameof(engine));
        engine.Validate();
        if (engineCount < 1 || engineCount > 6) throw new ArgumentOutOfRangeException(nameof(engineCount));
        var replacements = System.Math.Max(0d, System.Math.Ceiling(hours / engine.ReplacementLifeHours) - 1d);
        var fullLifeCycles = System.Math.Max(0d, System.Math.Ceiling((double)engine.ReplacementLifeHours / engine.ServiceIntervalHours) - 1d);
        var remainder = hours - replacements * engine.ReplacementLifeHours;
        var lastCycles = System.Math.Max(0d, System.Math.Ceiling(remainder / engine.ServiceIntervalHours) - 1d);
        var cycles = replacements * fullLifeCycles + lastCycles;
        if (cycles > int.MaxValue || replacements > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(hours));
        var routineCost = cycles * engineCount * engine.PartsPerService * engine.PartUnitPrice;
        var replacementCost = replacements * engineCount * engine.ReplacementUnitPrice;
        LifecycleCalculator.RequireFiniteNonNegative(routineCost + replacementCost, nameof(hours));
        return new EngineOwnershipCost(routineCost, replacementCost, (int)cycles, (int)replacements);
    }
}
