using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Physics;

public readonly record struct AtmosphereSample(
    double AltitudeMeters,
    Density AirDensity,
    double DensityRatio);
