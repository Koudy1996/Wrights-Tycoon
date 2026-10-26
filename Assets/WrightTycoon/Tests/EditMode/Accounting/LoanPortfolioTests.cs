using System;
using NUnit.Framework;
using WrightTycoon.Core.Accounting;

namespace WrightTycoon.Core.Tests.Accounting;

[TestFixture]
public sealed class LoanPortfolioTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void LoanCountDoesNotRestrictStandardOrFactoryFinancingButCreditStillDoes(bool factoryFinancing)
    {
        var portfolio = new LoanPortfolio();
        var context = CreateContext();
        for (var index = 0; index < 12; index++)
        {
            var assessment = portfolio.CalculateCreditAssessment(context);
            Assert.That(assessment.StandardFinancingAvailable, Is.True, $"Loan number {index + 1}");
            LoanOffer? offer;
            string message;
            var available = factoryFinancing
                ? portfolio.TryBuildFactoryConstructionOffer(LoanBalance.MinimumPrincipal, assessment, out offer, out message)
                : portfolio.TryBuildStandardOffer(LoanBalance.MinimumPrincipal, 60, LoanRepaymentMode.Amortized, assessment, out offer, out message);
            Assert.That(available, Is.True, message);
            if (factoryFinancing) portfolio.AcceptFactoryConstructionOffer(offer!, 0, 18);
            else portfolio.AcceptStandardOffer(offer!, 0);
        }
        Assert.That(portfolio.ActiveLoans.Count, Is.EqualTo(12));
        var restored = new LoanPortfolio();
        restored.RestoreSnapshot(portfolio.CreateSnapshot());
        Assert.That(restored.ActiveLoans.Count, Is.EqualTo(12));
        var nextAssessment = restored.CalculateCreditAssessment(context);
        Assert.That(nextAssessment.StandardFinancingAvailable, Is.True);
        Assert.That(nextAssessment.OutstandingPrincipal, Is.EqualTo(12 * LoanBalance.MinimumPrincipal));
        Assert.That(restored.TryBuildStandardOffer(LoanBalance.MaximumCreditLimit + LoanBalance.PrincipalIncrement,
            60, LoanRepaymentMode.Amortized, nextAssessment, out _, out _), Is.False,
            "Removing the loan-count limit must not remove the financial credit limit.");
    }

    [Test]
    public void StandardRepaymentModes_UseTheSameLimitsRateFeeAndTerms()
    {
        var portfolio = new LoanPortfolio();
        var assessment = portfolio.CalculateCreditAssessment(CreateContext());

        Assert.That(assessment.Rating, Is.EqualTo(CompanyCreditRating.A));
        Assert.That(portfolio.TryBuildStandardOffer(100_000_000d, 60, LoanRepaymentMode.Amortized, assessment, out var amortized, out _), Is.True);
        Assert.That(portfolio.TryBuildStandardOffer(100_000_000d, 60, LoanRepaymentMode.InterestOnly, assessment, out var interestOnly, out _), Is.True);
        Assert.That(amortized, Is.Not.Null);
        Assert.That(interestOnly, Is.Not.Null);
        Assert.That(interestOnly!.AnnualInterestRate, Is.EqualTo(amortized!.AnnualInterestRate));
        Assert.That(interestOnly.OriginationFee, Is.EqualTo(amortized.OriginationFee));
        Assert.That(interestOnly.TermMonths, Is.EqualTo(amortized.TermMonths));
        Assert.That(interestOnly.BalloonPayment, Is.EqualTo(100_000_000d));
        Assert.That(amortized.BalloonPayment, Is.Zero);
    }

    [Test]
    public void AmortizedLoan_ClosesAtTheEndWithoutRoundingResidue()
    {
        var portfolio = new LoanPortfolio();
        var loan = Accept(portfolio, LoanRepaymentMode.Amortized, 24);
        var totalPrincipal = 0d;

        for (var month = 1; month <= 24; month++)
        {
            var payments = portfolio.ProcessMonth(month);
            Assert.That(payments, Has.Count.EqualTo(1));
            totalPrincipal += payments[0].Principal;
        }

        Assert.That(loan.IsClosed, Is.True);
        Assert.That(loan.OutstandingPrincipal, Is.Zero);
        Assert.That(totalPrincipal, Is.EqualTo(loan.OriginalPrincipal).Within(0.02d));
    }

    [Test]
    public void InterestOnlyLoan_PaysOnlyInterestUntilFinalBalloon()
    {
        var portfolio = new LoanPortfolio();
        var loan = Accept(portfolio, LoanRepaymentMode.InterestOnly, 24);

        var first = portfolio.ProcessMonth(1)[0];
        Assert.That(first.Principal, Is.Zero);
        Assert.That(first.Interest, Is.GreaterThan(0d));

        for (var month = 2; month < 24; month++) portfolio.ProcessMonth(month);
        var final = portfolio.ProcessMonth(24)[0];
        Assert.That(final.Principal, Is.EqualTo(loan.OriginalPrincipal));
        Assert.That(loan.IsClosed, Is.True);
    }

    [Test]
    public void PartialPrepayment_CanReducePaymentOrShortenTerm()
    {
        var reducePortfolio = new LoanPortfolio();
        var reduceLoan = Accept(reducePortfolio, LoanRepaymentMode.Amortized, 60);
        var oldPayment = reduceLoan.ScheduledPayment;
        Assert.That(reducePortfolio.TryBuildPrepaymentQuote(reduceLoan.LoanId, 20_000_000d, out var reduceQuote, out _), Is.True);
        Assert.That(reducePortfolio.ApplyPrepayment(reduceQuote!, LoanPrepaymentStrategy.ReducePayment, 0, out _), Is.True);
        Assert.That(reduceLoan.ScheduledPayment, Is.LessThan(oldPayment));
        Assert.That(reduceQuote!.Fee, Is.EqualTo(200_000d));

        var shortenPortfolio = new LoanPortfolio();
        var shortenLoan = Accept(shortenPortfolio, LoanRepaymentMode.Amortized, 60);
        Assert.That(shortenPortfolio.TryBuildPrepaymentQuote(shortenLoan.LoanId, 20_000_000d, out var shortenQuote, out _), Is.True);
        Assert.That(shortenPortfolio.ApplyPrepayment(shortenQuote!, LoanPrepaymentStrategy.ShortenTerm, 0, out _), Is.True);
        Assert.That(shortenLoan.RemainingMonths, Is.LessThan(60));
    }

    [Test]
    public void EmergencyLoan_IsOfferedOnlyOnceAndSnapshotPreventsDuplicatePayment()
    {
        var portfolio = new LoanPortfolio();
        Assert.That(portfolio.TryBuildEmergencyOffer(1_000_000d, 10_000_000d, out var offer, out _), Is.True);
        Assert.That(offer!.CashProceeds, Is.EqualTo(31_000_000d));
        var loan = portfolio.AcceptEmergencyOffer(offer, 5);
        Assert.That(portfolio.TryBuildEmergencyOffer(1_000_000d, 0d, out _, out _), Is.False);

        var firstPayment = portfolio.ProcessMonth(6);
        Assert.That(firstPayment, Has.Count.EqualTo(1));
        Assert.That(firstPayment[0].Principal, Is.Zero);

        var restored = new LoanPortfolio();
        restored.RestoreSnapshot(portfolio.CreateSnapshot());
        Assert.That(restored.ProcessMonth(6), Is.Empty);
        Assert.That(restored.ActiveLoans[0].LoanId, Is.EqualTo(loan.LoanId));
    }

    private static LoanState Accept(LoanPortfolio portfolio, LoanRepaymentMode mode, int term)
    {
        var assessment = portfolio.CalculateCreditAssessment(CreateContext());
        Assert.That(portfolio.TryBuildStandardOffer(100_000_000d, term, mode, assessment, out var offer, out _), Is.True);
        return portfolio.AcceptStandardOffer(offer!, 0);
    }

    private static LoanFinancialContext CreateContext() => new(
        Cash: 500_000_000d,
        InventoryValue: 0d,
        FactoryResaleValue: 0d,
        EquipmentScrapValue: 0d,
        AccountingHistory: Array.Empty<MonthlyAccountingSnapshot>(),
        CurrentMonthIndex: 0);
}
