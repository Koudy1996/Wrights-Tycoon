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

    [Test]
    public void CancelProposal_WhenStillProposed_RemovesProjectFromBoard()
    {
        var board = new ResearchBoard();
        var quote = new ResearchQuote(2000d, 18, 0.4d);
        board.UpsertProposal(
            ResearchSubjectType.AircraftType,
            "type.sample",
            "Sample Type",
            quote);

        var canceled = board.TryCancelProposal(
            ResearchSubjectType.AircraftType,
            "type.sample",
            out var canceledProject,
            out var message);

        Assert.That(canceled, Is.True);
        Assert.That(canceledProject, Is.Not.Null);
        Assert.That(board.GetProjectsByStatus(ResearchStatus.Proposed), Is.Empty);
        Assert.That(message, Does.Contain("Canceled proposal"));
    }

    [Test]
    public void CancelProposal_WhenAlreadyApproved_ReturnsFalse()
    {
        var board = new ResearchBoard();
        var quote = new ResearchQuote(2000d, 18, 0.4d);
        board.UpsertProposal(
            ResearchSubjectType.AircraftType,
            "type.sample",
            "Sample Type",
            quote);

        board.TryApproveProposal(
            ResearchSubjectType.AircraftType,
            "type.sample",
            expenseMonthIndex: 20,
            out _,
            out _);

        var canceled = board.TryCancelProposal(
            ResearchSubjectType.AircraftType,
            "type.sample",
            out _,
            out var message);

        Assert.That(canceled, Is.False);
        Assert.That(message, Does.Contain("cannot be canceled"));
    }
}
