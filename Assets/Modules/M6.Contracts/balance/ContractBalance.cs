using System;
using System.Collections.Generic;
using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.Balance;

// M6 tuning surface for contract generation distributions and constraints.
public static class ContractBalance
{
    public const int MinContractsPerMonth = 1;
    public const int MaxContractsPerMonth = 6;
    public const int MinQuantityUnits = 20;
    public const int MaxQuantityUnits = 500;
    public const double SmallOrderShare = 0.75d;
    public const int SmallOrderMinQuantityUnits = 20;
    public const int SmallOrderMaxQuantityUnits = 99;
    public const int LargeOrderMinQuantityUnits = 100;
    public const int LargeOrderMaxQuantityUnits = 500;
    public const int MinDeadlineMonths = 6;
    public const int MaxDeadlineMonths = 36;

    public const double MinReliability = 0.65d;
    public const double MaxReliability = 0.9d;
    public const double MinRangeKilometers = 320d;
    public const double MaxRangeKilometers = 7000d;
    public const double MinSpeedMetersPerSecond = 55d;
    public const double MaxSpeedMetersPerSecond = 220d;
    public const double MinPayloadKilograms = 250d;
    public const double MaxPayloadKilograms = 12000d;

    public const int MinCrew = 1;
    public const int MaxCrew = 6;

    public const string HistoricalCampaignContractParameterFuelConsumption = "fuel_consumption";
    public const double FuelConsumptionRequirementBaseKilogramsPerHour = 60d;
    public const double FuelConsumptionRequirementPayloadCoefficient = 0.020d;
    public const double FuelConsumptionRequirementSpeedCoefficient = 0.35d;
    public const double FuelConsumptionRequirementRangePaddingKilogramsPerHour = 25d;
    public const double FuelConsumptionRequirementMinimumKilogramsPerHour = 60d;
    public const double FuelConsumptionRequirementMaximumKilogramsPerHour = 8000d;

    public const double LowDemandBandMinNormalized = 0d;
    public const double LowDemandBandMaxNormalized = 1d / 3d;
    public const double HighDemandBandMinNormalized = 1d / 3d;
    public const double HighDemandBandMaxNormalized = 1d;

    public const double PricingBaseUnitCost = 50000d;
    public const double PricingWeightCoefficient = 6d;
    public const double PricingRangeCoefficient = 30d;
    public const double PricingSpeedCoefficient = 220d;
    public const double PricingPayloadCoefficient = 12d;
    public const double PricingCrewCoefficient = 10000d;

    // Labor (MD) estimation for contract pricing.
    public const double PricingAirframeProductionManDaysBase = 60d;
    public const double PricingAirframeProductionManDaysWeightCoefficient = 0.0035d;
    public const double PricingAirframeProductionManDaysRangeCoefficient = 0.020d;
    public const double PricingAirframeProductionManDaysSpeedCoefficient = 0.120d;
    public const double PricingAirframeProductionManDaysPayloadCoefficient = 0.0025d;
    public const double PricingAirframeProductionManDaysCrewCoefficient = 12d;
    public const double PricingAirframeProductionManDaysMinimum = 15d;

    public const double PricingEngineProductionManDaysPerEngineBase = 35d;
    public const double PricingEngineProductionManDaysPerEngineWeightCoefficient = 0.0008d;
    public const double PricingEngineProductionManDaysPerEngineSpeedCoefficient = 0.080d;
    public const double PricingEngineProductionManDaysPerEnginePayloadCoefficient = 0.0010d;
    public const double PricingEngineProductionManDaysPerEngineCrewCoefficient = 3d;
    public const double PricingEngineProductionManDaysPerEngineMinimum = 8d;

    public const double PricingEstimatedEngineCountSingleMaxWeightKilograms = 7000d;
    public const double PricingEstimatedEngineCountDoubleMaxWeightKilograms = 80000d;
    public const double PricingEstimatedEngineCountTripleMaxWeightKilograms = 180000d;
    public const int PricingEstimatedEngineCountMin = 1;
    public const int PricingEstimatedEngineCountMax = 4;

    public const double PricingMinimumUnitCostFloor = 25000d;
    public const double SmallOrderContractMarkup = 0.25d;
    public const double LargeOrderContractMarkup = 0.18d;
    public const double ContractPriceMarginImprovementMultiplier = 1.05d;
    public const double AdditionalMarginMultiplier = 1.10d;
    public const double CampaignAdjustedMarkupFloor = 0.02d;
    public const int CampaignWeightedCategoryPoolBaseCopies = 4;
    public const int CampaignWeightedCategoryPoolMinimumCopies = 1;
    public const int CampaignWeightedCategoryPoolMaximumCopies = 12;

