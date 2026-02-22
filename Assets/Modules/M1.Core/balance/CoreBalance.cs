namespace WrightTycoon.Core.Balance;

// Core simulation constants shared by all pure-domain modules.
public static class CoreBalance
{
    public static class Atmosphere
    {
        public const double SeaLevelDensityKilogramsPerCubicMeter = 1.225d;
        public const double DensityGradient = 0.0000225577d;
        public const double DensityExponent = 4.256d;

        // Standard design/evaluation altitudes used by M1-M6.
        public static readonly double[] EvaluationAltitudesMeters = { 0d, 5000d, 10000d };
    }
}
