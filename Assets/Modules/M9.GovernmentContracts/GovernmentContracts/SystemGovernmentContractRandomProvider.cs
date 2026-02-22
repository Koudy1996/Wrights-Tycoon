using System;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class SystemGovernmentContractRandomProvider : IGovernmentContractRandomProvider
{
    private readonly Random _random;

    public SystemGovernmentContractRandomProvider(int? seed = null)
    {
        _random = seed.HasValue
            ? new Random(seed.Value)
            : new Random();
    }

    public int NextIntInclusive(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInclusive), "Maximum inclusive value must be >= minimum inclusive value.");
        }

        return _random.Next(minInclusive, maxInclusive + 1);
    }

    public double NextDouble() => _random.NextDouble();

    public double NextDouble(double minInclusive, double maxExclusive)
    {
        if (maxExclusive < minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Maximum exclusive value must be >= minimum inclusive value.");
        }

        return minInclusive + (_random.NextDouble() * (maxExclusive - minInclusive));
    }
}
