using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Balance;

public static class PrecisionEngineeringBalance
{
    public const int MinimumLevel = 0;
    public const int MaximumLevel = 10;
    public const double FirstBenefitPerLevel = 0.02d;
    public const double LastBenefitPerLevel = 0.01d;
    public const double FirstProductionCostPenaltyPerLevel = 0.02d;
    public const double LastProductionCostPenaltyPerLevel = 0.04d;
    public const double FirstProductionManDaysPenaltyPerLevel = 0.03d;
    public const double LastProductionManDaysPenaltyPerLevel = 0.06d;
    public const double FirstReliabilityPenaltyPerLevel = 0.01d;
    public const double LastReliabilityPenaltyPerLevel = 0.02d;
    public const double DevelopmentPenaltyScale = 2d;

    public static PrecisionEngineeringEffects GetEffects(int level)
    {
        var clampedLevel = System.Math.Clamp(level, MinimumLevel, MaximumLevel);
        var benefit = SumProgressiveRates(clampedLevel, FirstBenefitPerLevel, LastBenefitPerLevel);
        var costPenalty = SumProgressiveRates(
            clampedLevel,
            FirstProductionCostPenaltyPerLevel,
            LastProductionCostPenaltyPerLevel);
        var manDaysPenalty = SumProgressiveRates(
            clampedLevel,
            FirstProductionManDaysPenaltyPerLevel,
            LastProductionManDaysPenaltyPerLevel);
        var reliabilityPenalty = SumProgressiveRates(
            clampedLevel,
            FirstReliabilityPenaltyPerLevel,
            LastReliabilityPenaltyPerLevel);

        return new PrecisionEngineeringEffects(
            clampedLevel,
            1d + benefit,
            System.Math.Max(0.1d, 1d - benefit),
            System.Math.Max(0.1d, 1d - benefit),
            1d + costPenalty,
            1d + manDaysPenalty,
            System.Math.Max(0.1d, 1d - reliabilityPenalty),
            1d + (costPenalty * DevelopmentPenaltyScale),
            1d + (costPenalty * DevelopmentPenaltyScale));
    }

    private static double SumProgressiveRates(int level, double firstRate, double lastRate)
    {
        if (level <= 0)
        {
            return 0d;
        }

        var total = 0d;
        for (var point = 1; point <= level; point++)
        {
            var blend = (double)(point - 1) / (MaximumLevel - 1);
            total += firstRate + ((lastRate - firstRate) * blend);
        }

        return Numeric.Clamp(total, 0d, 0.9d);
    }
}

public readonly record struct PrecisionEngineeringEffects(
    int Level,
    double OutputMultiplier,
    double WeightMultiplier,
    double DragMultiplier,
    double ProductionCostMultiplier,
    double ProductionManDaysMultiplier,
    double ReliabilityMultiplier,
    double DevelopmentCostMultiplier,
    double DevelopmentTimeMultiplier);
