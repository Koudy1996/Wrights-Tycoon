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

        if (MinReliability < 0d || MinReliability > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(MinReliability), "Minimum reliability must be within [0, 1].");
        }

        if (MaxReliability < 0d || MaxReliability > 1d || MaxReliability < MinReliability)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxReliability), "Maximum reliability must be >= minimum reliability and within [0, 1].");
        }
    }
}
