using System.Collections.Generic;
using NUnit.Framework;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Core.Tests.Accounting;

public sealed class MaintenanceFinancialLedgerTests
{
    private static FleetMonthReport Report(double revenue, double cost, string design = "aircraft.a") =>
        new(0, 0d, 0, 0, 0, 0, 0, 0, 0, 0, revenue)
        {
            ProductionCost = cost,
            MaintenanceByDesign = new Dictionary<string, FleetMaintenanceResult> { [design] = new(revenue, cost) },
        };

    [Test]
    public void RealizedProfitUsesConsumedCostAndPreservesMonthlyAndPerTypeHistory()
    {
        var ledger = new MaintenanceFinancialLedger();
        ledger.RegisterMonth(10, Report(200, 100));
        ledger.RegisterMonth(11, Report(300, 120, "aircraft.b"));
        Assert.That(ledger.Revenue, Is.EqualTo(500));
        Assert.That(ledger.ProductionCost, Is.EqualTo(220));
        Assert.That(ledger.Profit, Is.EqualTo(280));
        Assert.That(ledger.GetTotals("aircraft.a").Profit, Is.EqualTo(100));
        Assert.That(ledger.GetTotals("aircraft.b").Profit, Is.EqualTo(180));
        Assert.That(ledger.GetLastMonth("aircraft.a").Revenue, Is.Zero);
        Assert.That(ledger.GetLastMonth("aircraft.b").Revenue, Is.EqualTo(300));
        Assert.That(ledger.FirstTrackedMonth, Is.EqualTo(10));
    }

    [Test]
    public void SaveReplayCannotDuplicateSalesOrCostsAndKeepsLossesVisible()
    {
        var original = new MaintenanceFinancialLedger();
        original.RegisterMonth(2, Report(100, 150));
        var saved = original.CreateSnapshot();
        var restored = new MaintenanceFinancialLedger(); restored.RestoreSnapshot(saved);
        restored.RegisterMonth(2, Report(100, 150));
        Assert.That(restored.Profit, Is.EqualTo(-50));
        Assert.That(restored.GetLastMonth("aircraft.a").ProductionCost, Is.EqualTo(150));
        restored.RegisterMonth(3, Report(200, 50));
        Assert.That(restored.Revenue, Is.EqualTo(300));
        Assert.That(restored.Profit, Is.EqualTo(100));
        Assert.That(restored.FirstTrackedMonth, Is.EqualTo(2));
    }
}
