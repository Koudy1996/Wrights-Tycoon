using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.Balance;

// M6 tuning surface for contract generation distributions and constraints.
public static class ContractBalance
{
    public const int MinContractsPerMonth = 1;
    public const int MaxContractsPerMonth = 5;
    public const int MinQuantityUnits = 5;
    public const int MaxQuantityUnits = 500;
    public const double SmallOrderShare = 0.75d;
    public const int SmallOrderMinQuantityUnits = 5;
    public const int SmallOrderMaxQuantityUnits = 99;
    public const int LargeOrderMinQuantityUnits = 100;
    public const int LargeOrderMaxQuantityUnits = 500;
    public const int MinDeadlineMonths = 6;
    public const int MaxDeadlineMonths = 36;

    public const double MinReliability = 0.72d;
    public const double MaxReliability = 0.985d;
    public const double MinRangeKilometers = 320d;
    public const double MaxRangeKilometers = 7200d;
    public const double MinSpeedMetersPerSecond = 55d;
    public const double MaxSpeedMetersPerSecond = 220d;
    public const double MinPayloadKilograms = 250d;
    public const double MaxPayloadKilograms = 12000d;

    public const int MinCrew = 1;
    public const int MaxCrew = 6;

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
    public const double LargeOrderContractMarkup = 0.20d;

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
    };
}
