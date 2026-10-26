using NUnit.Framework;
using WrightTycoon.Core.Research;

namespace WrightTycoon.Core.Tests.Research;

[TestFixture]
public sealed class ResearchEditPolicyTests
{
    [TestCase(ResearchStatus.Proposed, true)]
    [TestCase(ResearchStatus.Ongoing, false)]
    [TestCase(ResearchStatus.Completed, false)]
    public void CanEdit_UsesResearchStatusWhenProjectExists(ResearchStatus status, bool expected)
    {
        Assert.That(ResearchEditPolicy.CanEdit(status, hasApprovalMetadata: false), Is.EqualTo(expected));
    }

    [Test]
    [Category("Extended")]
    public void CanEdit_FallsBackToApprovalMetadataForLegacyRecordWithoutProject()
    {
        Assert.That(ResearchEditPolicy.CanEdit(status: null, hasApprovalMetadata: false), Is.True);
        Assert.That(ResearchEditPolicy.CanEdit(status: null, hasApprovalMetadata: true), Is.False);
    }
}
