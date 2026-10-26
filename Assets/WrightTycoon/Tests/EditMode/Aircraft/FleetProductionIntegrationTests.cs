using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Tests.Factories;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class FleetProductionIntegrationTests
{
    private static readonly SimulationDate Delivery = new(2026, 1);
    private static readonly EngineManufacturingCatalogItem MotorRecipe = new("engine.integration", "engine.parts", 100, 2);

    private sealed class FactoryServiceProvider : IFleetEngineServiceProvider
    {
        private readonly AircraftProductionFactory factory;
        private readonly EngineManufacturingNetwork motors;
        public FactoryServiceProvider(AircraftProductionFactory factory, EngineManufacturingNetwork motors)
        { this.factory = factory; this.motors = motors; }
        public int CompleteAirframeServices(string groupId, int requestedAircraft, int partsPerAircraft)
        {
            var count = System.Math.Min(requestedAircraft, factory.GetAirframeSparePartUnits(groupId) / partsPerAircraft);
            return count > 0 && factory.TryConsumeAirframeSpareParts(groupId, checked(count * partsPerAircraft)) ? count : 0;
        }
        public int CompleteEngineMaintenance(string engineDesignId, string groupId, int requestedAircraft,
            int engineCount, int partsPerEngine, bool replacement)
        {
            var perAircraft = checked(engineCount * (replacement ? 1 : partsPerEngine));
            var stock = replacement ? motors.GetEngineUnits(engineDesignId) : motors.GetPartUnits(groupId);
            var count = System.Math.Min(requestedAircraft, stock / perAircraft);
            if (count == 0) return 0;
            var units = checked(count * perAircraft);
            return (replacement ? motors.TryConsumeEngineUnits(engineDesignId, units)
                : motors.TryConsumePartUnits(groupId, units)) ? count : 0;
        }
    }

    private static EngineManufacturingNetwork MotorFactory()
    {
        var network = new EngineManufacturingNetwork();
        network.RestoreSnapshot(new EngineManufacturingSnapshot
        {
            Sites = new List<EngineManufacturingSite> { new()
            { SiteId = "motor-plant", EngineDesignId = MotorRecipe.EngineDesignId, AutomationTier = FactoryAutomationTier.HandBuilt } },
        });
        return network;
    }

    private static (AircraftProductionFactory factory, EngineManufacturingNetwork motors, FleetRegistry fleet,
        Dictionary<string, FactoryAircraftTypeCatalogItem> catalog) DeliveredAircraft()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("integration.transport", 100d, 2, 20d);
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork();
        var site = factory.Factories.Single();
        Assert.That(factory.TryPurchaseAndInstallEquipment(site.FactoryId, item.DesignId, FactoryAutomationTier.HandBuilt,
            item.Aircraft.Performance.TotalProductionManDays, double.MaxValue, out _, out _), Is.True);
        factory.TrySetMonthlyProductionTargetUnits(site.FactoryId, 1, out _, out _);
        factory.SetTotalProductionTargetUnits(site.Line!.LineId, 1);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var production = factory.SimulateMonth(catalog, 0, Delivery);
        Assert.That(production.TotalAircraftProduced, Is.EqualTo(1));
        Assert.That(factory.TryWithdrawFromWarehouse(item.DesignId, 1, out _), Is.True);
        var offer = new ServiceOfferSnapshot(500, 120, 10_000, 0, 0)
        {
            SparePartsPerService = 1, SparePartUnitPrice = 10_000, AirframePartsGroupId = item.DesignId, EngineCount = 2,
            EngineMaintenance = new EngineMaintenanceSpecification(MotorRecipe.EngineDesignId, MotorRecipe.PartsGroupId,
                .9, 500, 1000, 2, 3, 100),
        };
        var fleet = new FleetRegistry(89);
        Assert.That(fleet.RegisterDelivery("delivery.integration.1", item.DesignId, item.DisplayName, "contract.integration", 1, Delivery,
            new AircraftLifecycleSpecification(AircraftCategory.HeavyTransportWideBody, item.Aircraft.Performance.OverallReliability,
                0, 500, 100_000, 120), new OperatingProfile("transport", 2500, 4), offer, 100_000, 2), Is.True);
        var motors = MotorFactory();
        Assert.That(fleet.ProcessMonth(Delivery, new FactoryServiceProvider(factory, motors)).AircraftHours, Is.Zero);
        return (factory, motors, fleet, catalog);
    }

    private static (FleetMonthReport fleet, FactorySupportProductionReport support, EngineManufacturingMonthReport motors,
        FactoryMonthlyProductionReport production) Advance(AircraftProductionFactory factory, EngineManufacturingNetwork motors,
            FleetRegistry fleet, IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog, AccountingSystem accounting, int month)
    {
        var date = Delivery.AddMonths(month);
        var support = factory.ProduceSupport(month, catalog, fleet.GetServiceDemand(date)
            .Select(d => new FactorySupportRequest(d.DesignId, d.AirframeSpareParts, d.Guaranteed)));
        var motorReport = motors.ProcessMonth(month,
            new Dictionary<string, EngineManufacturingCatalogItem> { [MotorRecipe.EngineDesignId] = MotorRecipe },
            fleet.GetEngineServiceDemand(date).Select(d => new EngineManufacturingDemand(d.EngineDesignId, d.PartsGroupId,
                d.ReplacementEngines, d.EngineParts, d.Guaranteed)));
        var production = factory.SimulateMonth(catalog, month, date);
        var fleetReport = fleet.ProcessMonth(date, new FactoryServiceProvider(factory, motors));
        accounting.RegisterFleetService(month, date, fleetReport.Revenue, support.TotalExpenses + motorReport.TotalExpenses,
            "fleet.integration." + month);
        var factoryCosts = new AccountingCostBreakdown(production.TotalFactoryMaintenanceExpenses,
            production.TotalActiveEquipmentExpenses, 0, 0, production.LineReports.Sum(line => line.ActualLaborExpenses),
            production.LineReports.Sum(line => line.ProducedUnits * catalog[line.AssignedDesignId!].Aircraft.Performance.TotalProductionCost));
        accounting.RegisterMonthlyFactoryCosts(month, date, factoryCosts, new Dictionary<string, InventoryValueEntry>());
        return (fleetReport, support, motorReport, production);
    }

    [Test]
    public void DeliveredCohort_AirframeAndPhysicalEngineMaintenanceAccountingAndSavedMonthRemainDeterministic()
    {
        var state = DeliveredAircraft(); var accounting = new AccountingSystem(100_000_000);
        var first = Advance(state.factory, state.motors, state.fleet, state.catalog, accounting, 1);
        Assert.That(first.fleet.CompletedServices, Is.EqualTo(10));
        Assert.That(first.fleet.Revenue, Is.EqualTo(50_436));
        Assert.That(first.support.CompletedAirframeSpareParts, Is.EqualTo(5));
        Assert.That(first.motors.CompletedEngines, Is.EqualTo(4));
        Assert.That(first.motors.CompletedParts, Is.EqualTo(12));
        Assert.That(state.motors.GetEngineUnits(MotorRecipe.EngineDesignId), Is.Zero);
        Assert.That(state.motors.GetPartUnits(MotorRecipe.PartsGroupId), Is.Zero);
        var costs = accounting.TryGetSnapshot(1)!;
        Assert.That(accounting.Cash, Is.EqualTo(100_000_000 + costs.Income - costs.Costs.TotalCosts).Within(.00001));
        var savedCash = accounting.Cash;
        accounting.RegisterFleetService(1, Delivery.NextMonth(), first.fleet.Revenue,
            first.support.TotalExpenses + first.motors.TotalExpenses, "fleet.integration.1");
        Assert.That(accounting.Cash, Is.EqualTo(savedCash), "Persisted references cannot invoice service twice.");

        var restoredFactory = new AircraftProductionFactory(); restoredFactory.RestoreSnapshot(state.factory.CreateSnapshot());
        var restoredMotors = new EngineManufacturingNetwork(); restoredMotors.RestoreSnapshot(state.motors.CreateSnapshot());
        var restoredFleet = new FleetRegistry(1); restoredFleet.RestoreSnapshot(state.fleet.CreateSnapshot());
        var restoredAccounting = new AccountingSystem(0); restoredAccounting.RestoreSnapshot(accounting.CreateSnapshot());
        var second = Advance(state.factory, state.motors, state.fleet, state.catalog, accounting, 2);
        var replay = Advance(restoredFactory, restoredMotors, restoredFleet, state.catalog, restoredAccounting, 2);
        Assert.That(second.motors.CompletedEngines, Is.EqualTo(6));
        Assert.That(second.motors.CompletedParts, Is.EqualTo(8));
        Assert.That(second.fleet.Revenue, Is.EqualTo(50_624));
        Assert.That(replay.fleet, Is.EqualTo(second.fleet));
        Assert.That(replay.support.TotalExpenses, Is.EqualTo(second.support.TotalExpenses));
        Assert.That(replay.motors.TotalExpenses, Is.EqualTo(second.motors.TotalExpenses));
        Assert.That(restoredFleet.Cohorts, Is.EqualTo(state.fleet.Cohorts));
        Assert.That(restoredFleet.CreateSnapshot().AccidentRandomState, Is.EqualTo(state.fleet.CreateSnapshot().AccidentRandomState));
        Assert.That(restoredAccounting.Cash, Is.EqualTo(accounting.Cash));
        Assert.That(restoredAccounting.GetTransactions(), Is.EqualTo(accounting.GetTransactions()));
        Assert.That(restoredMotors.GetEngineUnits(MotorRecipe.EngineDesignId), Is.EqualTo(state.motors.GetEngineUnits(MotorRecipe.EngineDesignId)));
        Assert.That(restoredMotors.GetPartUnits(MotorRecipe.PartsGroupId), Is.EqualTo(state.motors.GetPartUnits(MotorRecipe.PartsGroupId)));
    }

    [Test]
    public void FiniteCapacityShortageGroundsFleetAndResumePaysOnlyCompletedServices()
    {
        var state = DeliveredAircraft(); var accounting = new AccountingSystem(100_000_000);
        var site = state.factory.Factories.Single();
        state.factory.SetTotalProductionTargetUnits(site.Line!.LineId, 0);
        state.factory.SetWorkforceEmployeeLimit(site.FactoryId, 0);
        state.factory.SetPartsAllocationPercent(site.FactoryId, 40);
        var shortage = Advance(state.factory, state.motors, state.fleet, state.catalog, accounting, 1);
        Assert.That(shortage.support.UsedManDays, Is.Zero);
        Assert.That(shortage.production.TotalAircraftProduced, Is.Zero);
        Assert.That(shortage.fleet.AircraftHours, Is.EqualTo(500));
        Assert.That(shortage.fleet.GuaranteedShortfall, Is.EqualTo(1));
        Assert.That(shortage.fleet.Revenue, Is.Zero);

        state.factory.SetWorkforceEmployeeLimit(site.FactoryId, 5);
        var resumed = Advance(state.factory, state.motors, state.fleet, state.catalog, accounting, 2);
        Assert.That(resumed.fleet.CompletedServices, Is.EqualTo(12));
        Assert.That(resumed.fleet.GroundedUnits, Is.Zero);
        Assert.That(resumed.fleet.Revenue, Is.EqualTo(60_636));
        Assert.That(resumed.support.UsedManDays + resumed.production.TotalUsedManDays, Is.LessThanOrEqualTo(105));
        Assert.That(resumed.production.TotalAircraftProduced, Is.Zero);
        Assert.That(state.factory.GetAirframeSparePartUnits(state.catalog.Keys.Single()), Is.Zero);
        var costs = accounting.GetHistory();
        Assert.That(accounting.Cash, Is.EqualTo(100_000_000 + costs.Sum(month => month.Income - month.Costs.TotalCosts)).Within(.00001));
    }
}
