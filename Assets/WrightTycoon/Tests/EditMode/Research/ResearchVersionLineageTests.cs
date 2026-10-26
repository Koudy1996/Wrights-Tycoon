using System.Collections.Generic;
using NUnit.Framework;
using WrightTycoon.Core.Research;

namespace WrightTycoon.Core.Tests.Research;

[TestFixture]
public sealed class ResearchVersionLineageTests
{
    [Test]
    public void ResolveRootSubjectId_GroupsNestedVersionsUnderOriginalType()
    {
        var sources = new Dictionary<string, string>
        {
            ["type.original"] = string.Empty,
            ["type.version.1"] = "type.original",
            ["type.version.2"] = "type.version.1",
        };

        Assert.That(
            ResearchVersionLineage.ResolveRootSubjectId("type.version.2", sources),
            Is.EqualTo("type.original"));
    }

    [TestCase("type.orphan", "type.missing")]
    [TestCase("type.cycle.a", "type.cycle.b")]
    [Category("Extended")]
    public void ResolveRootSubjectId_KeepsInvalidLegacyLineageVisible(string subjectId, string sourceId)
    {
        var sources = new Dictionary<string, string>
        {
            [subjectId] = sourceId,
            ["type.cycle.b"] = "type.cycle.a",
        };

        Assert.That(
            ResearchVersionLineage.ResolveRootSubjectId(subjectId, sources),
            Is.EqualTo(subjectId));
    }
}
