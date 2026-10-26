using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Game.Launch;
using WrightTycoon.Game.Machines;

namespace WrightTycoon.Game.Tests;

public sealed class FleetRuntimeEngineProviderTests
{
    private static IFleetEngineServiceProvider Provider(AircraftProductionFactory factory,
        EngineManufacturingNetwork engines, Action<string, int>? sold = null)
    {
        var type = typeof(WrightTycoonLaunchRunner).GetNestedType("FactoryFleetServiceProvider", BindingFlags.NonPublic)!;
        var constructor = type.GetConstructor(new[] { typeof(AircraftProductionFactory), typeof(EngineManufacturingNetwork), typeof(Action<string, int>) })!;
        return (IFleetEngineServiceProvider)constructor.Invoke(new object?[] { factory, engines, sold });
    }

    [Test]
    public void RuntimeReplacementRequiresAllExactVersionEnginesAndRegistersOnlyPhysicalSales()
    {
        var network = new EngineManufacturingNetwork();
        network.RestoreSnapshot(new EngineManufacturingSnapshot { EngineStock = new Dictionary<string, int>
        { ["v1"] = 3, ["v2-same-parts-group"] = 100 }, EngineStockCostBasis = new Dictionary<string,double> { ["v1"] = 3000, ["v2-same-parts-group"] = 100000 } });
        var ledger = new MachineStatisticsLedger();
        var provider = Provider(new AircraftProductionFactory(), network, ledger.RegisterEngineSale);
        Assert.That(provider.CompleteEngineMaintenance("v1", "shared", 2, 4, 0, true), Is.Zero);
        Assert.That(network.GetEngineUnits("v1"), Is.EqualTo(3));
        Assert.That(network.GetEngineUnits("v2-same-parts-group"), Is.EqualTo(100));
        Assert.That(ledger.GetSoldEngineUnits("v1"), Is.Zero);
        network.RestoreSnapshot(new EngineManufacturingSnapshot { EngineStock = new Dictionary<string, int>
        { ["v1"] = 7, ["v2-same-parts-group"] = 100 }, EngineStockCostBasis = new Dictionary<string,double> { ["v1"] = 7000, ["v2-same-parts-group"] = 100000 } });
        Assert.That(provider.CompleteEngineMaintenance("v1", "shared", 2, 4, 0, true), Is.EqualTo(1));
        Assert.That(network.GetEngineUnits("v1"), Is.EqualTo(3));
        Assert.That(network.GetEngineUnits("v2-same-parts-group"), Is.EqualTo(100));
        Assert.That(ledger.GetSoldEngineUnits("v1"), Is.EqualTo(4));
        Assert.That(((IFleetMaintenanceCostProvider)provider).LastConsumptionCost, Is.EqualTo(4000));
        Assert.That(ledger.SoldAircraftUnitsByDesignId, Is.Empty);
    }

    [Test]
    public void RuntimeRoutinePartsUseSharedGroupAndAreAtomicAcrossAllInstalledEngines()
    {
        var network = new EngineManufacturingNetwork();
        network.RestoreSnapshot(new EngineManufacturingSnapshot { PartStock = new Dictionary<string, int> { ["shared"] = 7 }, PartStockCostBasis = new Dictionary<string,double> { ["shared"] = 70 } });
        var ledger = new MachineStatisticsLedger();
        var provider = Provider(new AircraftProductionFactory(), network, ledger.RegisterEngineSale);
        Assert.That(provider.CompleteEngineMaintenance("v1", "shared", 2, 4, 2, false), Is.Zero);
        Assert.That(network.GetPartUnits("shared"), Is.EqualTo(7));
        network.RestoreSnapshot(new EngineManufacturingSnapshot { PartStock = new Dictionary<string, int> { ["shared"] = 9 }, PartStockCostBasis = new Dictionary<string,double> { ["shared"] = 90 } });
        Assert.That(provider.CompleteEngineMaintenance("v1", "shared", 2, 4, 2, false), Is.EqualTo(1));
        Assert.That(network.GetPartUnits("shared"), Is.EqualTo(1));
        Assert.That(((IFleetMaintenanceCostProvider)provider).LastConsumptionCost, Is.EqualTo(80));
        Assert.That(ledger.GetSoldEngineUnits("v1"), Is.Zero);
    }

    [Test]
    public void RuntimeAirframeServiceConsumesOnlyWholeAirframeGroupParts()
    {
        var factory = new AircraftProductionFactory();
        factory.RestoreSnapshot(new FactoryStateSnapshot { SupportInventory = new List<FactorySupportInventorySnapshot>
        { new() { DesignId = "airframe-group", AirframeSpareParts = 5, TotalCostBasis = 500 } } });
        var provider = Provider(factory, new EngineManufacturingNetwork());
        Assert.That(provider.CompleteAirframeServices("airframe-group", 2, 3), Is.EqualTo(1));
        Assert.That(factory.GetAirframeSparePartUnits("airframe-group"), Is.EqualTo(2));
        Assert.That(((IFleetMaintenanceCostProvider)provider).LastConsumptionCost, Is.EqualTo(300));
        Assert.That(provider.CompleteAirframeServices("airframe-group", 1, 3), Is.Zero);
        Assert.That(((IFleetMaintenanceCostProvider)provider).LastConsumptionCost, Is.Zero);
    }
}
