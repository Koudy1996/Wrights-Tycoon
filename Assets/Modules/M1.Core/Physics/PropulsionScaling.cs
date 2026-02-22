using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Physics;

public static class PropulsionScaling
{
    public static double ScaleByDensityRatio(double seaLevelOutput, double densityRatio)
    {
        Numeric.ThrowIfNegative(seaLevelOutput, nameof(seaLevelOutput));
        Numeric.ThrowIfNegative(densityRatio, nameof(densityRatio));
        // Simplified assumption: available output scales linearly with air density ratio.
        return seaLevelOutput * densityRatio;
    }
}
