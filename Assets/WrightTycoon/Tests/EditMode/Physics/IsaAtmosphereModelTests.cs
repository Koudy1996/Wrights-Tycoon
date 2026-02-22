using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Physics;

namespace WrightTycoon.Core.Tests.Physics;

[TestFixture]
public sealed class IsaAtmosphereModelTests
{
    [Test]
    public void GetDensityAtAltitude_ReturnsExpectedCheckpointValues()
    {
        var model = new IsaAtmosphereModel();

        Assert.That(model.GetDensityAtAltitude(0d).KilogramsPerCubicMeter, Is.EqualTo(1.225d).Within(1e-9));
        Assert.That(model.GetDensityAtAltitude(5000d).KilogramsPerCubicMeter, Is.EqualTo(0.7361048721d).Within(1e-6));
        Assert.That(model.GetDensityAtAltitude(10000d).KilogramsPerCubicMeter, Is.EqualTo(0.4126933684d).Within(1e-6));
    }

    [Test]
    public void GetDensityRatioAtAltitude_DecreasesMonotonicallyAcrossDesignAltitudes()
    {
        var model = new IsaAtmosphereModel();

        var atSeaLevel = model.GetDensityRatioAtAltitude(0d);
        var atFiveKm = model.GetDensityRatioAtAltitude(5000d);
        var atTenKm = model.GetDensityRatioAtAltitude(10000d);

        Assert.That(atSeaLevel, Is.GreaterThan(atFiveKm));
        Assert.That(atFiveKm, Is.GreaterThan(atTenKm));
    }

    [Test]
    public void GetEvaluationSamples_ContainsRequiredAltitudes()
    {
        var model = new IsaAtmosphereModel();

        var altitudes = model.GetEvaluationSamples().Select(sample => sample.AltitudeMeters).ToArray();
        Assert.That(altitudes, Is.EqualTo(new[] { 0d, 5000d, 10000d }));
    }
}
