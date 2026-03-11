using NUnit.Framework;
using WrightTycoon.Core.Research;

namespace WrightTycoon.Core.Tests.Research;

[TestFixture]
public sealed class ResearchBoardTests
{
    [Test]
    public void ApproveProposal_InFirstYear_AppliesTwentyMonthSpeedBonus()
    {
        var board = new ResearchBoard();
        var quote = new ResearchQuote(1000d, 30, 0.5d);
        board.UpsertProposal(
            ResearchSubjectType.Engine,
            "eng.test",
            "Test Engine",
            quote);

        var approved = board.TryApproveProposal(
            ResearchSubjectType.Engine,
            "eng.test",
            expenseMonthIndex: 12,
            out var approvedProject,
            out var message);

        Assert.That(approved, Is.True);
        Assert.That(approvedProject, Is.Not.Null);
        Assert.That(approvedProject!.ResearchTimeMonths, Is.EqualTo(10));
        Assert.That(message, Does.Contain("First-year speed bonus applied."));
    }

    [Test]
    public void ApproveProposal_AfterFirstYear_DoesNotApplySpeedBonus()
    {
        var board = new ResearchBoard();
        var quote = new ResearchQuote(1000d, 30, 0.5d);
        board.UpsertProposal(
            ResearchSubjectType.Engine,
            "eng.test",
            "Test Engine",
            quote);

        var approved = board.TryApproveProposal(
            ResearchSubjectType.Engine,
            "eng.test",
            expenseMonthIndex: 13,
            out var approvedProject,
            out _);

        Assert.That(approved, Is.True);
        Assert.That(approvedProject, Is.Not.Null);
        Assert.That(approvedProject!.ResearchTimeMonths, Is.EqualTo(30));
    }

    [Test]
    public void ApproveProposal_InFirstYear_NeverDropsBelowOneMonth()
    {
        var board = new ResearchBoard();
        var quote = new ResearchQuote(1000d, 8, 0.5d);
        board.UpsertProposal(
            ResearchSubjectType.Engine,
            "eng.fast",
            "Fast Engine",
            quote);

        var approved = board.TryApproveProposal(
            ResearchSubjectType.Engine,
            "eng.fast",
            expenseMonthIndex: 2,
            out var approvedProject,
            out _);

        Assert.That(approved, Is.True);
        Assert.That(approvedProject, Is.Not.Null);
        Assert.That(approvedProject!.ResearchTimeMonths, Is.EqualTo(1));
    }
}
