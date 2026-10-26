using NUnit.Framework;
using WrightTycoon.Core.Company;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Tests.EditMode.Company;

public sealed class CompanyReputationTests
{
    private static FleetMonthReport Report(int active = 100, int warranty = 100, int serviced = 0, int missed = 0,
        double expectedLosses = 0, int actualLosses = 0) =>
        new(1, expectedLosses * 100000, serviced + missed, serviced, missed, missed, 0, actualLosses, 0, missed, 0)
        {
            ActiveAircraftAtStart = active, WarrantyAircraft = warranty, WarrantyServicedAircraft = serviced,
            WarrantyMissedAircraft = missed, ExpectedAccidentLosses = expectedLosses,
        };

    [Test]
    public void UniqueWarrantyServiceRewardAndFailurePenaltyScaleAgainstWholeFleet()
    {
        var successful = new CompanyReputation(); successful.ProcessMonth(1, Report(serviced: 100));
        Assert.That(successful.Score, Is.EqualTo(50.25));
        var failed = new CompanyReputation(); failed.ProcessMonth(1, Report(missed: 100));
        Assert.That(failed.Score, Is.EqualTo(47));
        var sparse = new CompanyReputation(); sparse.ProcessMonth(1, Report(active: 1000, warranty: 1, missed: 1));
        Assert.That(sparse.Score, Is.EqualTo(49.997).Within(1e-9));
    }

    [Test]
    public void NoEventOrExpiredWarrantyDoesNotRewardSupportOrInvoices()
    {
        var reputation = new CompanyReputation();
        reputation.ProcessMonth(1, Report());
        reputation.ProcessMonth(2, Report(warranty: 0) with { CompletedServices = 1000, Revenue = 1e9 });
        Assert.That(reputation.Score, Is.EqualTo(50));
    }

    [Test]
    public void DuplicateMonthAndSaveReplayDoNotChangeTrustTwice()
    {
        var reputation = new CompanyReputation(); reputation.ProcessMonth(1, Report(serviced: 100));
        var saved = reputation.CreateSnapshot();
        reputation.ProcessMonth(1, Report(missed: 100));
        Assert.That(reputation.Score, Is.EqualTo(saved.Score));
        var restored = new CompanyReputation(); restored.RestoreSnapshot(saved);
        restored.ProcessMonth(1, Report(missed: 100));
        Assert.That(restored.Score, Is.EqualTo(saved.Score));
        Assert.That(restored.PriceMultiplier, Is.EqualTo(reputation.PriceMultiplier));
    }

    [Test]
    public void SafetyBetterThanHistoricalBaselineImprovesAndWorseHarmsReputation()
    {
        var safer = new CompanyReputation(); safer.ProcessMonth(1, Report(expectedLosses: 10, actualLosses: 5));
        var worse = new CompanyReputation(); worse.ProcessMonth(1, Report(expectedLosses: 10, actualLosses: 15));
        var matching = new CompanyReputation(); matching.ProcessMonth(1, Report(expectedLosses: 10, actualLosses: 10));
        Assert.That(safer.Score, Is.GreaterThan(50));
        Assert.That(worse.Score, Is.LessThan(50));
        Assert.That(matching.Score, Is.EqualTo(50));
        Assert.That(safer.Score - 50, Is.EqualTo(50 - worse.Score).Within(1e-9));
    }

    [Test]
    public void RollingSafetyEvidenceSurvivesSaveAndIdleMonthsDoNotFarmRewards()
    {
        var reputation = new CompanyReputation(); reputation.ProcessMonth(1, Report(expectedLosses: 2, actualLosses: 0));
        var restored = new CompanyReputation(); restored.RestoreSnapshot(reputation.CreateSnapshot());
        reputation.ProcessMonth(2, Report(expectedLosses: 3, actualLosses: 10));
        restored.ProcessMonth(2, Report(expectedLosses: 3, actualLosses: 10));
        Assert.That(restored.Score, Is.EqualTo(reputation.Score));
        Assert.That(restored.CreateSnapshot().AccidentHistory, Is.EqualTo(reputation.CreateSnapshot().AccidentHistory));
        var before = restored.Score; restored.ProcessMonth(3, Report());
        Assert.That(restored.Score, Is.EqualTo(before));
        restored.ProcessMonth(30, Report(expectedLosses: 10, actualLosses: 10));
        Assert.That(restored.Score, Is.EqualTo(before), "Expired evidence cannot penalize an unrelated later period.");
    }

    [Test]
    public void RareTinyFleetLuckHasNegligibleRewardAndSingleAccidentHasBoundedImpact()
    {
        var reputation = new CompanyReputation();
        for (var month = 1; month <= 120; month++) reputation.ProcessMonth(month, Report(expectedLosses: .00001));
        Assert.That(reputation.Score - 50, Is.LessThan(.001));
        var before = reputation.Score;
        reputation.ProcessMonth(121, Report(expectedLosses: .00001, actualLosses: 1));
        Assert.That(reputation.Score, Is.InRange(before - .5, before));
    }
}
