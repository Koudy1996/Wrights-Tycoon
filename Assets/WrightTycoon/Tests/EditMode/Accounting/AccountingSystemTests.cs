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
            FixedProductionLineExpenses: 100d,
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

        Assert.That(transactions[0].Description, Is.EqualTo("Factory fixed costs - production lines."));
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
            costs: new AccountingCostBreakdown(10d, 5d, 0d, 0d, 0d, 0d),
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
}
