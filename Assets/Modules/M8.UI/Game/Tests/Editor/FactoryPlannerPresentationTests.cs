using System;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

[Category("Extended")]
public sealed class FactoryPlannerPresentationTests
{
    private static FactoryPlannerRow Row(string id, string name, bool engine = false, double capacity = 100,
        double? production = 20, double? support = 10) => new(id, engine, name, "Product", FactoryBuildingSize.Small,
        FactoryAutomationTier.HandBuilt, capacity, capacity, production, support, "1", "1", "Ready", false);

    [Test]
    public void Allocation_NormalizesPhysicalCapacityAndKeepsProductionSupportFreeDisjoint()
    {
        var bar = FactoryPlannerProjection.NormalizeAllocation(100, 30, 20);
        Assert.That(bar.Measured, Is.True);
        Assert.That(bar.NormalizedProductionFraction, Is.EqualTo(.3));
        Assert.That(bar.SupportFraction, Is.EqualTo(.2));
        Assert.That(bar.FreeFraction, Is.EqualTo(.5).Within(.000000001));
        var clipped = FactoryPlannerProjection.NormalizeAllocation(100, 80, 80);
        Assert.That(clipped.NormalizedProductionFraction, Is.EqualTo(.8));
        Assert.That(clipped.SupportFraction, Is.EqualTo(.2).Within(.000001));
        Assert.That(clipped.FreeFraction, Is.Zero);
        var negative = FactoryPlannerProjection.NormalizeAllocation(100, -20, 10);
        Assert.That(negative.NormalizedProductionFraction, Is.Zero);
        Assert.That(negative.SupportFraction, Is.EqualTo(.1));
        Assert.That(negative.FreeFraction, Is.EqualTo(.9));
    }

