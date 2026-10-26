using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.Balance;

// M9 tuning surface for annual government contracts.
public static class GovernmentContractBalance
{
    public const int ContractAvailabilityMonths = 6;
    public const int ContractEvaluationDelayMonths = 6;
    public const double UpfrontFundingShare = 0.75d;

    public const double BaseEvaluationProbability = 0.5d;
    public const double FullBonusExcessRatio = 0.5d;
    public const double MaxBonusPerParameter = 0.08d;

    public const int DeliveryContractDurationYears = 10;
    public const int DeliveryBaseUnitsMin = 300;
    public const int DeliveryBaseUnitsMax = 600;
    public const double DeliveryBonusUnitsScale = 1000d;
    public const double DeliveryBonusUnitsMultiplier = 2d;
    public const double DeliveryCompletionBonusShare = 0.10d;

    public const double PricingMargin = 0.35d;
    public const double ContractPriceMarginImprovementMultiplier = 1.05d;
    public const double AdditionalMarginMultiplier = 1.10d;
    public const double TopThirdLowerBoundFactor = 0.66d;

    public const double SpeedAltitudeLowMeters = 0d;
    public const double SpeedAltitudeMediumMeters = 5000d;
    public const double SpeedAltitudeHighMeters = 10000d;

}
