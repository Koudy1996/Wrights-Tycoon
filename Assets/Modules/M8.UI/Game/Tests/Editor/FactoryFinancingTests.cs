using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

public sealed class FactoryFinancingTests
{
    [Test]
    public void DeferredLoanUsesCalendarAgeForPrepaymentWithoutAccruingInterestOrUsingRepaymentMonths()
    {
        var portfolio = new LoanPortfolio();
        var assessment = portfolio.CalculateCreditAssessment(new LoanFinancialContext(1_000_000_000d, 0, 0, 0, System.Array.Empty<MonthlyAccountingSnapshot>(), 7));
        portfolio.TryBuildFactoryConstructionOffer(72_000_000d, assessment, out var offer, out _);
        var loan = portfolio.AcceptFactoryConstructionOffer(offer!, 7, 48);
        Assert.That(portfolio.ProcessMonth(20), Is.Empty);
        Assert.That(loan.MonthsSinceOrigination, Is.EqualTo(13));
        Assert.That(loan.RemainingMonths, Is.EqualTo(120));
        Assert.That(loan.TotalInterestPaid, Is.Zero);
        Assert.That(portfolio.GetNextScheduledDebtService(21), Is.Zero);
        Assert.That(portfolio.TryBuildPrepaymentQuote(loan.LoanId, 1_000_000d, out var prepayment, out _), Is.True);
        Assert.That(prepayment!.Fee, Is.Zero);
    }

    [Test]
    public void OldLoanSnapshotsKeepOriginalDueDatesAndInferCalendarOrigination()
    {
        var portfolio = new LoanPortfolio();
        var snapshot = new LoanPortfolioSnapshot();
        snapshot.Loans.Add(new LoanStateSnapshot { LoanId = "legacy.loan", OriginalPrincipal = 72_000_000d,
            OutstandingPrincipal = 70_000_000d, AnnualInterestRate = .055, OriginalTermMonths = 120, RemainingMonths = 118,
            MonthsSinceOrigination = 2, NextDueMonthIndex = 10, LastProcessedMonthIndex = 9, ScheduledPayment = 800_000d });
        portfolio.RestoreSnapshot(snapshot);
        Assert.That(portfolio.ActiveLoans[0].OriginationMonthIndex, Is.EqualTo(7));
        Assert.That(portfolio.ActiveLoans[0].NextDueMonthIndex, Is.EqualTo(10));
        Assert.That(portfolio.ProcessMonth(10).Count, Is.EqualTo(1));
        Assert.That(portfolio.ActiveLoans[0].MonthsSinceOrigination, Is.EqualTo(3));
        Assert.That(portfolio.ActiveLoans[0].RemainingMonths, Is.EqualTo(117));
    }