    [TestCase(0d)]
    [TestCase(-1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void InvalidCapacity_DoesNotInventMeasuredOrFreeAllocation(double capacity)
    {
        var bar = FactoryPlannerProjection.NormalizeAllocation(capacity, 10, 10);
        Assert.That(bar.Measured, Is.False);
        Assert.That(bar.NormalizedProductionFraction + bar.SupportFraction + bar.FreeFraction, Is.Zero);
    }

    [Test]
    public void UnknownOrNonFiniteWork_IsNotShownAsAnIdleFactory()
    {
        Assert.That(FactoryPlannerProjection.NormalizeAllocation(100, null, 0).Measured, Is.False);
        Assert.That(FactoryPlannerProjection.NormalizeAllocation(100, 20, null).Measured, Is.False);
        Assert.That(FactoryPlannerProjection.NormalizeAllocation(100, double.NaN, 0).Measured, Is.False);
        Assert.That(FactoryPlannerProjection.NormalizeAllocation(100, 0, double.PositiveInfinity).Measured, Is.False);
        Assert.That(FactoryPlannerProjection.NormalizeAllocation(100, 0, 0).FreeFraction, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UtilizationSort_PutsUnmeasuredSitesLastInEitherDirection(bool descending)
    {
        var input = new[] { Row("motor", "A", true, production: null, support: null), Row("high", "B", production: 80), Row("low", "C", production: 10) };
        var result = FactoryPlannerProjection.FilterAndSort(input, sort: FactoryPlannerSort.Utilization, descending: descending);
        Assert.That(result.Last().Id, Is.EqualTo("motor"));
        Assert.That(result.First().Id, Is.EqualTo(descending ? "high" : "low"));
        Assert.That(input.First().Id, Is.EqualTo("motor"), "Projection sorting must not mutate its source list.");
    }

    [Test]
    public void FiltersAndTies_UseExplicitStateAndStableIdentity()
    {
        var rows = new[]
        {
            Row("b", "Same"), Row("a", "same"),
            Row("install", "Install", true) with { IsInstalling = true, Status = "Installing" },
            Row("expand", "Expand") with { IsExpanding = true, Status = "Expanding" },
            Row("build", "Build") with { IsBuilding = true, Status = "Building" },
            Row("attention", "No workforce") with { Attention = true, Status = "No workforce" },
        };
        Assert.That(FactoryPlannerProjection.FilterAndSort(rows).Where(row => row.Name.Equals("same", StringComparison.OrdinalIgnoreCase))
            .Select(row => row.Id), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(FactoryPlannerProjection.FilterAndSort(rows, FactoryPlannerTypeFilter.Engine).Single().Id, Is.EqualTo("install"));
        Assert.That(FactoryPlannerProjection.FilterAndSort(rows, status: FactoryPlannerStatusFilter.Attention).Single().Id, Is.EqualTo("attention"));
        Assert.That(FactoryPlannerProjection.FilterAndSort(rows, status: FactoryPlannerStatusFilter.Ready).Count, Is.EqualTo(2));
        Assert.That(FactoryPlannerProjection.FilterAndSort(rows, status: FactoryPlannerStatusFilter.Building).Single().Id, Is.EqualTo("build"));
        Assert.That(FactoryPlannerProjection.FilterAndSort(rows, status: FactoryPlannerStatusFilter.Expanding).Single().Id, Is.EqualTo("expand"));
        Assert.That(FactoryPlannerProjection.FilterAndSort(rows, status: FactoryPlannerStatusFilter.Installing).Single().Id, Is.EqualTo("install"));
    }

    [Test]
    public void Build_CombinesNetworksWithoutDistributingMotorNetworkTotalsAcrossSites()
    {
        var aircraft = new AircraftProductionFactory();
        aircraft.TryStartFactoryConstruction(double.MaxValue, out _, out _);
        for (var month = 0; month < 18; month++) aircraft.AdvanceMonth();
        var aircraftReport = aircraft.SimulateMonth(new System.Collections.Generic.Dictionary<string, FactoryAircraftTypeCatalogItem>(), 0, SimulationDate.WrightTycoonStart);
        var engines = new EngineManufacturingNetwork();
        engines.TryConstruct(double.MaxValue, out _, out _); engines.TryConstruct(double.MaxValue, out _, out _);
        for (var month = 0; month < 18; month++) engines.AdvanceMonth();
        var catalog = new System.Collections.Generic.Dictionary<string, EngineManufacturingCatalogItem>
        { ["engine"] = new("engine", "group", 1000, 100) };
        foreach (var site in engines.Sites) engines.Configure(site.SiteId, catalog["engine"], FactoryAutomationTier.HandBuilt, 1, 0, double.MaxValue, out _, out _);
        var motorReport = engines.ProcessMonth(0, catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(motorReport.CompletedEngines, Is.GreaterThan(0));
        var projected = FactoryPlannerProjection.Build(aircraft, engines, aircraftReport, motorReport, engineName: id => "Named " + id);
        Assert.That(projected.Count, Is.EqualTo(3));
        Assert.That(projected.Single(row => !row.IsEngine).HasMeasuredAllocation, Is.True);
        var motors = projected.Where(row => row.IsEngine).ToArray();
        Assert.That(motors.Count(row => row.HasMeasuredAllocation), Is.EqualTo(1));
        Assert.That(motors.Single(row => row.HasMeasuredAllocation).ProductionMD, Is.EqualTo(motorReport.UsedManDays));
        Assert.That(motors.Single(row => row.HasMeasuredAllocation).SupportMD, Is.Zero);
        foreach (var motor in motors)
        {
            var producedHere = motor.HasMeasuredAllocation ? 2 : 0;
            Assert.That(motor.Actual, Is.EqualTo($"Stock 2 engines · last month {producedHere} · total built {producedHere}"));
            Assert.That(motor.Product, Is.EqualTo("Named engine"));
        }
        var unknown = motors.Single(row => !row.HasMeasuredAllocation);
        Assert.That(unknown.ProductionMD, Is.Null); Assert.That(unknown.SupportMD, Is.Null);
        Assert.That(FactoryPlannerProjection.Build(null, engines).All(row => !row.HasMeasuredAllocation), Is.True);
        aircraft.SimulateMonth(new System.Collections.Generic.Dictionary<string, FactoryAircraftTypeCatalogItem>(), 1, SimulationDate.WrightTycoonStart.NextMonth());
        var stale = FactoryPlannerProjection.Build(aircraft, null, aircraftReport).Single();
        Assert.That(stale.HasMeasuredAllocation, Is.False, "A prior report cannot borrow another month's support allocation.");
    }

    [Test]
    public void MotorSiteDiagnostics_ReconcileActualProductionAndPartsWorkWithoutChangingNetworkTotals()
    {
        var network = new EngineManufacturingNetwork();
        network.TryConstruct(double.MaxValue, out _, out _); network.TryConstruct(double.MaxValue, out _, out _);
        for (var month = 0; month < 18; month++) network.AdvanceMonth();
        // Both sites work: the first uses 200 MD for parts and lends the rest to engines.
        foreach (var site in network.Sites) network.SetWorkforceEmployeeLimit(site.SiteId, 400);
        var item = new EngineManufacturingCatalogItem("motor", "group", 1000, 100);
        var catalog = new System.Collections.Generic.Dictionary<string, EngineManufacturingCatalogItem> { [item.EngineDesignId] = item };
        foreach (var site in network.Sites) network.Configure(site.SiteId, item, FactoryAutomationTier.HandBuilt, 0, 0, double.MaxValue, out _, out _);
        var report = network.ProcessMonth(0, catalog, new[] { new EngineManufacturingDemand("motor", "group", 200, 200) });
        Assert.That(report.CompletedEngines, Is.EqualTo(166)); Assert.That(report.CompletedParts, Is.EqualTo(200));
        Assert.That(report.ProductionManDaysBySite.Values.Sum(), Is.EqualTo(16600));
        Assert.That(report.PartsManDaysBySite.Values.Sum(), Is.EqualTo(200).Within(.00001));
        Assert.That(report.UsedManDays, Is.EqualTo(report.ProductionManDaysBySite.Values.Sum() + report.PartsManDaysBySite.Values.Sum()));
        Assert.That(report.ProductionManDaysBySite[network.Sites[0].SiteId], Is.EqualTo(8200));
        Assert.That(report.PartsManDaysBySite[network.Sites[0].SiteId], Is.EqualTo(200).Within(.00001));
        var rows = FactoryPlannerProjection.Build(null, network, engineReport: report);
        Assert.That(rows.All(row => row.HasMeasuredAllocation), Is.True);
        Assert.That(rows.Sum(row => row.ProductionMD!.Value + row.SupportMD!.Value), Is.EqualTo(report.UsedManDays));
    }
}
