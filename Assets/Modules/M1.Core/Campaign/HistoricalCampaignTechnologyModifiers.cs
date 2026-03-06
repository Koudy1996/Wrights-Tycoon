using WrightTycoon.Core.Balance;

namespace WrightTycoon.Core.Campaign;

public readonly record struct HistoricalCampaignTechnologyModifiers
{
    public HistoricalCampaignTechnologyModifiers(
        double enginePerformanceMultiplier,
        double airframeEfficiencyMultiplier,
        double aerodynamicsEfficiencyMultiplier,
        double fuelEfficiencyMultiplier)
    {
        EnginePerformanceMultiplier = ClampMultiplier(enginePerformanceMultiplier);
        AirframeEfficiencyMultiplier = ClampMultiplier(airframeEfficiencyMultiplier);
        AerodynamicsEfficiencyMultiplier = ClampMultiplier(aerodynamicsEfficiencyMultiplier);
        FuelEfficiencyMultiplier = ClampMultiplier(fuelEfficiencyMultiplier);
    }

    public double EnginePerformanceMultiplier { get; }

    public double AirframeEfficiencyMultiplier { get; }

    public double AerodynamicsEfficiencyMultiplier { get; }

    public double FuelEfficiencyMultiplier { get; }

    public static HistoricalCampaignTechnologyModifiers Neutral =>
        new(
            CoreBalance.HistoricalCampaignTechnology.NeutralMultiplier,
            CoreBalance.HistoricalCampaignTechnology.NeutralMultiplier,
            CoreBalance.HistoricalCampaignTechnology.NeutralMultiplier,
            CoreBalance.HistoricalCampaignTechnology.NeutralMultiplier);

    public static HistoricalCampaignTechnologyModifiers Sanitize(HistoricalCampaignTechnologyModifiers? rawModifiers)
    {
        if (!rawModifiers.HasValue)
        {
            return Neutral;
        }

        var modifiers = rawModifiers.Value;
        return new HistoricalCampaignTechnologyModifiers(
            modifiers.EnginePerformanceMultiplier,
            modifiers.AirframeEfficiencyMultiplier,
            modifiers.AerodynamicsEfficiencyMultiplier,
            modifiers.FuelEfficiencyMultiplier);
    }

    private static double ClampMultiplier(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return CoreBalance.HistoricalCampaignTechnology.NeutralMultiplier;
        }

        if (value < CoreBalance.HistoricalCampaignTechnology.MinimumMultiplier)
        {
            return CoreBalance.HistoricalCampaignTechnology.MinimumMultiplier;
        }

        if (value > CoreBalance.HistoricalCampaignTechnology.MaximumMultiplier)
        {
            return CoreBalance.HistoricalCampaignTechnology.MaximumMultiplier;
        }

        return value;
    }
}
