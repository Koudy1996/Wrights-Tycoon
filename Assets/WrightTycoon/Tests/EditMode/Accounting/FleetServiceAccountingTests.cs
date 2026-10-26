using System.Collections.Generic;
using NUnit.Framework;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Tests.Factories;

namespace WrightTycoon.Tests.EditMode.Accounting;

public sealed class FleetServiceAccountingTests
{
    [Test]
    public void ValuationIncludesSupportStockAndOnlyPaidPartialWork()
    {
        var design = AircraftProductionFactoryTests.CreateCatalogItem("support.value", 100, 2, 20);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [design.DesignId] = design };
        var factory = new FactoryStateSnapshot();
        factory.SupportInventory.Add(new FactorySupportInventorySnapshot { DesignId = design.DesignId, AirframeSpareParts = 4, TotalCostBasis = 9876 });
        factory.SupportWorkOrders.Add(new FactorySupportWorkOrderSnapshot { DesignId = design.DesignId,
            RemainingUnits = 100, UnitManDays = 10, CompletedManDays = 2, UnitMaterialCost = 100, UnitOverheadCost = 50 });
        var values = FleetInventoryValuation.AddSupport(new Dictionary<string, InventoryValueEntry>(), factory, catalog);
        Assert.That(values.Count, Is.EqualTo(2));
        Assert.That(values["airframe-spare-part:" + design.DesignId].Units, Is.EqualTo(4));
        Assert.That(values["airframe-spare-part:" + design.DesignId].TotalValue, Is.EqualTo(9876));
        Assert.That(values["support-wip:" + design.DesignId + ":0"].TotalValue,
            Is.EqualTo(2 * (WrightTycoon.Core.Balance.ProductionEconomyBalance.LaborCostPerManDay + 15)));
    }

    [Test]
    public void ServiceRevenueAndExpenseAreBookedOnceAcrossSaveAndFactoryRebooking()
    {
        var system = new AccountingSystem(1000d);
        var date = new SimulationDate(1950, 1);
        var inventory = new Dictionary<string, InventoryValueEntry>();
        system.RegisterMonthlyFactoryCosts(1, date, AccountingCostBreakdown.Zero, inventory);
        system.RegisterFleetService(1, date, 200d, 50d, "fleet:1");
        system.RegisterFleetService(1, date, 200d, 50d, "fleet:1");
        Assert.That(system.Cash, Is.EqualTo(1150d));
        Assert.That(system.GetTransactions()[1].Description, Is.EqualTo("Production of spare parts"));
        var snapshot = system.CreateSnapshot();
        snapshot.Transactions[1].Description = "Fleet service costs.";
        var restored = new AccountingSystem(0d);
        restored.RestoreSnapshot(snapshot);
        Assert.That(restored.GetTransactions()[1].Description, Is.EqualTo("Production of spare parts"));
        restored.RegisterFleetService(1, date, 200d, 50d, "fleet:1");
        restored.RegisterMonthlyFactoryCosts(1, date, AccountingCostBreakdown.Zero, inventory);
        Assert.That(restored.Cash, Is.EqualTo(1150d));
        Assert.That(restored.TryGetSnapshot(1)!.OperatingProfitLoss, Is.EqualTo(150d));
    }
}
