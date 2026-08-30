using System.Collections.Generic;
using NUnit.Framework;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Accounting;

[TestFixture]
public sealed class AccountingSystemTests
{
    [Test]
    public void RegisteringMonthlyCostsAndIncome_CreatesOneByOneTransactions()
    {
        var accounting = new AccountingSystem(startingCash: 1000d);
        var date = new SimulationDate(1946, 1);
        var costs = new AccountingCostBreakdown(
            FactoryMaintenanceExpenses: 100d,
            ActiveEquipmentExpenses: 0d,
            VariableEngineStaffExpenses: 50d,
            VariableEngineBuildExpenses: 0d,
            VariablePlaneStaffExpenses: 40d,
            VariablePlaneBuildExpenses: 10d,
            VariableResearchExpenses: 20d);
        var inventory = new Dictionary<string, InventoryValueEntry>();

        accounting.RegisterMonthlyFactoryCosts(
            monthIndex: 0,
            date: date,
            costs: costs,
            inventoryByDesign: inventory);
        accounting.RegisterContractIncome(
            monthIndex: 0,
            date: date,
            income: 300d,
            inventoryByDesign: inventory,
            description: "Contract delivery income: ctr.0000.0001");

        var transactions = accounting.GetTransactions();
        Assert.That(transactions.Count, Is.EqualTo(6));
        Assert.That(accounting.Cash, Is.EqualTo(1080d).Within(1e-9));

        Assert.That(transactions[0].Description, Is.EqualTo("Factory property maintenance."));
        Assert.That(transactions[0].Amount, Is.EqualTo(-100d).Within(1e-9));
        Assert.That(transactions[1].Description, Is.EqualTo("Factory variable costs - engine staff."));
        Assert.That(transactions[4].Description, Is.EqualTo("Factory variable costs - R&D."));
        Assert.That(transactions[5].Description, Is.EqualTo("Contract delivery income: ctr.0000.0001"));
        Assert.That(transactions[5].Amount, Is.EqualTo(300d).Within(1e-9));
    }

    [Test]
    public void SnapshotRoundTrip_PreservesTransactions()
    {
        var accounting = new AccountingSystem(startingCash: 500d);
        var date = new SimulationDate(1946, 2);
        var inventory = new Dictionary<string, InventoryValueEntry>();

        accounting.RegisterContractIncome(
            monthIndex: 1,
            date: date,
            income: 120d,
            inventoryByDesign: inventory,
            description: "Government upfront funding: gov.1946.01");
        accounting.RegisterMonthlyFactoryCosts(
            monthIndex: 1,
            date: date,
            costs: new AccountingCostBreakdown(10d, 0d, 5d, 0d, 0d, 0d, 0d),
            inventoryByDesign: inventory);

        var snapshot = accounting.CreateSnapshot();

        var restored = new AccountingSystem(startingCash: 0d);
        restored.RestoreSnapshot(snapshot);

        Assert.That(restored.Cash, Is.EqualTo(accounting.Cash).Within(1e-9));
        Assert.That(restored.GetTransactions().Count, Is.EqualTo(accounting.GetTransactions().Count));
        Assert.That(restored.GetTransactions()[0].Description, Is.EqualTo(accounting.GetTransactions()[0].Description));
        Assert.That(
            restored.GetTransactions()[^1].CashAfterTransaction,
            Is.EqualTo(accounting.GetTransactions()[^1].CashAfterTransaction).Within(1e-9));
    }

    [Test]
    public void HeadquartersOperatingAndCapitalExpenses_AreBookedSeparately()
    {
        var accounting = new AccountingSystem(startingCash: 100_000_000d);
        var date = new SimulationDate(1946, 3);
        var inventory = new Dictionary<string, InventoryValueEntry>();

        accounting.RegisterCapitalExpense(2, date, 20_000_000d, "Wind Tunnel construction");
        accounting.RegisterMonthlyFactoryCosts(
            2,
            date,
            new AccountingCostBreakdown(0d, 0d, 0d, 0d, 0d, 0d, 0d, HeadquartersExpenses: 1_250_000d),
            inventory);

        Assert.That(accounting.Cash, Is.EqualTo(78_750_000d));
        Assert.That(accounting.GetTransactions(), Has.Some.Matches<AccountingTransactionEntry>(
            transaction => transaction.Description == "Wind Tunnel construction" && transaction.Amount == -20_000_000d));
        Assert.That(accounting.GetTransactions(), Has.Some.Matches<AccountingTransactionEntry>(
            transaction => transaction.Description == "Headquarters operating costs." && transaction.Amount == -1_250_000d));
    }

    [Test]
    public void LoanCashFlows_SeparatePrincipalFromProfitAndInterest()
    {
        var accounting = new AccountingSystem(startingCash: 10_000_000d);
        var date = new SimulationDate(1946, 4);

        accounting.RegisterLoanDisbursement(3, date, 20_000_000d, "loan.0001");
        accounting.RegisterLoanFee(3, date, 100_000d, "loan.0001", "Loan origination fee.");
        accounting.RegisterLoanDebtService(3, date, 200_000d, 300_000d, "loan.0001");

        var month = accounting.TryGetSnapshot(3)!;
        Assert.That(accounting.Cash, Is.EqualTo(29_400_000d));
        Assert.That(month.Income, Is.Zero);
        Assert.That(month.OperatingProfitLoss, Is.Zero);
        Assert.That(month.MonthlyProfitLoss, Is.EqualTo(-300_000d));
        Assert.That(month.LoanPrincipalReceived, Is.EqualTo(20_000_000d));
        Assert.That(month.LoanPrincipalRepaid, Is.EqualTo(300_000d));
        Assert.That(accounting.GetTransactions()[0].Kind, Is.EqualTo(AccountingTransactionKind.LoanPrincipalReceived));
        Assert.That(accounting.GetTransactions()[0].ReferenceId, Is.EqualTo("loan.0001"));
    }

    [Test]
    public void LoanAccountingSnapshot_RoundTripPreservesKindsAndMonthlyFinancing()
    {
        var accounting = new AccountingSystem(startingCash: 1_000_000d);
        var date = new SimulationDate(1946, 5);
        accounting.RegisterLoanDisbursement(4, date, 5_000_000d, "loan.0002");
        accounting.RegisterLoanDebtService(4, date, 25_000d, 75_000d, "loan.0002");

        var restored = new AccountingSystem(0d);
        restored.RestoreSnapshot(accounting.CreateSnapshot());

        var month = restored.TryGetSnapshot(4)!;
        Assert.That(month.LoanPrincipalReceived, Is.EqualTo(5_000_000d));
        Assert.That(month.LoanInterestExpenses, Is.EqualTo(25_000d));
        Assert.That(month.LoanPrincipalRepaid, Is.EqualTo(75_000d));
        Assert.That(restored.GetTransactions()[^1].Kind, Is.EqualTo(AccountingTransactionKind.LoanPrincipalRepaid));
        Assert.That(restored.GetTransactions()[^1].ReferenceId, Is.EqualTo("loan.0002"));
    }
}
