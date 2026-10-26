using System;
using NUnit.Framework;
using WrightTycoon.Game.Machines;

namespace WrightTycoon.Game.Tests;

[TestFixture]
[Category("Extended")]
public sealed class MachineStatisticsLedgerTests
{
    [Test]
    public void RegisterProductionAndSale_CountsAircraftAndInstalledEngines()
    {
        var ledger = new MachineStatisticsLedger();

        ledger.RegisterProduction("type.a", "engine.v2", 6, 12);
        ledger.RegisterSale("type.a", "engine.v2", 6, 5, 25_000_000d);

        Assert.That(ledger.GetProducedAircraftUnits("type.a"), Is.EqualTo(12));
        Assert.That(ledger.GetSoldAircraftUnits("type.a"), Is.EqualTo(5));
        Assert.That(ledger.GetProducedEngineUnits("engine.v2"), Is.EqualTo(72));
        Assert.That(ledger.GetSoldEngineUnits("engine.v2"), Is.EqualTo(30));
        Assert.That(ledger.GetSalesRevenue("type.a"), Is.EqualTo(25_000_000d));
    }

    [Test]
    public void SharedEngine_AccumulatesUnitsAcrossAircraftTypes()
    {
        var ledger = new MachineStatisticsLedger();

        ledger.RegisterProduction("type.a", "engine.shared", 2, 10);
        ledger.RegisterProduction("type.b", "engine.shared", 4, 7);
        ledger.RegisterSale("type.a", "engine.shared", 2, 3, 1_000_000d);
        ledger.RegisterSale("type.b", "engine.shared", 4, 2, 2_000_000d);

        Assert.That(ledger.GetProducedEngineUnits("engine.shared"), Is.EqualTo(48));
        Assert.That(ledger.GetSoldEngineUnits("engine.shared"), Is.EqualTo(14));
    }

    [Test]
    public void StandaloneMotorProduction_AndExternalAirframeAssemblyDoNotCountEnginesTwice()
    {
        var ledger = new MachineStatisticsLedger();
        ledger.RegisterEngineProduction("engine.shared", 20);
        ledger.RegisterProduction("type.a", "engine.shared", 2, 6, includeEngineProduction: false);
        ledger.RegisterProduction("type.b", "engine.shared", 4, 2, includeEngineProduction: false);
        Assert.That(ledger.GetProducedEngineUnits("engine.shared"), Is.EqualTo(20));
        Assert.That(ledger.GetProducedAircraftUnits("type.a"), Is.EqualTo(6));
        Assert.That(ledger.GetProducedAircraftUnits("type.b"), Is.EqualTo(2));
        var restored = new MachineStatisticsLedger(); restored.RestoreSnapshot(ledger.CreateSnapshot());
        Assert.That(restored.GetProducedEngineUnits("engine.shared"), Is.EqualTo(20));
        Assert.That(restored.GetProducedAircraftUnits("type.a"), Is.EqualTo(6));
    }

    [Test]
    public void ReplacementEngineSale_DoesNotCreateAircraftSalesOrAircraftRevenue()
    {
        var ledger = new MachineStatisticsLedger();
        ledger.RegisterProduction("type.a", "engine.shared", 2, 3, includeEngineProduction: false);
        ledger.RegisterEngineProduction("engine.shared", 10);
        ledger.RegisterSale("type.a", "engine.shared", 2, 1, 100_000d);
        ledger.RegisterEngineSale("engine.shared", 3);
        Assert.That(ledger.GetSoldEngineUnits("engine.shared"), Is.EqualTo(5));
        Assert.That(ledger.GetSoldAircraftUnits("type.a"), Is.EqualTo(1));
        Assert.That(ledger.GetSalesRevenue("type.a"), Is.EqualTo(100_000d));
        Assert.That(ledger.GetProducedEngineUnits("engine.shared"), Is.EqualTo(10));
        Assert.That(ledger.SoldAircraftUnitsByDesignId.Count, Is.EqualTo(1));
    }

    [Test]
    public void Snapshot_RoundTripsCountersRevenueAndEstimateMarker()
    {
        var source = new MachineStatisticsLedger();
        source.RegisterProduction("type.a", "engine.a", 1, 9);
        source.RegisterSale("type.a", "engine.a", 1, 4, 500_000d);
        source.AddSalesRevenue("type.a", 100_000d, isEstimate: true);

        var restored = new MachineStatisticsLedger();
        restored.RestoreSnapshot(source.CreateSnapshot());

        Assert.That(restored.GetProducedAircraftUnits("type.a"), Is.EqualTo(9));
        Assert.That(restored.GetSoldAircraftUnits("type.a"), Is.EqualTo(4));
        Assert.That(restored.GetProducedEngineUnits("engine.a"), Is.EqualTo(9));
        Assert.That(restored.GetSoldEngineUnits("engine.a"), Is.EqualTo(4));
        Assert.That(restored.GetSalesRevenue("type.a"), Is.EqualTo(600_000d));
        Assert.That(restored.IsSalesRevenueEstimated("type.a"), Is.True);
    }

    [Test]
    public void Counters_UseCheckedArithmetic()
    {
        var ledger = new MachineStatisticsLedger();
        ledger.RegisterProduction("type.a", "engine.a", 1, int.MaxValue);

        Assert.Throws<OverflowException>(() =>
            ledger.RegisterProduction("type.a", "engine.a", 1, 1));
    }

    [Test]
    public void MachineEconomics_AllocatesSharedResearchAndIncludesAllProduction()
    {
        var familyShare = MachineEconomicsCalculator.AllocateSharedResearchCost(12_000_000d, 3);
        var engineShare = MachineEconomicsCalculator.AllocateSharedResearchCost(8_000_000d, 2);

        var summary = MachineEconomicsCalculator.Calculate(
            fixedCosts: 2_000_000d + familyShare + engineShare,
            producedUnits: 20,
            unitProductionCost: 500_000d,
            salesRevenue: 25_000_000d);

        Assert.That(summary.FixedCosts, Is.EqualTo(10_000_000d));
        Assert.That(summary.TotalCosts, Is.EqualTo(20_000_000d));
        Assert.That(summary.TotalProfit, Is.EqualTo(5_000_000d));
    }
}
