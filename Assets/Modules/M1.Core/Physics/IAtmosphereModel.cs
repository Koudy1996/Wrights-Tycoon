using System.Collections.Generic;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Physics;

public interface IAtmosphereModel
{
    Density SeaLevelDensity { get; }

    Density GetDensityAtAltitude(double altitudeMeters);

    double GetDensityRatioAtAltitude(double altitudeMeters);

    IReadOnlyList<AtmosphereSample> GetEvaluationSamples();
}
