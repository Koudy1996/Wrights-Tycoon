using System;
using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Tests.Factories;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class AircraftSparePartEconomyTests
{
    private static ServiceOfferSnapshot Offer(int interval, int months, double airframeCost, double reliability,
        AircraftPerformanceReport? performance = null)
    {
        var source = AircraftProductionFactoryTests.CreateCatalogItem("separate.parts", 100, 2, 20).Aircraft;
        var type = new AircraftTypeDefinition { Id = source.TypeDefinition.Id, FamilyId = source.TypeDefinition.FamilyId,
            DisplayName = "Separate parts", ServiceIntervalHours = interval, GuaranteedSupportMonths = months };
        var aircraft = new WrightTycoon.Core.Aircraft.Aircraft(source.FamilyDefinition, type, source.Engine, source.Airframe,
            performance ?? source.Performance with { AirframeProductionCost = airframeCost, AirframeProductionManDays = 0 }, source.Development);
        return LifecycleCostCalculator.CreateEngineLifecycleServiceOffer(aircraft, reliability);
    }

    [Test]
    public void ReferenceCostAllocatesOnlySmallHandBuiltMonthlyOverheadAndUsesFiftyPercentSellingMargin()
    {
        const double materials = 10000d, manDays = 100d;
        var aircraftCost = FleetServiceBalance.ReferenceManufacturingCost(materials, manDays);
        var engineCost = FleetServiceBalance.ReferenceManufacturingCost(materials, manDays, engineFactory: true);
        Assert.That(aircraftCost, Is.EqualTo(materials + manDays * (80d + 601000d / 12600d)).Within(1e-8));
        Assert.That(engineCost, Is.EqualTo(materials + manDays * (80d + 301000d / 12600d)).Within(1e-8));
        Assert.That(FleetServiceBalance.SellingPrice(aircraftCost), Is.EqualTo(2d * aircraftCost));
        Assert.That(FleetServiceBalance.SellingPrice(engineCost), Is.EqualTo(2d * engineCost));
    }

    [TestCase(1d, 1)]
    [TestCase(1.2d, 1)]
    [TestCase(double.MaxValue, 1)]
    [TestCase(.9d, 2)]
    [TestCase(.8d, 3)]
    [TestCase(.5d, 6)]
    [TestCase(0d, 11)]
    [TestCase(.900000000001d, 2)]
    [TestCase(.899999999999d, 2)]
    [TestCase(.899999d, 3)]
    [TestCase(.999999d, 2)]
    [Category("Extended")]
    public void QuantityFollowsReliabilityDecilesWithoutFloatingPointBoundaryInflation(double reliability, int expected)
    {
        Assert.That(FleetServiceBalance.RequiredSpareParts(reliability), Is.EqualTo(expected));
    }

    [Test]
    public void PartsPerServiceDependOnReliabilityNotTheServiceInterval()
    {
        foreach (var reliability in new[] { 0d, .5d, .8d, .9d, 1d })
        foreach (var interval in LifecycleBalance.ServiceIntervalsHours)
        {
            var offer = Offer(interval, 120, 100000d, reliability);
            Assert.That(offer.SparePartsPerService, Is.EqualTo(FleetServiceBalance.RequiredSpareParts(reliability)));
            Assert.That(offer.ServicePricePerEvent, Is.EqualTo(offer.SparePartsPerService * offer.SparePartUnitPrice));
        }
    }

    [Test]
    public void AirframePartIncludesSmallHandBuiltOverheadAtFiftyPercentSellingMargin()
    {
        // $100,000 materials (including $40,000 engine materials), 10,000 total MD at $80/MD.
        var performance = new AircraftPerformanceReport(Array.Empty<AircraftPerformancePoint>(), 0,
            Mass.FromKilograms(0), 100000, 10000, .8, Mass.FromKilograms(0),
            AirframeProductionCost: 60000, AirframeProductionManDays: 6000,
            EnginesProductionCost: 40000, EnginesProductionManDays: 4000);
        Assert.That(performance.CompleteProductionCost, Is.EqualTo(900000d));
        var offer = Offer(1000, 120, 0d, .8, performance);
        var cost = (60000d + 6000d * (80d + 601000d / 12600d)) * .01d;
        var price = offer.SparePartUnitPrice;
        Assert.That(cost, Is.EqualTo(8261.904761904761d).Within(1e-8));
        Assert.That(price, Is.EqualTo(cost / .5d).Within(1e-8));
        Assert.That((price - cost) / price, Is.EqualTo(.5d).Within(1e-12));
        Assert.That(offer.SparePartsPerService, Is.EqualTo(3));
        Assert.That(offer.SparePartUnitPrice, Is.EqualTo(price));
        Assert.That(offer.ServicePricePerEvent, Is.EqualTo(3d * cost / .5d).Within(1e-8));
        Assert.That(offer.ExpectedRepairCostPerHour, Is.Zero);
        Assert.That(offer.DowntimeCostPerHour, Is.EqualTo(9d).Within(1e-12));
    }

    [Test]
    public void SeparateMaintenanceTermsAreFrozenOnCreation()
    {
        var original = Offer(500, 180, 100000, .9);
        var newer = Offer(2000, 60, 200000, .5);
        Assert.That(original.MaintenanceModelVersion, Is.EqualTo(2));
        Assert.That(original.SparePartsPerService, Is.EqualTo(2));
        Assert.That(original.SparePartUnitPrice, Is.EqualTo(2000));
        Assert.That(original.ServicePricePerEvent, Is.EqualTo(4000));
        Assert.That(original.ServiceIntervalHours, Is.EqualTo(500));
        Assert.That(original.GuaranteedSupportMonths, Is.EqualTo(180));
        Assert.That(newer.ServicePricePerEvent, Is.EqualTo(24000));
        Offer(1000, 120, 0, 1).Validate();
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    [TestCase(-.01d)]
    [Category("Extended")]
    public void InvalidReliabilityCannotCreatePartRequirements(double reliability)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FleetServiceBalance.RequiredSpareParts(reliability));
        Assert.Throws<ArgumentOutOfRangeException>(() => Offer(1000, 120, 10000, reliability));
    }

    [Test]
    [Category("Extended")]
    public void NewOffersRejectInvalidCostQuantityVersionAndInconsistentFrozenPrice()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Offer(1000, 120, double.NaN, .9));
        Assert.Throws<ArgumentOutOfRangeException>(() => Offer(1000, 120, double.PositiveInfinity, .9));
        Assert.Throws<ArgumentOutOfRangeException>(() => Offer(1000, 120, -1, .9));
        Assert.Throws<ArgumentOutOfRangeException>(() => Offer(1500, 120, 1000, .9));
        Assert.Throws<ArgumentOutOfRangeException>(() => Offer(1000, 0, 1000, .9));
        var offer = Offer(1000, 120, 100000, .9);
        Assert.Throws<ArgumentOutOfRangeException>(() => (offer with { MaintenanceModelVersion = 3 }).Validate());
        Assert.Throws<ArgumentException>(() => (offer with { EngineMaintenance = null }).Validate());
        Assert.Throws<ArgumentException>(() => (offer with { AirframePartsGroupId = "" }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (offer with { MaintenanceModelVersion = 0 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (offer with { MaintenanceModelVersion = 1 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (offer with { SparePartsPerService = 0 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (offer with { SparePartsPerService = 12 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (offer with { SparePartUnitPrice = -1 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (offer with { SparePartUnitPrice = double.NaN }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (offer with { SparePartUnitPrice = double.PositiveInfinity }).Validate());
        Assert.Throws<ArgumentException>(() => (offer with { ServicePricePerEvent = 1000 }).Validate());
    }
}
