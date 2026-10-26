using System;
using System.Collections.Generic;
using NUnit.Framework;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Tests.Factories;

namespace WrightTycoon.Core.Tests.Aircraft;

public sealed class FleetServiceBalanceTests
{
    [TestCase(1, 10d)]
    [TestCase(2, 100d)]
    [TestCase(4, 4000d)]
    public void ReferenceHandBuiltPartsAndReplacementSalesEarnFiftyPercentAfterActualProductionOverhead(int engines, double engineMd)
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("priced.service", 100d, engines, engineMd);
        var offer = LifecycleCostCalculator.CreateServiceOffer(item.Aircraft);
        var terms = offer.EngineMaintenance!;
        var airframeParts = 4 * offer.SparePartsPerService;
        var engineParts = 4 * engines * terms.PartsPerService;
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork();
        var site = factory.Factories[0];
        Assert.That(factory.TryPurchaseAndInstallEquipment(site.FactoryId, item.DesignId, FactoryAutomationTier.HandBuilt,
            item.Aircraft.Performance.TotalProductionManDays, double.MaxValue, out _, out _), Is.True);
        for (var month = 0; month < FactoryBalance.GetInitialInstallationMonths(FactoryAutomationTier.HandBuilt); month++) factory.AdvanceMonth();
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var airframeReport = factory.ProduceSupport(0, catalog, new[] { new FactorySupportRequest(item.DesignId, airframeParts) });
        Assert.That(factory.TryConsumeAirframeSpareParts(item.DesignId, airframeParts, out var airframeCost), Is.True);

        var network = new EngineManufacturingNetwork();
        Assert.That(network.TryConstruct(double.MaxValue, out _, out _), Is.True);
        for (var month = 0; month < 18; month++) network.AdvanceMonth();
        var engine = new EngineManufacturingCatalogItem(terms.EngineDesignId, terms.PartsGroupId, item.Aircraft.Engine)
            { IsPartsGroupAnchor = true };
        Assert.That(network.Configure(network.Sites[0].SiteId, engine, FactoryAutomationTier.HandBuilt,
            0, 0, double.MaxValue, out _, out _), Is.True);
        for (var month = 0; month < FactoryBalance.GetInitialInstallationMonths(FactoryAutomationTier.HandBuilt); month++) network.AdvanceMonth();
        var engineCatalog = new Dictionary<string, EngineManufacturingCatalogItem> { [engine.EngineDesignId] = engine };
        var partsReport = network.ProcessMonth(0, engineCatalog,
            new[] { new EngineManufacturingDemand(terms.EngineDesignId, terms.PartsGroupId, 0, engineParts) });
        Assert.That(network.TryConsumePartUnits(terms.PartsGroupId, engineParts, out var enginePartsCost), Is.True);
        var replacementReport = network.ProcessMonth(1, engineCatalog,
            new[] { new EngineManufacturingDemand(terms.EngineDesignId, terms.PartsGroupId, engines, 0) });
        Assert.That(network.TryConsumeEngineUnits(terms.EngineDesignId, engines, out var replacementCost), Is.True);

        var airframeRevenue = airframeParts * offer.SparePartUnitPrice;
        var enginePartsRevenue = engineParts * terms.PartUnitPrice;
        var replacementRevenue = engines * terms.ReplacementUnitPrice;
        Assert.That(airframeCost, Is.GreaterThan(airframeReport.MaterialExpenses + airframeReport.LaborExpenses));
        Assert.That(enginePartsCost, Is.GreaterThan(partsReport.MaterialExpenses + partsReport.LaborExpenses));
        Assert.That(replacementCost, Is.GreaterThan(replacementReport.MaterialExpenses + replacementReport.LaborExpenses));
        Assert.That((airframeRevenue - airframeCost) / airframeRevenue, Is.EqualTo(.50d).Within(1e-8));
        Assert.That((enginePartsRevenue - enginePartsCost) / enginePartsRevenue, Is.EqualTo(.50d).Within(1e-8));
        Assert.That((replacementRevenue - replacementCost) / replacementRevenue, Is.EqualTo(.50d).Within(1e-8));
        var revenue = airframeRevenue + enginePartsRevenue + replacementRevenue;
        var contribution = revenue - airframeCost - enginePartsCost - replacementCost;
        Assert.That(contribution / revenue, Is.EqualTo(.50d).Within(1e-8));
        Assert.That(airframeReport.UsedManDays, Is.LessThanOrEqualTo(site.AvailableManDays));
        Assert.That(partsReport.UsedManDays, Is.LessThanOrEqualTo(network.Sites[0].AvailableManDays));
        Assert.That(replacementReport.UsedManDays, Is.LessThanOrEqualTo(network.Sites[0].AvailableManDays));
        var candidate = GovernmentContractSubmissionCandidateFactory.FromFactoryCatalogItem(item, AircraftLifecycleStage.Completed);
        Assert.That(candidate.Metrics.CreateServiceOffer(), Is.EqualTo(offer));
    }

    [Test]
    public void RiskDisplayUsesExposureAndDoesNotMultiplyPerFlightRatesByHoursTwice()
    {
        var date = new SimulationDate(2020, 1);
        var category = AircraftCategory.MediumTransportNarrowBody;
        var rate = FleetAccidentCatalog.Get(date, category, "transport");
        var risk = FleetAccidentCatalog.ReferenceLossProbability(date, category, "transport", 3600, 3, .9);
        Assert.That(risk, Is.EqualTo(1d - System.Math.Exp(-rate.Rate * 1200 / 1000000 * .5d)).Within(1e-12));
        Assert.That(FleetAccidentCatalog.ReferenceLossProbability(date, category, "transport", 0, 3, .9), Is.Zero);
        Assert.That(FleetAccidentCatalog.ReferenceLossProbability(date, category, "transport", 3600, 3, .9, 1.2, 1.2), Is.GreaterThan(risk));
        Assert.That(FleetAccidentCatalog.ReferenceLossProbability(date, category, "transport:piston", 3600, 3, .9), Is.GreaterThan(risk));
        Assert.Throws<ArgumentOutOfRangeException>(() => FleetAccidentCatalog.ReferenceLossProbability(date, category, "transport", 1, 0, .9));
    }
}
