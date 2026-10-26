using NUnit.Framework;
using WrightTycoon.Core.Aircraft;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class WingAreaPayloadOptimizerTests
{
    [Test]
    public void FindsSmallestWingAreaThatStillMeetsSelectedPayload()
    {
        var result = WingAreaPayloadOptimizer.FindSmallestMeetingPayload(
            minimumWingArea: 10d,
            maximumWingArea: 30d,
            targetPayloadKilograms: 1_235d,
            area => new WingAreaPayloadEvaluation(true, area * 100d));

        Assert.That(result.Success, Is.True);
        Assert.That(result.WingAreaSquareMeters, Is.EqualTo(12.4d).Within(1e-9d));
        Assert.That(result.PayloadKilograms, Is.GreaterThanOrEqualTo(1_235d));
        Assert.That((result.WingAreaSquareMeters - 0.1d) * 100d, Is.LessThan(1_235d));
    }

    [Test]
    public void ReturnsFailureWhenPayloadCannotBeReached()
    {
        var result = WingAreaPayloadOptimizer.FindSmallestMeetingPayload(
            10d,
            20d,
            5_000d,
            area => new WingAreaPayloadEvaluation(true, area * 100d));

        Assert.That(result.Success, Is.False);
    }

    [Test]
    [Category("Extended")]
    public void SkipsInvalidAircraftCandidatesAndPreservesUsefulError()
    {
        var result = WingAreaPayloadOptimizer.FindSmallestMeetingPayload(
            10d,
            20d,
            1_500d,
            area => area < 15d
                ? new WingAreaPayloadEvaluation(false, 0d, "Candidate is outside the family envelope.")
                : new WingAreaPayloadEvaluation(true, area * 100d));

        Assert.That(result.Success, Is.True);
        Assert.That(result.WingAreaSquareMeters, Is.EqualTo(15d).Within(1e-9d));
    }
}
