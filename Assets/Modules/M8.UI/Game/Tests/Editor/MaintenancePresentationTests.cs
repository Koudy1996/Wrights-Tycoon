using System;
using NUnit.Framework;
using WrightTycoon.Game.Launch;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Game.Tests;

[TestFixture]
public sealed class MaintenancePresentationTests
{
    [TestCase(200, 200)]
    [TestCase(8000, 4000)]
    [TestCase(123, 1000)]
    [Category("Extended")]
    public void EditableDesignerUsesNewIntervalsWithoutChangingLegacyFleetValidation(int saved, int expected)
    {
        var normalize = typeof(WrightTycoonLaunchRunner).GetMethod("NormalizeServiceInterval",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        Assert.That(normalize.Invoke(null, new object[] { saved }), Is.EqualTo(expected));
        Assert.That(typeof(WrightTycoonLaunchRunner).GetMethod("DrawAircraftMaintenanceParameters",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), Is.Null,
            "The verbose aircraft service block must not return to the type report.");
    }

    [Test]
    public void FleetSummaryUsesFrozenEngineCountsAndOrdinalDistinctDesignSelection()
    {
        var cohorts = new[]
        {
            Cohort("a", "one", 4, 2, 1, 1, 2, false, 500, 5000, 30),
            Cohort("a", "two", 3, 3, 0, 0, 4, true, 1000, 9000, 40),
            Cohort("b", "three", 6, 6, 0, 0, 1, false, 500, 5000, 35),
            Cohort("A", "case-sensitive", 100, 100, 0, 0, 8, false, 8000, 99999, 90),
            Cohort("other", "not-selected", 100, 100, 0, 0, 8, false, 8000, 99999, 90),
        };
        var summary = MaintenancePresentation.Summarize(cohorts, new[] { "a", "b", "a" }, 20);
        Assert.That(summary.ActiveAircraft, Is.EqualTo(11));
        Assert.That(summary.GroundedAircraft, Is.EqualTo(3));
        Assert.That(summary.FlyingEngines, Is.EqualTo(10));
        Assert.That(summary.GroundedEngines, Is.EqualTo(12));
        Assert.That(summary.ServiceIntervalsHours, Is.EqualTo(new[] { 500, 1000 }));
        Assert.That(summary.ServicePrices, Is.EqualTo(new[] { 5000d, 9000d }));
        Assert.That(summary.GuaranteedAircraft, Is.EqualTo(11));
        Assert.That(summary.GuaranteedContracts, Is.EqualTo(3));
    }

    [Test]
    public void GuaranteesIncludeDisabledSupportButExcludeRetiredAircraftAndLastMonthIsInclusive()
    {
        var cohorts = new[]
        {
            Cohort("a", "same-contract", 4, 1, 1, 2, 2, false, 500, 5000, 30) with { SupportEnabled = false },
            Cohort("a", "same-contract", 3, 3, 0, 0, 4, true, 1000, 9000, 40),
            Cohort("a", "expired", 6, 6, 0, 0, 1, false, 500, 5000, 29),
            Cohort("a", "retired", 5, 0, 1, 4, 2, false, 8000, 99000, 90),
        };
        var summary = MaintenancePresentation.Summarize(cohorts, new[] { "a" }, 30);
        Assert.That(summary.ActiveAircraft, Is.EqualTo(10));
        Assert.That(summary.GuaranteedAircraft, Is.EqualTo(4));
        Assert.That(summary.GuaranteedContracts, Is.EqualTo(1));
        Assert.That(summary.NextSupportEndMonth, Is.EqualTo(30));
        Assert.That(summary.LatestSupportEndMonth, Is.EqualTo(40));
        Assert.That(summary.ServiceIntervalsHours, Is.EqualTo(new[] { 500, 1000 }));
        Assert.That(summary.ServicePrices, Is.EqualTo(new[] { 5000d, 9000d }));
        var expired = MaintenancePresentation.Summarize(cohorts, new[] { "a" }, 41);
        Assert.That(expired.GuaranteedAircraft, Is.Zero);
        Assert.That(expired.GuaranteedContracts, Is.Zero);
        Assert.That(expired.NextSupportEndMonth, Is.EqualTo(-1));
        Assert.That(expired.LatestSupportEndMonth, Is.EqualTo(-1));
    }

    [Test]
    [Category("Extended")]
    public void EmptySelectionHasNoTermsEnginesOrGuarantees()
    {
        var summary = MaintenancePresentation.Summarize(
            new[] { Cohort("a", "one", 4, 4, 0, 0, 2, false, 500, 5000, 30) }, Array.Empty<string>(), 20);
        Assert.That(summary.ActiveAircraft, Is.Zero);
        Assert.That(summary.FlyingEngines, Is.Zero);
        Assert.That(summary.GroundedEngines, Is.Zero);
        Assert.That(summary.ServiceIntervalsHours, Is.Empty);
        Assert.That(summary.ServicePrices, Is.Empty);
        Assert.That(summary.GuaranteedAircraft, Is.Zero);
        Assert.That(summary.LatestSupportEndMonth, Is.EqualTo(-1));
        Assert.That(summary.NextSupportEndMonth, Is.EqualTo(-1));
    }

    private static FleetCohortSnapshot Cohort(string design, string contract, int original, int active,
        int losses, int retired, int engines, bool grounded, int interval, double price, int supportEnd) =>
        new(design + contract, contract, design, design, contract, original, active, losses, retired,
            0, supportEnd,
            new AircraftLifecycleSpecification(AircraftCategory.LightMultiRolePiston, .8, 0, interval, 20000, 120),
            new OperatingProfile("test", 100, 1), new ServiceOfferSnapshot(interval, 120, price, 0, 0)
            {
                SparePartsPerService = 1, SparePartUnitPrice = price, AirframePartsGroupId = design, EngineCount = engines,
                EngineMaintenance = new EngineMaintenanceSpecification("engine", "parts", .8, 500, 4000, 3, 10, 1000),
            },
            18000, 20000, engines, 0, 0, 0, grounded, true, -1, 0);

}
