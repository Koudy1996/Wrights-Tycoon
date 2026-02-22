using WrightTycoon.Core.Math;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Physics;

public sealed class AerodynamicsCalculator : IAerodynamicsCalculator
{
    public double CalculateDynamicPressure(Density airDensity, Speed airspeed)
    {
        var speedSquared = airspeed.MetersPerSecond * airspeed.MetersPerSecond;
        // q = 0.5 * rho * V^2
        return 0.5d * airDensity.KilogramsPerCubicMeter * speedSquared;
    }

    public Force CalculateLift(Density airDensity, Speed airspeed, Area referenceArea, double liftCoefficient)
    {
        Numeric.ThrowIfNotFinite(liftCoefficient, nameof(liftCoefficient));
        return CalculateForce(airDensity, airspeed, referenceArea, liftCoefficient);
    }

    public Force CalculateDrag(Density airDensity, Speed airspeed, Area referenceArea, double dragCoefficient)
    {
        Numeric.ThrowIfNegative(dragCoefficient, nameof(dragCoefficient));
        return CalculateForce(airDensity, airspeed, referenceArea, dragCoefficient);
    }

    private static Force CalculateForce(Density airDensity, Speed airspeed, Area referenceArea, double coefficient)
    {
        var speedSquared = airspeed.MetersPerSecond * airspeed.MetersPerSecond;
        // F = q * S * C where C is Cl or Cd depending on the caller.
        var magnitude = 0.5d * airDensity.KilogramsPerCubicMeter * speedSquared * referenceArea.SquareMeters * coefficient;
        return Force.FromNewtons(magnitude);
    }
}
