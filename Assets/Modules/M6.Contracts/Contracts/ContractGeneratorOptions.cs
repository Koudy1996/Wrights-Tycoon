using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Contracts;

public sealed class ContractGeneratorOptions
{
    public double BalancedPriorityShare { get; init; }
    public int MinContractsPerMonth { get; init; } = ContractBalance.MinContractsPerMonth;

    public int MaxContractsPerMonth { get; init; } = ContractBalance.MaxContractsPerMonth;

    public int MinQuantityUnits { get; init; } = ContractBalance.MinQuantityUnits;

    public int MaxQuantityUnits { get; init; } = ContractBalance.MaxQuantityUnits;

    public double SmallOrderShare { get; init; } = ContractBalance.SmallOrderShare;

    public int SmallOrderMinQuantityUnits { get; init; } = ContractBalance.SmallOrderMinQuantityUnits;

    public int SmallOrderMaxQuantityUnits { get; init; } = ContractBalance.SmallOrderMaxQuantityUnits;

    public int LargeOrderMinQuantityUnits { get; init; } = ContractBalance.LargeOrderMinQuantityUnits;

    public int LargeOrderMaxQuantityUnits { get; init; } = ContractBalance.LargeOrderMaxQuantityUnits;

    public int MinDeadlineMonths { get; init; } = ContractBalance.MinDeadlineMonths;

    public int MaxDeadlineMonths { get; init; } = ContractBalance.MaxDeadlineMonths;

    public double MinReliability { get; init; } = ContractBalance.MinReliability;

    public double MaxReliability { get; init; } = ContractBalance.MaxReliability;

    public double MinRangeKilometers { get; init; } = ContractBalance.MinRangeKilometers;

    public double MaxRangeKilometers { get; init; } = ContractBalance.MaxRangeKilometers;

    public double MinSpeedMetersPerSecond { get; init; } = ContractBalance.MinSpeedMetersPerSecond;

    public double MaxSpeedMetersPerSecond { get; init; } = ContractBalance.MaxSpeedMetersPerSecond;

    public double MinPayloadKilograms { get; init; } = ContractBalance.MinPayloadKilograms;

    public double MaxPayloadKilograms { get; init; } = ContractBalance.MaxPayloadKilograms;

    public int MinCrew { get; init; } = ContractBalance.MinCrew;

    public int MaxCrew { get; init; } = ContractBalance.MaxCrew;

    public double LowDemandBandMinNormalized { get; init; } = ContractBalance.LowDemandBandMinNormalized;

    public double LowDemandBandMaxNormalized { get; init; } = ContractBalance.LowDemandBandMaxNormalized;

    public double HighDemandBandMinNormalized { get; init; } = ContractBalance.HighDemandBandMinNormalized;

    public double HighDemandBandMaxNormalized { get; init; } = ContractBalance.HighDemandBandMaxNormalized;

    public IReadOnlyList<double> SpeedRequirementAltitudesMeters { get; init; } = ContractBalance.SpeedRequirementAltitudesMeters;

    public IReadOnlyList<string> CompanyPrefixes { get; init; } = ContractBalance.CompanyPrefixes;

    public IReadOnlyList<string> CompanySuffixes { get; init; } = ContractBalance.CompanySuffixes;

    public IReadOnlyList<AircraftCategory> RequestedCategories { get; init; } = ContractBalance.RequestedCategories;