    public const double CampaignMixKoreaWar = 2.25d;
    public const double CampaignMixKoreaArmistice = 1.35d;
    public const double CampaignMixVietnamWar = 2.10d;
    public const double CampaignMixGlobalAirTravelExpansion = 1.60d;
    public const double CampaignMixColdWarEnd = 1.30d;
    public const double CampaignMixGlobalAviationCrisis = 0.70d;
    public const double CampaignMixGlobalFinancialCrisis = 0.75d;

    public const double CampaignPriceKoreaWar = 1.12d;
    public const double CampaignPriceKoreaArmistice = 0.92d;
    public const double CampaignPriceVietnamWar = 1.15d;
    public const double CampaignPriceGlobalOilCrisis = 0.92d;
    public const double CampaignPriceSecondOilCrisis = 0.94d;

    public const int CampaignBonusBatchKoreaMinContracts = 8;
    public const int CampaignBonusBatchKoreaMaxContracts = 12;
    public const int CampaignBonusBatchVietnamMinContracts = 10;
    public const int CampaignBonusBatchVietnamMaxContracts = 14;
    public const int CampaignBonusBatchCivilCopies = 1;
    public const int CampaignBonusBatchMilitaryBaseCopies = 4;
    public const int CampaignBonusBatchMilitaryMaxCopies = 12;

    public static readonly double[] SpeedRequirementAltitudesMeters = { 0d, 5000d, 10000d };

    public static readonly string[] CompanyPrefixes =
    {
        "Aero",
        "North",
        "Blue",
        "Silver",
        "Atlas",
        "Summit",
        "Polar",
        "Sky",
        "Star",
        "Frontier",
        "Pacific",
        "Global",
    };

    public static readonly string[] CompanySuffixes =
    {
        "Freight",
        "Cargo",
        "Logistics",
        "Transport",
        "Airlift",
        "Airways",
        "Express",
        "Mail",
        "Supply",
        "Carriers",
    };

    public static readonly AircraftCategory[] RequestedCategories =
    {
        AircraftCategory.LightSportAndRecreational,
        AircraftCategory.LightMultiRolePiston,
        AircraftCategory.TurbopropRegional,
        AircraftCategory.RegionalJet,
        AircraftCategory.MediumTransportNarrowBody,
        AircraftCategory.HeavyTransportWideBody,
        AircraftCategory.SpecialCivil,
        AircraftCategory.BusinessVipJet,
        AircraftCategory.MilitaryTactical,
        AircraftCategory.MilitaryStrategicSupport,
        AircraftCategory.LightCargo,
        AircraftCategory.MediumCargo,
        AircraftCategory.HeavyCargo,
        AircraftCategory.LongRangeCargo,
        AircraftCategory.Bomber,
    };

    public static readonly AircraftCategory[] CivilRequestedCategories =
    {
        AircraftCategory.LightSportAndRecreational,
        AircraftCategory.LightMultiRolePiston,
        AircraftCategory.TurbopropRegional,
        AircraftCategory.RegionalJet,
        AircraftCategory.MediumTransportNarrowBody,
        AircraftCategory.HeavyTransportWideBody,
        AircraftCategory.SpecialCivil,
        AircraftCategory.BusinessVipJet,
        AircraftCategory.LightCargo,
        AircraftCategory.MediumCargo,
        AircraftCategory.HeavyCargo,
        AircraftCategory.LongRangeCargo,
    };

    public static readonly AircraftCategory[] MilitaryRequestedCategories =
    {
        AircraftCategory.MilitaryTactical,
        AircraftCategory.MilitaryStrategicSupport,
        AircraftCategory.Bomber,
    };

    public static readonly AircraftCategory[] HistoricalCampaignInitiallyLockedCategories =
    {
        AircraftCategory.HeavyTransportWideBody,
    };

    // Categories that must stay unavailable before their historical introduction.
    public const int HeavyTransportWideBodyIntroductionYear = 1960;

    // Editable category groups for runway-constrained contracts.
    // Categories may appear in more than one group to enable multiple valid runway requirements.
    public static readonly AircraftCategory[] ShortRunwayCategories =
    {
        AircraftCategory.LightSportAndRecreational,
        AircraftCategory.LightMultiRolePiston,
        AircraftCategory.TurbopropRegional,
        AircraftCategory.SpecialCivil,
        AircraftCategory.BusinessVipJet,
        AircraftCategory.MilitaryTactical,
        AircraftCategory.LightCargo,
    };

