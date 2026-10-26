using System;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class FleetAircraftSparePartsTests
{
    private static readonly SimulationDate Delivery = new(2026, 1);

    private sealed class PartsProvider : IFleetEngineServiceProvider
    {
        public int Stock = int.MaxValue;
        public int Consumed;
        public int CompleteAirframeServices(string groupId, int requestedServices, int partsPerService)
        {
            Assert.That(groupId, Is.EqualTo("airframe-group"));
            var count = System.Math.Min(requestedServices, Stock / partsPerService);
            var parts = checked(count * partsPerService);
            Stock -= parts; Consumed += parts; return count;
        }
        public int CompleteEngineMaintenance(string engineDesignId, string groupId, int requestedAircraft,
            int engineCount, int partsPerEngine, bool replacement) => requestedAircraft;
    }

    private static FleetRegistry Registry(int count = 1, double monthlyHours = 2500, int quantity = 3,
        double reliability = .9)
    {
        var fleet = new FleetRegistry(89);
        var offer = new ServiceOfferSnapshot(500, 120, quantity * 7, 0, 0)
        {
            SparePartsPerService = quantity, SparePartUnitPrice = 7, AirframePartsGroupId = "airframe-group",
            EngineCount = 2,
            EngineMaintenance = new EngineMaintenanceSpecification("engine-v1", "engine-parts", .9, 8000, 1000000, 1, 0, 0),
        };
        fleet.RegisterDelivery("delivery", "design", "Test", "contract", count, Delivery,
            new AircraftLifecycleSpecification(AircraftCategory.HeavyTransportWideBody, reliability, 0, 500, 100000, 120),
            new OperatingProfile("transport", monthlyHours, 4), offer, 100000, 2);
        return fleet;
    }

    [Test]
    public void AirframePartsProgramCrossesFiveThresholds()
    {
        var fleet = Registry(); var provider = new PartsProvider();
        var demand = fleet.GetServiceDemand(Delivery.NextMonth()).Single();
        Assert.That(demand.AirframeSpareParts, Is.EqualTo(15));
        var forecast = fleet.GetSupportForecast("design", Delivery.NextMonth(), 1).Single();
        Assert.That(forecast.AirframeSpareParts, Is.EqualTo(15));
        var report = fleet.ProcessMonth(Delivery.NextMonth(), provider);
        Assert.That(report.CompletedServices, Is.EqualTo(5));
        Assert.That(provider.Consumed, Is.EqualTo(15)); Assert.That(report.Revenue, Is.EqualTo(105));
    }

    [Test]
    public void FrozenReliabilitySpecificQuantityControlsActualDemandConsumptionAndRevenue()
    {
        var reliable = Registry(quantity: 2, reliability: .95);
        var lessReliable = Registry(quantity: 5, reliability: .65);
        var reliableProvider = new PartsProvider(); var lessReliableProvider = new PartsProvider();
        Assert.That(reliable.GetServiceDemand(Delivery.NextMonth()).Single().AirframeSpareParts, Is.EqualTo(10));
        Assert.That(lessReliable.GetServiceDemand(Delivery.NextMonth()).Single().AirframeSpareParts, Is.EqualTo(25));
        var small = reliable.ProcessMonth(Delivery.NextMonth(), reliableProvider);
        var large = lessReliable.ProcessMonth(Delivery.NextMonth(), lessReliableProvider);
        Assert.That(small.Revenue, Is.EqualTo(70)); Assert.That(large.Revenue, Is.EqualTo(175));
        Assert.That(reliableProvider.Consumed, Is.EqualTo(10)); Assert.That(lessReliableProvider.Consumed, Is.EqualTo(25));
    }

    [Test]
    public void PartialStockServicesOnlyWholeAircraftAndPreservesUnconsumedRemainder()
    {
        var fleet = Registry(count: 40); var provider = new PartsProvider { Stock = 4 };
        var report = fleet.ProcessMonth(Delivery.NextMonth(), provider);
        Assert.That(report.CompletedServices, Is.EqualTo(1));
        Assert.That(report.Revenue, Is.EqualTo(21)); Assert.That(provider.Stock, Is.EqualTo(1));
        Assert.That(provider.Consumed, Is.EqualTo(3));
        Assert.That(fleet.Cohorts.Sum(c => c.CompletedServiceCycles * c.ActiveCount), Is.EqualTo(1));
        Assert.That(fleet.Cohorts.Sum(c => c.OriginalCount), Is.EqualTo(40));
        foreach (var c in fleet.Cohorts)
            Assert.That(c.OriginalCount, Is.EqualTo(c.ActiveCount + c.AccidentLosses + c.RetiredCount));
        var shortage = fleet.ProcessMonth(Delivery.AddMonths(2), provider);
        Assert.That(shortage.Revenue, Is.Zero); Assert.That(shortage.AircraftHours, Is.Zero);
        Assert.That(provider.Stock, Is.EqualTo(1));
    }

    [Test]
    public void NoMonthlyInvoiceOrPartConsumptionBeforeServiceThreshold()
    {
        var fleet = Registry(monthlyHours: 100); var provider = new PartsProvider();
        for (var month = 1; month < 5; month++)
        {
            Assert.That(fleet.GetServiceDemand(Delivery.AddMonths(month)), Is.Empty);
            var report = fleet.ProcessMonth(Delivery.AddMonths(month), provider);
            Assert.That(report.Revenue, Is.Zero); Assert.That(report.CompletedServices, Is.Zero);
            Assert.That(provider.Consumed, Is.Zero);
        }
        Assert.That(fleet.GetServiceDemand(Delivery.AddMonths(5)).Single().AirframeSpareParts, Is.EqualTo(3));
        var due = fleet.ProcessMonth(Delivery.AddMonths(5), provider);
        Assert.That(due.CompletedServices, Is.EqualTo(1)); Assert.That(due.Revenue, Is.EqualTo(21));
    }

    [Test]
    public void SnapshotPreservesFrozenQuantityPriceAndServicePhaseAcrossShortageRecovery()
    {
        var fleet = Registry(); var firstProvider = new PartsProvider { Stock = 4 };
        fleet.ProcessMonth(Delivery.NextMonth(), firstProvider);
        var saved = fleet.CreateSnapshot(); var restored = new FleetRegistry(1); restored.RestoreSnapshot(saved);
        Assert.That(restored.Cohorts.Single().Offer.MaintenanceModelVersion, Is.EqualTo(2));
        Assert.That(restored.Cohorts.Single().Offer.SparePartsPerService, Is.EqualTo(3));
        Assert.That(restored.Cohorts.Single().Offer.SparePartUnitPrice, Is.EqualTo(7));
        Assert.That(restored.GetServiceDemand(Delivery.AddMonths(2)), Is.EqualTo(fleet.GetServiceDemand(Delivery.AddMonths(2))));
        Assert.That(restored.ProcessMonth(Delivery.AddMonths(2), new PartsProvider()),
            Is.EqualTo(fleet.ProcessMonth(Delivery.AddMonths(2), new PartsProvider())));
        Assert.That(restored.Cohorts, Is.EqualTo(fleet.Cohorts));
    }

    [Test]
    public void OfferRequiresInstalledEngineTermsAndAirframeGroup()
    {
        var offer = Registry().Cohorts.Single().Offer;
        Assert.Throws<ArgumentException>(() => (offer with { EngineMaintenance = null }).Validate());
        Assert.Throws<ArgumentException>(() => (offer with { AirframePartsGroupId = "" }).Validate());
    }
}