    public void Validate()
    {
        if (double.IsNaN(BalancedPriorityShare) || BalancedPriorityShare < 0d || BalancedPriorityShare > 1d)
            throw new ArgumentOutOfRangeException(nameof(BalancedPriorityShare));
        ValidateRange(MinContractsPerMonth, MaxContractsPerMonth, nameof(MinContractsPerMonth), nameof(MaxContractsPerMonth));
        ValidateRange(MinQuantityUnits, MaxQuantityUnits, nameof(MinQuantityUnits), nameof(MaxQuantityUnits));
        ValidateRange(SmallOrderMinQuantityUnits, SmallOrderMaxQuantityUnits, nameof(SmallOrderMinQuantityUnits), nameof(SmallOrderMaxQuantityUnits));
        ValidateRange(LargeOrderMinQuantityUnits, LargeOrderMaxQuantityUnits, nameof(LargeOrderMinQuantityUnits), nameof(LargeOrderMaxQuantityUnits));
        ValidateRange(MinDeadlineMonths, MaxDeadlineMonths, nameof(MinDeadlineMonths), nameof(MaxDeadlineMonths));
        ValidateRange(MinCrew, MaxCrew, nameof(MinCrew), nameof(MaxCrew));

        ValidateRange(MinReliability, MaxReliability, nameof(MinReliability), nameof(MaxReliability));
        ValidateRange(MinRangeKilometers, MaxRangeKilometers, nameof(MinRangeKilometers), nameof(MaxRangeKilometers));
        ValidateRange(MinSpeedMetersPerSecond, MaxSpeedMetersPerSecond, nameof(MinSpeedMetersPerSecond), nameof(MaxSpeedMetersPerSecond));
        ValidateRange(MinPayloadKilograms, MaxPayloadKilograms, nameof(MinPayloadKilograms), nameof(MaxPayloadKilograms));
        ValidateRange(LowDemandBandMinNormalized, LowDemandBandMaxNormalized, nameof(LowDemandBandMinNormalized), nameof(LowDemandBandMaxNormalized));
        ValidateRange(HighDemandBandMinNormalized, HighDemandBandMaxNormalized, nameof(HighDemandBandMinNormalized), nameof(HighDemandBandMaxNormalized));

        Numeric.ThrowIfNotFinite(SmallOrderShare, nameof(SmallOrderShare));
        if (SmallOrderShare < 0d || SmallOrderShare > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(SmallOrderShare), "Small order share must stay within [0, 1].");
        }

        if (SmallOrderMaxQuantityUnits >= LargeOrderMinQuantityUnits)
        {
            throw new ArgumentException("Small order quantity range must stay below the large order minimum.");
        }

        if (SmallOrderMinQuantityUnits < MinQuantityUnits || SmallOrderMaxQuantityUnits > MaxQuantityUnits)
        {
            throw new ArgumentException("Small order quantity range must fit within global quantity bounds.");
        }

        if (LargeOrderMinQuantityUnits < MinQuantityUnits || LargeOrderMaxQuantityUnits > MaxQuantityUnits)
        {
            throw new ArgumentException("Large order quantity range must fit within global quantity bounds.");
        }

        if (LowDemandBandMinNormalized < 0d || LowDemandBandMaxNormalized > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(LowDemandBandMinNormalized), "Low-demand band must stay within [0, 1].");
        }

        if (HighDemandBandMinNormalized < 0d || HighDemandBandMaxNormalized > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(HighDemandBandMinNormalized), "High-demand band must stay within [0, 1].");
        }

        if (MinReliability < 0d || MaxReliability > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(MinReliability), "Reliability range must stay within [0, 1].");
        }

        if (SpeedRequirementAltitudesMeters is null || SpeedRequirementAltitudesMeters.Count == 0)
        {
            throw new ArgumentException("At least one speed requirement altitude is required.", nameof(SpeedRequirementAltitudesMeters));
        }

        if (CompanyPrefixes is null || CompanyPrefixes.Count == 0)
        {
            throw new ArgumentException("At least one company prefix is required.", nameof(CompanyPrefixes));
        }

        if (CompanySuffixes is null || CompanySuffixes.Count == 0)
        {
            throw new ArgumentException("At least one company suffix is required.", nameof(CompanySuffixes));
        }

        if (RequestedCategories is null || RequestedCategories.Count == 0)
        {
            throw new ArgumentException("At least one aircraft category is required.", nameof(RequestedCategories));
        }

        foreach (var altitude in SpeedRequirementAltitudesMeters)
        {
            Numeric.ThrowIfNegative(altitude, nameof(SpeedRequirementAltitudesMeters));
        }

        if (CompanyPrefixes.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Company prefixes cannot contain empty values.", nameof(CompanyPrefixes));
        }

        if (CompanySuffixes.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Company suffixes cannot contain empty values.", nameof(CompanySuffixes));
        }

        foreach (var category in RequestedCategories)
        {
            _ = AircraftCategoryCatalog.GetProfile(category);
        }
    }

    private static void ValidateRange(int min, int max, string minName, string maxName)
    {
        if (min > max)
        {
            throw new ArgumentException($"{minName} must be less than or equal to {maxName}.");
        }
    }

    private static void ValidateRange(double min, double max, string minName, string maxName)
    {
        Numeric.ThrowIfNotFinite(min, minName);
        Numeric.ThrowIfNotFinite(max, maxName);
        if (min > max)
        {
            throw new ArgumentException($"{minName} must be less than or equal to {maxName}.");
        }
    }
}
