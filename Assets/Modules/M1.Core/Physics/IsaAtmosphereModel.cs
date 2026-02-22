using System;
using System.Collections.Generic;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Physics;

public sealed class IsaAtmosphereModel : IAtmosphereModel
{
    private readonly IReadOnlyList<AtmosphereSample> _evaluationSamples;

    public IsaAtmosphereModel()
    {
        SeaLevelDensity = Density.FromKilogramsPerCubicMeter(CoreBalance.Atmosphere.SeaLevelDensityKilogramsPerCubicMeter);
        _evaluationSamples = BuildEvaluationSamples();
    }

    public Density SeaLevelDensity { get; }

    public Density GetDensityAtAltitude(double altitudeMeters)
    {
        Numeric.ThrowIfNotFinite(altitudeMeters, nameof(altitudeMeters));

        var clampedAltitude = System.Math.Max(0d, altitudeMeters);
        // Simplified ISA approximation for tycoon-level performance modeling.
        var densityBase = System.Math.Max(0d, 1d - (CoreBalance.Atmosphere.DensityGradient * clampedAltitude));
        var densityValue = SeaLevelDensity.KilogramsPerCubicMeter * System.Math.Pow(densityBase, CoreBalance.Atmosphere.DensityExponent);

        return Density.FromKilogramsPerCubicMeter(densityValue);
    }

    public double GetDensityRatioAtAltitude(double altitudeMeters)
    {
        var density = GetDensityAtAltitude(altitudeMeters);
        return density.KilogramsPerCubicMeter / SeaLevelDensity.KilogramsPerCubicMeter;
    }

    public IReadOnlyList<AtmosphereSample> GetEvaluationSamples() => _evaluationSamples;

    private IReadOnlyList<AtmosphereSample> BuildEvaluationSamples()
    {
        var altitudes = CoreBalance.Atmosphere.EvaluationAltitudesMeters;
        var samples = new AtmosphereSample[altitudes.Length];
        for (var index = 0; index < altitudes.Length; index++)
        {
            var altitude = altitudes[index];
            var density = GetDensityAtAltitude(altitude);
            var ratio = density.KilogramsPerCubicMeter / SeaLevelDensity.KilogramsPerCubicMeter;
            samples[index] = new AtmosphereSample(altitude, density, ratio);
        }

        return Array.AsReadOnly(samples);
    }
}