    [TestCase(false, FactoryBuildingSize.Small)]
    [TestCase(false, FactoryBuildingSize.Medium)]
    [TestCase(false, FactoryBuildingSize.Large)]
    [TestCase(true, FactoryBuildingSize.Small)]
    [TestCase(true, FactoryBuildingSize.Medium)]
    [TestCase(true, FactoryBuildingSize.Large)]
    [Category("Extended")]
    public void FactoryFinancingStartsAtBuildingCompletionThenRunsFullTenYears(bool engine, FactoryBuildingSize size)
    {
        var go = new GameObject("DeferredFactoryLoanTest"); go.SetActive(false);
        var runner = go.AddComponent<WrightTycoonLaunchRunner>();
        try
        {
            var type = typeof(WrightTycoonLaunchRunner);
            var aircraft = new AircraftProductionFactory(); var engines = new EngineManufacturingNetwork();
            var accounting = new AccountingSystem(10_000_000_000d);
            void Set(string name, object value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, value);
            Set("_productionFactory", aircraft); Set("_engineManufacturing", engines); Set("_accountingSystem", accounting); Set("_elapsedMonths", 7);
            Assert.That(type.GetMethod("StartFinancedFactoryConstruction", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(runner, new object[] { engine, size }), Is.True);
            var portfolio = (LoanPortfolio)type.GetField("_loanPortfolio", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runner)!;
            var duration = FactoryBalance.GetConstructionMonths(size);
            var principal = portfolio.OutstandingPrincipal;
            Assert.That(portfolio.ActiveLoans[0].NextDueMonthIndex, Is.EqualTo(7 + duration));
            for (var elapsed = 1; elapsed < duration; elapsed++)
            {
                aircraft.AdvanceMonth(); engines.AdvanceMonth();
                Assert.That(portfolio.ProcessMonth(7 + elapsed), Is.Empty);
                Assert.That(portfolio.OutstandingPrincipal, Is.EqualTo(principal));
                Assert.That(portfolio.ActiveLoans[0].RemainingMonths, Is.EqualTo(120));
                Assert.That(portfolio.ActiveLoans[0].TotalInterestPaid, Is.Zero);
                Assert.That(portfolio.GetNextScheduledDebtService(7 + elapsed), Is.Zero);
                if (elapsed == duration / 2)
                {
                    var restored = new LoanPortfolio(); restored.RestoreSnapshot(portfolio.CreateSnapshot()); portfolio = restored;
                    Assert.That(portfolio.ActiveLoans[0].OriginationMonthIndex, Is.EqualTo(7));
                }
            }
            Assert.That(engine ? engines.Sites[0].IsOperational : aircraft.Factories[0].IsOperational, Is.False);
            aircraft.AdvanceMonth(); engines.AdvanceMonth();
            Assert.That(engine ? engines.Sites[0].IsOperational : aircraft.Factories[0].IsOperational, Is.True);
            Assert.That(portfolio.GetNextScheduledDebtService(7 + duration), Is.GreaterThan(0));
            Assert.That(portfolio.ProcessMonth(7 + duration).Count, Is.EqualTo(1));
            Assert.That(portfolio.ActiveLoans[0].RemainingMonths, Is.EqualTo(119));
            Assert.That(portfolio.ActiveLoans[0].MonthsSinceOrigination, Is.EqualTo(duration));
            Assert.That(portfolio.ProcessMonth(7 + duration), Is.Empty);
            var afterFirst = new LoanPortfolio(); afterFirst.RestoreSnapshot(portfolio.CreateSnapshot());
            for (var installment = 1; installment < 120; installment++)
                Assert.That(afterFirst.ProcessMonth(7 + duration + installment).Count, Is.EqualTo(1));
            Assert.That(afterFirst.OutstandingPrincipal, Is.Zero);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [TestCase(FactoryBuildingSize.Small)]
    [TestCase(FactoryBuildingSize.Medium)]
    [TestCase(FactoryBuildingSize.Large)]
    public void AircraftAndMotorBuildingsGainCollateralDuringConstructionAndRestoreIt(FactoryBuildingSize size)
    {
        var aircraft = new AircraftProductionFactory(); var engines = new EngineManufacturingNetwork();
        aircraft.TryStartFactoryConstruction(size, double.MaxValue, out var cost, out _);
        engines.TryConstruct(size, double.MaxValue, out _, out _);
        var months = FactoryBalance.GetConstructionMonths(size);
        Assert.That(aircraft.GetFactoryResaleValue("factory.001"), Is.Zero);
        for (var m = 0; m < months / 2; m++) { aircraft.AdvanceMonth(); engines.AdvanceMonth(); }
        Assert.That(aircraft.GetFactoryResaleValue("factory.001"), Is.EqualTo(cost * .45).Within(.01));
        Assert.That(engines.Sites[0].BuildingResaleValue, Is.EqualTo(cost * .225).Within(.01));
        var restoredAircraft = new AircraftProductionFactory(); restoredAircraft.RestoreSnapshot(aircraft.CreateSnapshot());
        var restoredEngines = new EngineManufacturingNetwork(); restoredEngines.RestoreSnapshot(engines.CreateSnapshot());
        Assert.That(restoredAircraft.GetFactoryResaleValue("factory.001"), Is.EqualTo(cost * .45).Within(.01));
        Assert.That(restoredEngines.Sites[0].BuildingResaleValue, Is.EqualTo(cost * .225).Within(.01));
        for (var m = months / 2; m < months; m++) { restoredAircraft.AdvanceMonth(); restoredEngines.AdvanceMonth(); }
        Assert.That(restoredAircraft.GetFactoryResaleValue("factory.001"), Is.EqualTo(cost * .9).Within(.01));
        Assert.That(restoredEngines.Sites[0].BuildingResaleValue, Is.EqualTo(cost * .45).Within(.01));
        Assert.That(restoredAircraft.TrySellFactory("factory.001", out var sale, out _), Is.True);
        Assert.That(sale, Is.EqualTo(cost * .9).Within(.01));
        for (var m = 0; m < 12; m++) restoredEngines.AdvanceMonth();
        var aged = new EngineManufacturingNetwork(); aged.RestoreSnapshot(restoredEngines.CreateSnapshot());
        Assert.That(aged.Sites[0].OperatingMonths, Is.EqualTo(12));
        Assert.That(aged.Sites[0].BuildingResaleValue, Is.EqualTo(cost * .445).Within(.01));
    }

    [Test]
    public void ExpansionValueIncludesCompletedBuildingAndOnlyFinishedFractionOfExtension()
    {
        var aircraft = new AircraftProductionFactory(); var engines = new EngineManufacturingNetwork();
        aircraft.TryStartFactoryConstruction(double.MaxValue, out _, out _); engines.TryConstruct(double.MaxValue, out _, out _);
        for (var m = 0; m < 18; m++) { aircraft.AdvanceMonth(); engines.AdvanceMonth(); }
        aircraft.TryStartFactoryExpansion("factory.001", double.MaxValue, out _, out _);
        engines.TryExpand("engine-plant.001", double.MaxValue, out _, out _);
        Assert.That(aircraft.GetFactoryResaleValue("factory.001"), Is.EqualTo(72_000_000d));
        for (var m = 0; m < 6; m++) { aircraft.AdvanceMonth(); engines.AdvanceMonth(); }
        Assert.That(aircraft.GetFactoryResaleValue("factory.001"), Is.EqualTo(144_000_000d));
        Assert.That(engines.Sites[0].BuildingResaleValue, Is.EqualTo(72_000_000d));
    }

    [Test]
    public void LoanContextIncludesBothNetworksAndMotorEquipmentDuringInstallation()
    {
        var go = new GameObject("FactoryCollateralTest"); go.SetActive(false);
        var runner = go.AddComponent<WrightTycoonLaunchRunner>();
        try
        {
            var aircraft = new AircraftProductionFactory(); var engines = new EngineManufacturingNetwork();
            aircraft.TryStartFactoryConstruction(double.MaxValue, out _, out _); engines.TryConstruct(double.MaxValue, out _, out _);
            for (var m = 0; m < 9; m++) { aircraft.AdvanceMonth(); engines.AdvanceMonth(); }
            engines.Sites[0].EquipmentCost = 10_000_000d; engines.Sites[0].InstallationMonthsRemaining = 2;
            var type = typeof(WrightTycoonLaunchRunner);
            type.GetField("_productionFactory", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, aircraft);
            type.GetField("_engineManufacturing", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, engines);
            type.GetField("_accountingSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, new AccountingSystem(100_000_000d));
            var context = (LoanFinancialContext)type.GetMethod("BuildLoanFinancialContext", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(runner, null)!;
            Assert.That(context.FactoryResaleValue, Is.EqualTo(54_000_000d));
            Assert.That(context.EquipmentScrapValue, Is.EqualTo(1_500_000d));
            var assessment = new LoanPortfolio().CalculateCreditAssessment(context);
            Assert.That(assessment.CreditLimit, Is.EqualTo(150_000_000d + 25_000_000d + .8 * 54_000_000d + 1_500_000d));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [TestCase(CompanyCreditRating.A)]
    [TestCase(CompanyCreditRating.B)]
    [TestCase(CompanyCreditRating.C)]
    [TestCase(CompanyCreditRating.D)]
    [Category("Extended")]
    public void FactoryLoanHasTenYearTermAndTwoPercentagePointDiscountWithoutChangingStandardOffers(CompanyCreditRating rating)
    {
        var portfolio = new LoanPortfolio();
        var context = new LoanFinancialContext(1_000_000_000d, 0, 0, 0, System.Array.Empty<MonthlyAccountingSnapshot>(), 0);
        var assessment = portfolio.CalculateCreditAssessment(context) with { Rating = rating, AnnualInterestRate = LoanBalance.GetBaseAnnualInterestRate(rating) };
        Assert.That(portfolio.TryBuildStandardOffer(72_000_000d, 120, LoanRepaymentMode.Amortized, assessment, out var standard, out _), Is.True);
        Assert.That(portfolio.TryBuildFactoryConstructionOffer(72_000_000d, assessment, out var factory, out _), Is.True);
        Assert.That(factory!.TermMonths, Is.EqualTo(120));
        Assert.That(factory.AnnualInterestRate, Is.EqualTo(standard!.AnnualInterestRate - .02).Within(1e-10));
        Assert.That(factory.ScheduledMonthlyPayment, Is.LessThan(standard.ScheduledMonthlyPayment));
        Assert.That(factory.OriginationFee, Is.EqualTo(standard.OriginationFee));
        var loan = portfolio.AcceptStandardOffer(factory, 0);
        var restored = new LoanPortfolio(); restored.RestoreSnapshot(portfolio.CreateSnapshot());
        Assert.That(restored.ActiveLoans[0].AnnualInterestRate, Is.EqualTo(factory.AnnualInterestRate));
        for (var month = 1; month <= 120; month++) restored.ProcessMonth(month);
        Assert.That(restored.OutstandingPrincipal, Is.Zero);
        Assert.That(loan.OriginalTermMonths, Is.EqualTo(120));
    }

    [TestCase(1d, FactoryBuildingSize.Small)]
    [TestCase(8_359_999d, FactoryBuildingSize.Small)]
    [TestCase(80_000_000d, FactoryBuildingSize.Large)] // Enough deposit, but insufficient standard credit.
    public void FailedFinancingCreatesNoPlantLoanOrAccountingEntry(double initialCash, FactoryBuildingSize size)
    {
        var go = new GameObject("FactoryFinancingTest"); go.SetActive(false);
        var runner = go.AddComponent<WrightTycoonLaunchRunner>();
        try
        {
            var accounting = new AccountingSystem(initialCash);
            var aircraft = new AircraftProductionFactory();
            var type = typeof(WrightTycoonLaunchRunner);
            type.GetField("_accountingSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, accounting);
            type.GetField("_productionFactory", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, aircraft);
            var started = type.GetMethod("StartFinancedFactoryConstruction", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(runner, new object[] { false, size });
            Assert.That(started, Is.False);
            Assert.That(aircraft.Factories, Is.Empty);
            Assert.That(((LoanPortfolio)type.GetField("_loanPortfolio", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runner)!).Loans, Is.Empty);
            Assert.That(accounting.Cash, Is.EqualTo(initialCash));
            Assert.That(accounting.GetTransactions(), Is.Empty);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
