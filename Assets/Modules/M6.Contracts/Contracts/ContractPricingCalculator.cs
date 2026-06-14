using System;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Contracts;

public sealed class ContractPricingCalculator
{
    private readonly IHistoricalCampaignContractModifierSource? _campaignModifierSource;

    public ContractPricingCalculator(IHistoricalCampaignContractModifierSource? campaignModifierSource = null)
    {
        _campaignModifierSource = campaignModifierSource;
    }

    public ContractPricingQuote CalculateQuote(ContractRequirements requirements, int quantityUnits)
    {
        if (requirements is null)
        {
            throw new ArgumentNullException(nameof(requirements));
        }

        if (quantityUnits < ContractBalance.MinQuantityUnits || quantityUnits > ContractBalance.MaxQuantityUnits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityUnits),
                $"Quantity must be within [{ContractBalance.MinQuantityUnits}, {ContractBalance.MaxQuantityUnits}].");
        }

        requirements.Validate();

        var runwayPriceMultiplier = ContractBalance.ResolveCommercialRunwayPriceMultiplier(
            requirements.RequiredCategory,
            requirements.MaximumRequiredRunwayLengthMeters);
        var estimatedMinimumUnitCost = EstimateMinimumUnitProductionCost(requirements) * runwayPriceMultiplier;
        var markup = quantityUnits < ContractBalance.LargeOrderMinQuantityUnits
            ? ContractBalance.SmallOrderContractMarkup
            : ContractBalance.LargeOrderContractMarkup;
        var priceMultiplier = ResolvePriceMultiplier(requirements.RequiredCategory);
        var adjustedMarkup = ((1d + markup) * priceMultiplier) - 1d;
        if (adjustedMarkup < ContractBalance.CampaignAdjustedMarkupFloor)
        {
            adjustedMarkup = ContractBalance.CampaignAdjustedMarkupFloor;
        }

        var unitPrice = estimatedMinimumUnitCost * (1d + adjustedMarkup);
        var totalPrice = unitPrice * quantityUnits;

        return new ContractPricingQuote(
            RoundCurrency(estimatedMinimumUnitCost),
            RoundCurrency(unitPrice),
            RoundCurrency(totalPrice),
            adjustedMarkup);
    }

    public double EstimateMinimumUnitProductionCost(ContractRequirements requirements)
    {
        if (requirements is null)
        {
            throw new ArgumentNullException(nameof(requirements));
        }

        requirements.Validate();

        if (requirements.RequiredCategory.HasValue)
        {
            var profile = AircraftCategoryCatalog.GetProfile(requirements.RequiredCategory.Value);
            return EstimateByProfile(requirements, profile);
        }

        var profiles = AircraftCategoryCatalog.GetAllProfiles();
        if (profiles.Count == 0)
        {
            return ContractBalance.PricingMinimumUnitCostFloor;
        }

        var total = 0d;
        for (var index = 0; index < profiles.Count; index++)
        {
            total += EstimateByProfile(requirements, profiles[index]);
        }

        return total / profiles.Count;
    }

    private static double EstimateByProfile(ContractRequirements requirements, AircraftCategoryProfile profile)
    {
        var difficulty = CalculateDifficultyNormalized(requirements, profile);

        var minHardwareCost = CalculateReferenceUnitHardwareCost(
            profile.MinMaximumWeightKilograms,
            profile.MinRequiredRangeKilometers,
            profile.MinRequiredSpeedKilometersPerHour,
            profile.MinRequiredPayloadKilograms,
            profile.MinCrew);
        var maxHardwareCost = CalculateReferenceUnitHardwareCost(
            profile.MaxMaximumWeightKilograms,
            profile.MaxRequiredRangeKilometers,
            profile.MaxRequiredSpeedKilometersPerHour,
            profile.MaxRequiredPayloadKilograms,
            profile.MaxCrew);

        var minLaborCost = CalculateReferenceUnitManDayLaborCost(
            profile.Category,
            profile.MinMaximumWeightKilograms,
            profile.MinRequiredRangeKilometers,
            profile.MinRequiredSpeedKilometersPerHour,
            profile.MinRequiredPayloadKilograms,
            profile.MinCrew);
        var maxLaborCost = CalculateReferenceUnitManDayLaborCost(
            profile.Category,
            profile.MaxMaximumWeightKilograms,
            profile.MaxRequiredRangeKilometers,
            profile.MaxRequiredSpeedKilometersPerHour,
            profile.MaxRequiredPayloadKilograms,
            profile.MaxCrew);

        var estimatedHardwareCost = minHardwareCost + ((maxHardwareCost - minHardwareCost) * difficulty);
        var estimatedLaborCost = minLaborCost + ((maxLaborCost - minLaborCost) * difficulty);

        var estimated = estimatedHardwareCost + estimatedLaborCost;
        return System.Math.Max(ContractBalance.PricingMinimumUnitCostFloor, estimated);
    }

    private static double CalculateReferenceUnitHardwareCost(
        double maxWeightKilograms,
        double rangeKilometers,
        double speedKilometersPerHour,
        double payloadKilograms,
        int crew)
    {
        return ContractBalance.PricingBaseUnitCost
            + (maxWeightKilograms * ContractBalance.PricingWeightCoefficient)
            + (rangeKilometers * ContractBalance.PricingRangeCoefficient)
            + (speedKilometersPerHour * ContractBalance.PricingSpeedCoefficient)
            + (payloadKilograms * ContractBalance.PricingPayloadCoefficient)
            + (crew * ContractBalance.PricingCrewCoefficient);
    }

    private static double CalculateReferenceUnitManDayLaborCost(
        AircraftCategory category,
        double maxWeightKilograms,
        double rangeKilometers,
        double speedKilometersPerHour,
        double payloadKilograms,
        int crew)
    {
        var airframeManDays = CalculateEstimatedAirframeProductionManDays(
            maxWeightKilograms,
            rangeKilometers,
            speedKilometersPerHour,
            payloadKilograms,
            crew);

        var engineCount = EstimateEngineCount(category, maxWeightKilograms);
        var engineManDaysPerEngine = CalculateEstimatedEngineProductionManDaysPerEngine(
            maxWeightKilograms,
            speedKilometersPerHour,
            payloadKilograms,
            crew);
        var totalEngineManDays = engineManDaysPerEngine * engineCount;

        var planeLaborCostPerManDay = FactoryBalance.PlaneStaffMonthlyCostPerEmployee
            / System.Math.Max(FactoryBalance.EmployeeManDaysPerMonth, Numeric.Epsilon);
        var engineLaborCostPerManDay = FactoryBalance.EngineStaffMonthlyCostPerEmployee
            / System.Math.Max(FactoryBalance.EmployeeManDaysPerMonth, Numeric.Epsilon);

        return (airframeManDays * planeLaborCostPerManDay)
            + (totalEngineManDays * engineLaborCostPerManDay);
    }

    private static double CalculateEstimatedAirframeProductionManDays(
        double maxWeightKilograms,
        double rangeKilometers,
        double speedKilometersPerHour,
        double payloadKilograms,
        int crew)
    {
        var estimated = ContractBalance.PricingAirframeProductionManDaysBase
            + (maxWeightKilograms * ContractBalance.PricingAirframeProductionManDaysWeightCoefficient)
            + (rangeKilometers * ContractBalance.PricingAirframeProductionManDaysRangeCoefficient)
            + (speedKilometersPerHour * ContractBalance.PricingAirframeProductionManDaysSpeedCoefficient)
            + (payloadKilograms * ContractBalance.PricingAirframeProductionManDaysPayloadCoefficient)
            + (crew * ContractBalance.PricingAirframeProductionManDaysCrewCoefficient);

        return System.Math.Max(estimated, ContractBalance.PricingAirframeProductionManDaysMinimum);
    }

    private static double CalculateEstimatedEngineProductionManDaysPerEngine(
        double maxWeightKilograms,
        double speedKilometersPerHour,
        double payloadKilograms,
        int crew)
    {
        var estimated = ContractBalance.PricingEngineProductionManDaysPerEngineBase
            + (maxWeightKilograms * ContractBalance.PricingEngineProductionManDaysPerEngineWeightCoefficient)
            + (speedKilometersPerHour * ContractBalance.PricingEngineProductionManDaysPerEngineSpeedCoefficient)
            + (payloadKilograms * ContractBalance.PricingEngineProductionManDaysPerEnginePayloadCoefficient)
            + (crew * ContractBalance.PricingEngineProductionManDaysPerEngineCrewCoefficient);

        return System.Math.Max(estimated, ContractBalance.PricingEngineProductionManDaysPerEngineMinimum);
    }

    private static int EstimateEngineCount(AircraftCategory category, double maxWeightKilograms)
    {
        var estimated = maxWeightKilograms switch
        {
            <= ContractBalance.PricingEstimatedEngineCountSingleMaxWeightKilograms => 1,
            <= ContractBalance.PricingEstimatedEngineCountDoubleMaxWeightKilograms => 2,
            <= ContractBalance.PricingEstimatedEngineCountTripleMaxWeightKilograms => 3,
            _ => 4,
        };

        if (category is AircraftCategory.HeavyTransportWideBody or AircraftCategory.MilitaryStrategicSupport)
        {
            estimated = System.Math.Max(estimated, 2);
        }

        if (category == AircraftCategory.LightSportAndRecreational)
        {
            estimated = 1;
        }

        return (int)Numeric.Clamp(
            estimated,
            ContractBalance.PricingEstimatedEngineCountMin,
            ContractBalance.PricingEstimatedEngineCountMax);
    }

    private static double CalculateDifficultyNormalized(ContractRequirements requirements, AircraftCategoryProfile profile)
    {
        var sum = 0d;
        var count = 0;

        if (requirements.MinimumReliability.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MinimumReliability.Value,
                ContractBalance.MinReliability,
                ContractBalance.MaxReliability,
                invert: false));
        }

        if (requirements.MaximumTotalWeightKilograms.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MaximumTotalWeightKilograms.Value,
                profile.MinMaximumWeightKilograms,
                profile.MaxMaximumWeightKilograms,
                invert: false));
        }

        if (requirements.MinimumRangeKilometers.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MinimumRangeKilometers.Value,
                profile.MinRequiredRangeKilometers,
                profile.MaxRequiredRangeKilometers,
                invert: false));
        }

        if (requirements.MinimumSpeed.HasValue)
        {
            var minimumSpeedKmh = requirements.MinimumSpeed.Value.MinimumSpeedMetersPerSecond * 3.6d;
            AddMetric(ref sum, ref count, Normalize(
                minimumSpeedKmh,
                profile.MinRequiredSpeedKilometersPerHour,
                profile.MaxRequiredSpeedKilometersPerHour,
                invert: false));
        }

        if (requirements.MinimumPayloadKilograms.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MinimumPayloadKilograms.Value,
                profile.MinRequiredPayloadKilograms,
                profile.MaxRequiredPayloadKilograms,
                invert: false));
        }

        if (requirements.MinimumManeuverability.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MinimumManeuverability.Value,
                profile.MinRequiredManeuverability,
                profile.MaxRequiredManeuverability,
                invert: false));
        }

        if (requirements.MinimumCrew.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MinimumCrew.Value,
                profile.MinCrew,
                profile.MaxCrew,
                invert: false));
        }

        if (requirements.MaximumRequiredRunwayLengthMeters.HasValue)
        {
            AddMetric(ref sum, ref count, Normalize(
                requirements.MaximumRequiredRunwayLengthMeters.Value,
                PlaneDesignerBalance.Airframe.RunwayShortLengthMeters,
                PlaneDesignerBalance.Airframe.RunwayLongLengthMeters,
                invert: true));
        }

        if (requirements.MaximumFuelConsumptionKilogramsPerHour.HasValue)
        {
            var (minimumFuelConsumption, maximumFuelConsumption) =
                ContractBalance.ResolveFuelConsumptionRequirementRangeKilogramsPerHour(profile.Category);
            AddMetric(ref sum, ref count, Normalize(
                requirements.MaximumFuelConsumptionKilogramsPerHour.Value,
                minimumFuelConsumption,
                maximumFuelConsumption,
                invert: true));
        }

        if (count == 0)
        {
            return 0.5d;
        }

        return Numeric.Clamp(sum / count, 0d, 1d);
    }

    private static double Normalize(double value, double minValue, double maxValue, bool invert)
    {
        if (maxValue - minValue <= Numeric.Epsilon)
        {
            return 0.5d;
        }

        var normalized = Numeric.Clamp((value - minValue) / (maxValue - minValue), 0d, 1d);
        return invert ? 1d - normalized : normalized;
    }

    private static void AddMetric(ref double sum, ref int count, double value)
    {
        sum += value;
        count++;
    }

    private double ResolvePriceMultiplier(AircraftCategory? category)
    {
        var modifiers = _campaignModifierSource?.GetCurrentModifiers();
        if (modifiers is null)
        {
            return 1d;
        }

        return modifiers.ResolvePriceMultiplier(category);
    }

    private static double RoundCurrency(double value) =>
        System.Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