    public static readonly AircraftCategory[] MediumRunwayCategories =
    {
        AircraftCategory.LightMultiRolePiston,
        AircraftCategory.TurbopropRegional,
        AircraftCategory.RegionalJet,
        AircraftCategory.MediumTransportNarrowBody,
        AircraftCategory.SpecialCivil,
        AircraftCategory.BusinessVipJet,
        AircraftCategory.MilitaryTactical,
        AircraftCategory.MilitaryStrategicSupport,
        AircraftCategory.LightCargo,
        AircraftCategory.MediumCargo,
        AircraftCategory.Bomber,
    };

    public static readonly AircraftCategory[] LongRunwayCategories =
    {
        AircraftCategory.RegionalJet,
        AircraftCategory.MediumTransportNarrowBody,
        AircraftCategory.HeavyTransportWideBody,
        AircraftCategory.MilitaryStrategicSupport,
        AircraftCategory.MediumCargo,
        AircraftCategory.HeavyCargo,
        AircraftCategory.LongRangeCargo,
        AircraftCategory.Bomber,
    };

    public static double[] ResolveAllowedRunwayLengthMeters(AircraftCategory category)
    {
        var allowedRunwayLengths = new List<double>(capacity: 3);

        if (ContainsCategory(ShortRunwayCategories, category))
        {
            allowedRunwayLengths.Add(PlaneDesignerBalance.Airframe.RunwayShortLengthMeters);
        }

        if (ContainsCategory(MediumRunwayCategories, category))
        {
            allowedRunwayLengths.Add(PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters);
        }

        if (ContainsCategory(LongRunwayCategories, category))
        {
            allowedRunwayLengths.Add(PlaneDesignerBalance.Airframe.RunwayLongLengthMeters);
        }

        if (allowedRunwayLengths.Count == 0)
        {
            allowedRunwayLengths.Add(PlaneDesignerBalance.Airframe.RunwayMediumLengthMeters);
        }

        return allowedRunwayLengths.ToArray();
    }

    public static double ResolveRequiredRunwayLengthMeters(AircraftCategory category) =>
        ResolveRequiredRunwayLengthMeters(category, runwayOptionRoll: 0d);

    public static (double Min, double Max) ResolveReferenceStructuralIndexRange(AircraftCategory category) => category switch
    {
        AircraftCategory.LightSportAndRecreational => (20d, 40d),
        AircraftCategory.LightMultiRolePiston => (30d, 80d),
        AircraftCategory.TurbopropRegional => (85d, 180d),
        AircraftCategory.RegionalJet => (105d, 330d),
        AircraftCategory.MediumTransportNarrowBody => (170d, 600d),
        AircraftCategory.HeavyTransportWideBody => (650d, 1200d),
        AircraftCategory.SpecialCivil => (40d, 300d),
        AircraftCategory.BusinessVipJet => (80d, 250d),
        AircraftCategory.MilitaryTactical => (110d, 370d),
        AircraftCategory.MilitaryStrategicSupport => (375d, 950d),
        AircraftCategory.LightCargo => (40d, 75d),
        AircraftCategory.MediumCargo => (120d, 350d),
        AircraftCategory.HeavyCargo => (375d, 900d),
        AircraftCategory.LongRangeCargo => (750d, 1200d),
        AircraftCategory.Bomber => (350d, 900d),
        _ => (ProductionEconomyBalance.Airframe.ReferenceStructuralIndex, 180d),
    };

    public static double ResolveRequiredRunwayLengthMeters(AircraftCategory category, double runwayOptionRoll)
    {
        var allowedRunwayLengths = ResolveAllowedRunwayLengthMeters(category);
        if (allowedRunwayLengths.Length == 1)
        {
            return allowedRunwayLengths[0];
        }

        if (double.IsNaN(runwayOptionRoll) || double.IsInfinity(runwayOptionRoll) || runwayOptionRoll <= 0d)
        {
            return allowedRunwayLengths[0];
        }

        if (runwayOptionRoll >= 1d)
        {
            return allowedRunwayLengths[allowedRunwayLengths.Length - 1];
        }

        var index = (int)(runwayOptionRoll * allowedRunwayLengths.Length);
        if (index < 0)
        {
            index = 0;
        }
        else if (index >= allowedRunwayLengths.Length)
        {
            index = allowedRunwayLengths.Length - 1;
        }

        return allowedRunwayLengths[index];
    }

