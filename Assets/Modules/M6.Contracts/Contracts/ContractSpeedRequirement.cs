using System;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Contracts;

public readonly record struct ContractSpeedRequirement(
    double AltitudeMeters,
    double MinimumSpeedMetersPerSecond)
{
    public void Validate()
    {
        Numeric.ThrowIfNegative(AltitudeMeters, nameof(AltitudeMeters));
        Numeric.ThrowIfNotFinite(MinimumSpeedMetersPerSecond, nameof(MinimumSpeedMetersPerSecond));

        if (MinimumSpeedMetersPerSecond <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumSpeedMetersPerSecond),
                "Minimum speed must be greater than zero.");
        }
    }
}
