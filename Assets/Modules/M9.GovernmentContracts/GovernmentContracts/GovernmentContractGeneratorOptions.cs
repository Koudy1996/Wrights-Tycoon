using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractGeneratorOptions
{
    public IReadOnlyList<AircraftCategory> RequestedAircraftTypes { get; init; } =
        ContractBalance.RequestedCategories.ToArray();

    public double MinReliability { get; init; } = ContractBalance.MinReliability;

    public double MaxReliability { get; init; } = ContractBalance.MaxReliability;

    public double MinRunwayLengthMeters { get; init; } = PlaneDesignerBalance.Airframe.RunwayShortLengthMeters;

    public double MaxRunwayLengthMeters { get; init; } = PlaneDesignerBalance.Airframe.RunwayLongLengthMeters;

    public string ContractIdPrefix { get; init; } = "gov";

    public void Validate()
    {
        if (RequestedAircraftTypes is null || RequestedAircraftTypes.Count == 0)
        {
            throw new InvalidOperationException("At least one requested aircraft type must be configured.");
        }

        for (var index = 0; index < RequestedAircraftTypes.Count; index++)
        {
            _ = AircraftCategoryCatalog.GetProfile(RequestedAircraftTypes[index]);
        }

        if (string.IsNullOrWhiteSpace(ContractIdPrefix))
        {
            throw new ArgumentException("Contract id prefix cannot be empty.", nameof(ContractIdPrefix));
        }

        Numeric.ThrowIfNotFinite(MinReliability, nameof(MinReliability));
        Numeric.ThrowIfNotFinite(MaxReliability, nameof(MaxReliability));
        Numeric.ThrowIfNotFinite(MinRunwayLengthMeters, nameof(MinRunwayLengthMeters));
        Numeric.ThrowIfNotFinite(MaxRunwayLengthMeters, nameof(MaxRunwayLengthMeters));

        if (MinReliability < 0d || MinReliability > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(MinReliability), "Minimum reliability must be within [0, 1].");
        }

        if (MaxReliability < 0d || MaxReliability > 1d || MaxReliability < MinReliability)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxReliability), "Maximum reliability must be >= minimum reliability and within [0, 1].");
        }

        if (MinRunwayLengthMeters <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(MinRunwayLengthMeters), "Minimum runway length must be > 0.");
        }

        if (MaxRunwayLengthMeters < MinRunwayLengthMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRunwayLengthMeters), "Maximum runway length must be >= minimum runway length.");
        }
    }
}