    public static int ResolveHistoricalCampaignCategoryPoolCopies(double weightMultiplier)
    {
        if (double.IsNaN(weightMultiplier) || double.IsInfinity(weightMultiplier))
        {
            throw new ArgumentOutOfRangeException(nameof(weightMultiplier), "Weight multiplier must be finite.");
        }

        var clampedMultiplier = System.Math.Max(0d, weightMultiplier);
        var copies = (int)System.Math.Round(
            CampaignWeightedCategoryPoolBaseCopies * clampedMultiplier,
            MidpointRounding.AwayFromZero);
        if (copies < CampaignWeightedCategoryPoolMinimumCopies)
        {
            copies = CampaignWeightedCategoryPoolMinimumCopies;
        }
        else if (copies > CampaignWeightedCategoryPoolMaximumCopies)
        {
            copies = CampaignWeightedCategoryPoolMaximumCopies;
        }

        return copies;
    }

    public static bool IsCivilContractCategory(AircraftCategory category) => ContainsCategory(CivilRequestedCategories, category);

    public static bool IsMilitaryContractCategory(AircraftCategory category) => ContainsCategory(MilitaryRequestedCategories, category);

    public static bool IsCategoryAvailableForYear(AircraftCategory category, int year)
    {
        if (year < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be greater than or equal to 1.");
        }

        return category switch
        {
            AircraftCategory.HeavyTransportWideBody => year >= HeavyTransportWideBodyIntroductionYear,
            _ => true,
        };
    }

    [Obsolete("Use TypeRequirementBalance.ResolveMinimumRequirementMultiplier instead.")]
    public static double ResolveHistoricalCampaignRequirementDifficultyMultiplier(int year, int playerYearOffset = 0) =>
        TypeRequirementBalance.ResolveMinimumRequirementMultiplier(year, playerYearOffset);

    public static bool TryResolveHistoricalCampaignMixWeightMultiplier(string? balanceKey, out double multiplier)
    {
        multiplier = 1d;
        if (string.IsNullOrWhiteSpace(balanceKey))
        {
            return false;
        }

        switch (balanceKey.Trim())
        {
            case "contract.event.korea_war.mix":
                multiplier = CampaignMixKoreaWar;
                return true;
            case "contract.event.korea_armistice.mix":
                multiplier = CampaignMixKoreaArmistice;
                return true;
            case "contract.event.vietnam_war.mix":
                multiplier = CampaignMixVietnamWar;
                return true;
            case "contract.event.global_air_travel.mix":
                multiplier = CampaignMixGlobalAirTravelExpansion;
                return true;
            case "contract.event.cold_war_end.mix":
                multiplier = CampaignMixColdWarEnd;
                return true;
            case "contract.event.global_aviation_crisis.mix":
                multiplier = CampaignMixGlobalAviationCrisis;
                return true;
            case "contract.event.financial_crisis.mix":
                multiplier = CampaignMixGlobalFinancialCrisis;
                return true;
            default:
                return false;
        }
    }

    public static bool TryResolveHistoricalCampaignPriceMultiplier(string? balanceKey, out double multiplier)
    {
        multiplier = 1d;
        if (string.IsNullOrWhiteSpace(balanceKey))
        {
            return false;
        }

        switch (balanceKey.Trim())
        {
            case "contract.event.korea_war.price":
                multiplier = CampaignPriceKoreaWar;
                return true;
            case "contract.event.korea_armistice.price":
                multiplier = CampaignPriceKoreaArmistice;
                return true;
            case "contract.event.vietnam_war.price":
                multiplier = CampaignPriceVietnamWar;
                return true;
            case "contract.event.oil_crisis.price":
                multiplier = CampaignPriceGlobalOilCrisis;
                return true;
            case "contract.event.second_oil_crisis.price":
                multiplier = CampaignPriceSecondOilCrisis;
                return true;
            default:
                return false;
        }
    }

