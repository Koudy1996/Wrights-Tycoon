using NUnit.Framework;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Tests.Physics;

[TestFixture]
public sealed class AerodynamicsCalculatorTests
{
    [Test]
    public void CalculateLiftAndDrag_UsesStandardAerodynamicEquations()
    {
        var calculator = new AerodynamicsCalculator();
        var airDensity = Density.FromKilogramsPerCubicMeter(1.225d);
        var speed = Speed.FromMetersPerSecond(70d);
        var area = Area.FromSquareMeters(16d);

        var lift = calculator.CalculateLift(airDensity, speed, area, liftCoefficient: 1.0d);
        var drag = calculator.CalculateDrag(airDensity, speed, area, dragCoefficient: 0.03d);

        Assert.That(lift.Newtons, Is.EqualTo(48020d).Within(1e-6));
        Assert.That(drag.Newtons, Is.EqualTo(1440.6d).Within(1e-6));
    }

    [Test]
    public void PropulsionScaling_ScalesOutputByDensityRatio()
    {
        var seaLevelPower = 1200d;
        var densityRatio = 0.6d;

        var scaledPower = PropulsionScaling.ScaleByDensityRatio(seaLevelPower, densityRatio);
        Assert.That(scaledPower, Is.EqualTo(720d).Within(1e-9));
    }
}
