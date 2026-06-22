using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Balance;

public static class PrecisionEngineeringBalance
{
    public const int MinimumLevel = 0;
    public const int MaximumLevel = 10;
    public const double FirstBenefitPerLevel = 0.02d;
    public const double LastBenefitPerLevel = 0.01d;
    public const double FirstPenaltyPerLevel = 0.01d;
    public const double LastPenaltyPerLevel = 0.02d;
    public const double DevelopmentPenaltyScale = 2d;

    public static PrecisionEngineeringEffects GetEffects(int level)
    {
        var clampedLevel = System.Math.Clamp(level, MinimumLevel, MaximumLevel);
        var benefit = SumProgressiveRates(clampedLevel, FirstBenefitPerLevel, LastBenefitPerLevel);
        var penalty = SumProgressiveRates(clampedLevel, FirstPenaltyPerLevel, LastPenaltyPerLevel);

        return new PrecisionEngineeringEffects(
            clampedLevel,
            1d + benefit,
            System.Math.Max(0.1d, 1d - benefit),
            System.Math.Max(0.1d, 1d - benefit),
            1d + penalty,
            1d + penalty,
            System.Math.Max(0.1d, 1d - penalty),
            1d + (penalty * DevelopmentPenaltyScale),
            1d + (penalty * DevelopmentPenaltyScale));
    }

    private static double SumProgressiveRates(int level, double firstRate, double lastRate)
    {
        if (level <= 0)
        {
            return 0d;
        }

        if (MaximumLevel <= 1)
        {
            return firstRate;
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