    public static bool TryResolveHistoricalCampaignBonusBatchRange(string? balanceKey, out int minContracts, out int maxContracts)
    {
        minContracts = 0;
        maxContracts = 0;
        if (string.IsNullOrWhiteSpace(balanceKey))
        {
            return false;
        }

        switch (balanceKey.Trim())
        {
            case "contract.event.korea_war.bonus_batch":
                minContracts = CampaignBonusBatchKoreaMinContracts;
                maxContracts = CampaignBonusBatchKoreaMaxContracts;
                return true;
            case "contract.event.vietnam_war.bonus_batch":
                minContracts = CampaignBonusBatchVietnamMinContracts;
                maxContracts = CampaignBonusBatchVietnamMaxContracts;
                return true;
            default:
                return false;
        }
    }

    public static AircraftCategory[] BuildBonusBatchRequestedCategories(double militaryWeightMultiplier)
    {
        if (double.IsNaN(militaryWeightMultiplier) || double.IsInfinity(militaryWeightMultiplier))
        {
            throw new ArgumentOutOfRangeException(nameof(militaryWeightMultiplier), "Military weight multiplier must be finite.");
        }

        var categories = new List<AircraftCategory>(capacity: 64);
        var militaryCopies = (int)System.Math.Round(
            CampaignBonusBatchMilitaryBaseCopies * System.Math.Max(0d, militaryWeightMultiplier),
            MidpointRounding.AwayFromZero);
        if (militaryCopies < 1)
        {
            militaryCopies = 1;
        }
        else if (militaryCopies > CampaignBonusBatchMilitaryMaxCopies)
        {
            militaryCopies = CampaignBonusBatchMilitaryMaxCopies;
        }

        for (var copyIndex = 0; copyIndex < militaryCopies; copyIndex++)
        {
            for (var militaryIndex = 0; militaryIndex < MilitaryRequestedCategories.Length; militaryIndex++)
            {
                categories.Add(MilitaryRequestedCategories[militaryIndex]);
            }
        }

        for (var copyIndex = 0; copyIndex < CampaignBonusBatchCivilCopies; copyIndex++)
        {
            for (var civilIndex = 0; civilIndex < CivilRequestedCategories.Length; civilIndex++)
            {
                categories.Add(CivilRequestedCategories[civilIndex]);
            }
        }

        return categories.ToArray();
    }

    public static bool TryNormalizeHistoricalCampaignContractParameter(string? parameterId, out string normalizedParameterId)
    {
        normalizedParameterId = string.Empty;
        if (string.IsNullOrWhiteSpace(parameterId))
        {
            return false;
        }

        var trimmed = parameterId.Trim();
        if (string.Equals(trimmed, HistoricalCampaignContractParameterFuelConsumption, StringComparison.OrdinalIgnoreCase))
        {
            normalizedParameterId = HistoricalCampaignContractParameterFuelConsumption;
            return true;
        }

        return false;
    }

    public static (double MinValue, double MaxValue) ResolveFuelConsumptionRequirementRangeKilogramsPerHour(AircraftCategory category)
    {
        var profile = AircraftCategoryCatalog.GetProfile(category);

        var minValue = FuelConsumptionRequirementBaseKilogramsPerHour
            + (profile.MinRequiredPayloadKilograms * FuelConsumptionRequirementPayloadCoefficient)
            + (profile.MinRequiredSpeedKilometersPerHour * FuelConsumptionRequirementSpeedCoefficient)
            - FuelConsumptionRequirementRangePaddingKilogramsPerHour;

        var maxValue = FuelConsumptionRequirementBaseKilogramsPerHour
            + (profile.MaxRequiredPayloadKilograms * FuelConsumptionRequirementPayloadCoefficient)
            + (profile.MaxRequiredSpeedKilometersPerHour * FuelConsumptionRequirementSpeedCoefficient)
            + FuelConsumptionRequirementRangePaddingKilogramsPerHour;

        minValue = Clamp(minValue, FuelConsumptionRequirementMinimumKilogramsPerHour, FuelConsumptionRequirementMaximumKilogramsPerHour);
        maxValue = Clamp(maxValue, FuelConsumptionRequirementMinimumKilogramsPerHour, FuelConsumptionRequirementMaximumKilogramsPerHour);

        if (maxValue < minValue)
        {
            maxValue = minValue;
        }

        return (minValue, maxValue);
    }

    private static double Clamp(double value, double minValue, double maxValue)
    {
        if (value < minValue)
        {
            return minValue;
        }

        return value > maxValue ? maxValue : value;
    }

    private static bool ContainsCategory(AircraftCategory[] configuredCategories, AircraftCategory category)
    {
        for (var index = 0; index < configuredCategories.Length; index++)
        {
            if (configuredCategories[index] == category)
            {
                return true;
            }
        }

        return false;
    }
}
