using System.Reflection;
using NUnit.Framework;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Research;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

[Category("Extended")]
public sealed class ResearchApprovalUiTests
{
    [TestCase(ResearchSubjectType.Engine)]
    [TestCase(ResearchSubjectType.AircraftType)]
    [TestCase(ResearchSubjectType.AircraftFamily)]
    public void DisplayedFinalMonthsMatchActualApprovalAcrossBonusBoundary(ResearchSubjectType subject)
    {
        foreach (var bookingMonth in new[] { 1, ResearchBalance.FirstYearApprovalSpeedBonusWindowMonths,
                     ResearchBalance.FirstYearApprovalSpeedBonusWindowMonths + 1 })
        {
            foreach (var quotedMonths in new[] { 1, 3, 17 })
            {
                var board = new ResearchBoard();
                board.UpsertProposal(subject, "test", "Test", new ResearchQuote(100, quotedMonths, 0.5));
                var displayed = CalculateMonths(quotedMonths, bookingMonth);
                Assert.That(board.TryApproveProposal(subject, "test", bookingMonth, out var approved, out _), Is.True);
                Assert.That(displayed, Is.EqualTo(approved!.ResearchTimeMonths));
            }
        }
    }

    [Test]
    public void ChangedModifierOrFacilityQuoteUsesItsCurrentFinalMonths()
    {
        var bookingMonth = ResearchBalance.FirstYearApprovalSpeedBonusWindowMonths + 1;
        Assert.That(CalculateMonths(18, bookingMonth), Is.EqualTo(18));
        Assert.That(CalculateMonths(9, bookingMonth), Is.EqualTo(9));
        Assert.That(CalculateMonths(6, bookingMonth), Is.EqualTo(6));
    }

    [TestCase(24f)]
    [TestCase(32f)]
    [TestCase(40f)]
    public void FinalTimeRowIsTwiceTheStandardMetricHeight(float standardHeight)
    {
        var method = typeof(WrightTycoonLaunchRunner).GetMethod("CalculateResearchApprovalRowHeight",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.That((float)method.Invoke(null, new object[] { standardHeight })!, Is.EqualTo(standardHeight * 2));
    }

    private static int CalculateMonths(int quotedMonths, int bookingMonth)
    {
        var method = typeof(WrightTycoonLaunchRunner).GetMethod("CalculateResearchApprovalMonths",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (int)method.Invoke(null, new object[] { quotedMonths, bookingMonth })!;
    }
}
