using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Physics;

public interface IAerodynamicsCalculator
{
    double CalculateDynamicPressure(Density airDensity, Speed airspeed);

    Force CalculateLift(Density airDensity, Speed airspeed, Area referenceArea, double liftCoefficient);

    Force CalculateDrag(Density airDensity, Speed airspeed, Area referenceArea, double dragCoefficient);
}
