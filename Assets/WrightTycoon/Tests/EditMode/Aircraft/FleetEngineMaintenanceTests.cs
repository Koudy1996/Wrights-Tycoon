using System;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Company;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Tests.Factories;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class FleetEngineMaintenanceTests
{
    private static readonly SimulationDate Delivery = new(2026, 1);

    private sealed class Provider : IFleetEngineServiceProvider
    {
        public int EngineStock = int.MaxValue;
        public int EnginePartsStock = int.MaxValue;
        public int AirframePartsStock = int.MaxValue;
        public int ReplacedEngines;
        public int ConsumedEngineParts;
        public int ConsumedAirframeParts;
        public string LastEngineId = string.Empty;
        public string LastEngineGroup = string.Empty;
        public int CompleteAirframeServices(string groupId, int requestedAircraft, int partsPerAircraft)
        {
            Assert.That(groupId, Is.EqualTo("airframe-group"));
            var count = System.Math.Min(requestedAircraft, AirframePartsStock / partsPerAircraft);
            var parts = checked(count * partsPerAircraft);
            AirframePartsStock -= parts; ConsumedAirframeParts += parts; return count;
        }
        public int CompleteEngineMaintenance(string engineDesignId, string groupId, int requestedAircraft,
            int engineCount, int partsPerEngine, bool replacement)
        {
            LastEngineId = engineDesignId; LastEngineGroup = groupId;
            if (replacement)
            {
                Assert.That(partsPerEngine, Is.Zero, "Whole replacement cannot also demand routine parts.");
                var count = System.Math.Min(requestedAircraft, EngineStock / engineCount);
                EngineStock -= count * engineCount; ReplacedEngines += count * engineCount; return count;
            }
            var quantity = checked(engineCount * partsPerEngine);
            var serviced = System.Math.Min(requestedAircraft, EnginePartsStock / quantity);
            EnginePartsStock -= serviced * quantity; ConsumedEngineParts += serviced * quantity; return serviced;
        }

    }

    private static FleetRegistry Registry(int count = 1, double utilization = 2000, double contractLife = 100000,
        int engineInterval = 500, int engineLife = 1000, int engineCount = 4, double expectedLife = 100000)
    {
        var offer = new ServiceOfferSnapshot(2000, 120, 21, 0, 0)
        {
            MaintenanceModelVersion = 2, SparePartsPerService = 3, SparePartUnitPrice = 7,
            AirframePartsGroupId = "airframe-group", EngineCount = engineCount,
            EngineMaintenance = new EngineMaintenanceSpecification("exact-engine-v1", "shared-engine-parts", .9,
                engineInterval, engineLife, 2, 3, 100),
        };
        var fleet = new FleetRegistry(89);
        fleet.RegisterDelivery("engine-delivery", "aircraft", "Aircraft", "contract", count, Delivery,
            new AircraftLifecycleSpecification(AircraftCategory.HeavyTransportWideBody, .9, 0, 2000, expectedLife, 120),
            new OperatingProfile("transport", utilization, 4), offer, contractLife, engineCount);
        return fleet;
    }

    [Test]
    public void EngineDemandCanSelectOneAircraftDesignSharingTheSameEngine()
    {
        var fleet = Registry(); var first = fleet.Cohorts.Single();
        fleet.RegisterDelivery("other-delivery", "other-aircraft", "Other aircraft", "other-contract", 2,
            Delivery, first.Specification, first.Profile, first.Offer, first.ContractLifeHours, first.EngineCount);
        var selected = fleet.GetEngineServiceDemand(Delivery.NextMonth(), "aircraft").Single();
        var other = fleet.GetEngineServiceDemand(Delivery.NextMonth(), "other-aircraft").Single();
        var total = fleet.GetEngineServiceDemand(Delivery.NextMonth()).Single();
        Assert.That(selected.EngineParts, Is.EqualTo(16));
        Assert.That(selected.ReplacementEngines, Is.EqualTo(8));
        Assert.That(other.EngineParts, Is.EqualTo(32));
        Assert.That(total.EngineParts, Is.EqualTo(selected.EngineParts + other.EngineParts));
        Assert.That(total.ReplacementEngines, Is.EqualTo(selected.ReplacementEngines + other.ReplacementEngines));
        Assert.That(fleet.GetEngineServiceDemand(Delivery.NextMonth(), "missing"), Is.Empty);
    }

    [Test]
    public void CoincidingReplacementSkipsRoutinePartsAndDoesNotResetAirframeAge()
    {
        var fleet = Registry(); var provider = new Provider();
        var demand = fleet.GetEngineServiceDemand(Delivery.NextMonth()).Single();
        Assert.That(demand.EngineParts, Is.EqualTo(16)); Assert.That(demand.ReplacementEngines, Is.EqualTo(8));
        Assert.That(demand.EngineDesignId, Is.EqualTo("exact-engine-v1"));
        var airframeDemand = fleet.GetServiceDemand(Delivery.NextMonth()).Single();
        Assert.That(airframeDemand.AirframeSpareParts, Is.EqualTo(3));
        Assert.That(airframeDemand.AirframePartsGroupId, Is.EqualTo("airframe-group"));
        var report = fleet.ProcessMonth(Delivery.NextMonth(), provider);
        Assert.That(provider.ConsumedEngineParts, Is.EqualTo(16)); Assert.That(provider.ReplacedEngines, Is.EqualTo(8));
        Assert.That(provider.ConsumedAirframeParts, Is.EqualTo(3));
        Assert.That(report.Revenue, Is.EqualTo(16 * 3 + 8 * 100 + 3 * 7));
        Assert.That(report.CompletedServices, Is.EqualTo(5));
        var cohort = fleet.Cohorts.Single();
        Assert.That(cohort.FlightHours, Is.EqualTo(2000)); Assert.That(cohort.Wear, Is.EqualTo(.02).Within(1e-9));
        Assert.That(cohort.EngineHoursSinceReplacement, Is.Zero); Assert.That(cohort.CompletedEngineServiceCycles, Is.Zero);
        Assert.That(cohort.CompletedServiceCycles, Is.EqualTo(1));
        Assert.That(provider.LastEngineId, Is.EqualTo("exact-engine-v1"));
        Assert.That(provider.LastEngineGroup, Is.EqualTo("shared-engine-parts"));
    }

    [Test]
    public void FourEngineAircraftCannotReplaceThreeOfItsEnginesOrConsumePartialStock()
    {
        var fleet = Registry(); var provider = new Provider { EngineStock = 3 };
        var report = fleet.ProcessMonth(Delivery.NextMonth(), provider);
        Assert.That(report.CompletedServices, Is.EqualTo(1)); Assert.That(report.Revenue, Is.EqualTo(24));
        Assert.That(report.GroundedUnits, Is.EqualTo(1)); Assert.That(report.AircraftHours, Is.EqualTo(1000));
        Assert.That(provider.EngineStock, Is.EqualTo(3)); Assert.That(provider.ReplacedEngines, Is.Zero);
        Assert.That(fleet.Cohorts.Single().EngineHoursSinceReplacement, Is.EqualTo(1000));
        Assert.That(fleet.GetEngineServiceDemand(Delivery.AddMonths(2)).Single().ReplacementEngines, Is.GreaterThanOrEqualTo(4));
        var stalled = fleet.ProcessMonth(Delivery.AddMonths(2), provider);
        Assert.That(stalled.AircraftHours, Is.Zero); Assert.That(stalled.Revenue, Is.Zero);
        Assert.That(provider.EngineStock, Is.EqualTo(3));
    }

    [Test]
    public void FourEngineAircraftCannotConsumePartsForOnlySomeInstalledEngines()
    {
        var fleet = Registry(); var provider = new Provider { EnginePartsStock = 7 };
        var report = fleet.ProcessMonth(Delivery.NextMonth(), provider);
        Assert.That(report.AircraftHours, Is.EqualTo(500)); Assert.That(report.CompletedServices, Is.Zero);
        Assert.That(report.Revenue, Is.Zero); Assert.That(report.GroundedUnits, Is.EqualTo(1));
        Assert.That(provider.EnginePartsStock, Is.EqualTo(7)); Assert.That(provider.ConsumedEngineParts, Is.Zero);
        Assert.That(provider.ReplacedEngines, Is.Zero);
        Assert.That(fleet.Cohorts.Single().EngineHoursSinceReplacement, Is.EqualTo(500));
        Assert.That(fleet.Cohorts.Single().CompletedEngineServiceCycles, Is.Zero);
    }

    [Test]
    public void MultiAircraftEngineShortageSplitsCohortWithoutErasingDebtOrCounts()
    {
        var fleet = Registry(count: 40); var provider = new Provider { EngineStock = 4 };
        fleet.ProcessMonth(Delivery.NextMonth(), provider);
        Assert.That(provider.ReplacedEngines, Is.EqualTo(4)); Assert.That(provider.EngineStock, Is.Zero);
        Assert.That(fleet.Cohorts.Count, Is.GreaterThan(20));
        Assert.That(fleet.Cohorts.Sum(c => c.OriginalCount), Is.EqualTo(40));
        foreach (var c in fleet.Cohorts)
            Assert.That(c.OriginalCount, Is.EqualTo(c.ActiveCount + c.AccidentLosses + c.RetiredCount));
        Assert.That(fleet.Cohorts.Any(c => c.FlightHours == 2000), Is.True);
        Assert.That(fleet.Cohorts.Any(c => c.FlightHours == 1000), Is.True);
        Assert.That(fleet.Cohorts.Where(c => c.ActiveCount > 0).All(c => c.Grounded), Is.True);
        var debt = fleet.GetEngineServiceDemand(Delivery.AddMonths(2)).Single();
        Assert.That(debt.ReplacementEngines, Is.GreaterThanOrEqualTo(fleet.ActiveUnits * 4));
        Assert.That(fleet.ProcessMonth(Delivery.AddMonths(2), new Provider()).CompletedServices, Is.GreaterThan(0));
    }

    [Test]
    public void BeforeEngineThresholdThereIsNoMonthlyEngineInvoice()
    {
        var fleet = Registry(utilization: 100); var provider = new Provider();
        for (var month = 1; month <= 4; month++)
        {
            Assert.That(fleet.GetEngineServiceDemand(Delivery.AddMonths(month)), Is.Empty);
            var report = fleet.ProcessMonth(Delivery.AddMonths(month), provider);
            Assert.That(report.Revenue, Is.Zero); Assert.That(report.CompletedServices, Is.Zero);
        }
        var due = fleet.ProcessMonth(Delivery.AddMonths(5), provider);
        Assert.That(due.CompletedServices, Is.EqualTo(1)); Assert.That(due.Revenue, Is.EqualTo(24));
        Assert.That(provider.ConsumedAirframeParts, Is.Zero); Assert.That(provider.ReplacedEngines, Is.Zero);
    }

    [Test]
    public void RetirementAtReplacementThresholdDoesNotBillUnneededEngine()
    {
        var fleet = Registry(contractLife: 1000, expectedLife: 1000); var provider = new Provider();
        var demand = fleet.GetEngineServiceDemand(Delivery.NextMonth()).Single();
        Assert.That(demand.ReplacementEngines, Is.Zero); Assert.That(demand.EngineParts, Is.EqualTo(8));
        var report = fleet.ProcessMonth(Delivery.NextMonth(), provider);
        Assert.That(report.Retirements, Is.EqualTo(1)); Assert.That(report.Revenue, Is.EqualTo(24));
        Assert.That(provider.ReplacedEngines, Is.Zero); Assert.That(fleet.ActiveUnits, Is.Zero);
        Assert.That(fleet.ProcessMonth(Delivery.AddMonths(2), provider).Revenue, Is.Zero);
    }

    [Test]
    public void SnapshotPreservesInstalledEngineVersionAgeTermsAndReplacementDebt()
    {
        var fleet = Registry(); fleet.ProcessMonth(Delivery.NextMonth(), new Provider { EngineStock = 0 });
        var saved = fleet.CreateSnapshot(); var restored = new FleetRegistry(1); restored.RestoreSnapshot(saved);
        Assert.That(restored.Cohorts.Single().EngineHoursSinceReplacement, Is.EqualTo(1000));
        Assert.That(restored.Cohorts.Single().CompletedEngineServiceCycles, Is.EqualTo(1));
        Assert.That(restored.Cohorts.Single().Offer.EngineMaintenance!.EngineDesignId, Is.EqualTo("exact-engine-v1"));
        Assert.That(restored.Cohorts.Single().Offer.EngineCount, Is.EqualTo(4));
        Assert.That(restored.GetEngineServiceDemand(Delivery.AddMonths(2)), Is.EqualTo(fleet.GetEngineServiceDemand(Delivery.AddMonths(2))));
        Assert.That(restored.ProcessMonth(Delivery.AddMonths(2), new Provider()),
            Is.EqualTo(fleet.ProcessMonth(Delivery.AddMonths(2), new Provider())));
        Assert.That(restored.Cohorts, Is.EqualTo(fleet.Cohorts));
        Assert.Throws<ArgumentOutOfRangeException>(() => restored.RestoreSnapshot(saved with
        { Cohorts = saved.Cohorts.Select(c => c with { EngineHoursSinceReplacement = double.NaN }).ToArray() }));
    }

    [TestCase(0)]
    [TestCase(1)]
    [Category("Extended")]
    public void RestoreRejectsUnsupportedMaintenanceModels(int model)
    {
        var fleet = Registry(); var snapshot = fleet.CreateSnapshot();
        var invalid = snapshot with { Cohorts = snapshot.Cohorts.Select(c => c with
            { Offer = c.Offer with { MaintenanceModelVersion = model } }).ToArray() };
        Assert.Throws<ArgumentOutOfRangeException>(() => fleet.RestoreSnapshot(invalid));
        Assert.That(fleet.Cohorts, Is.EqualTo(snapshot.Cohorts));
    }

    [Test]
    public void CombinedOfferCannotQuoteFewerEnginesThanTheInstalledFleet()
    {
        var fleet = Registry(); var saved = fleet.CreateSnapshot(); var c = saved.Cohorts.Single();
        var other = new FleetRegistry(1);
        Assert.Throws<ArgumentException>(() => other.RegisterDelivery("mismatch", c.DesignId, c.DesignName, c.ContractId,
            1, Delivery, c.Specification, c.Profile, c.Offer, c.ContractLifeHours, 2));
        Assert.That(other.ActiveUnits, Is.Zero);
        Assert.Throws<ArgumentException>(() => other.RestoreSnapshot(saved with
        { Cohorts = saved.Cohorts.Select(cohort => cohort with { EngineCount = 2 }).ToArray() }));
        Assert.That(other.ActiveUnits, Is.Zero);
        other.RestoreSnapshot(saved);
        Assert.That(other.Cohorts.Single().EngineCount, Is.EqualTo(other.Cohorts.Single().Offer.EngineCount));
    }

    [TestCase(200, 1000, 2400d, 10, 2, 400d, 2)]
    [TestCase(500, 2000, 4500d, 7, 2, 500d, 1)]
    [TestCase(1000, 3000, 6500d, 4, 2, 500d, 0)]
    [TestCase(2000, 6000, 13000d, 4, 2, 1000d, 0)]
    [TestCase(200, 1100, 2400d, 11, 2, 200d, 1)]
    [TestCase(200, 200, 600d, 0, 3, 0d, 0)]
    [Category("Extended")]
    public void ConfiguredEngineIntervalAndLifeDriveSeparateClockAndExactPhysicalConsumption(int interval,
        int life, double monthlyHours, int routineCycles, int replacements, double endingEngineHours, int endingCycles)
    {
        var fleet = Registry(utilization: monthlyHours, engineInterval: interval, engineLife: life, engineCount: 2);
        var provider = new Provider();
        var demand = fleet.GetEngineServiceDemand(Delivery.NextMonth()).Single();
        Assert.That(demand.EngineParts, Is.EqualTo(routineCycles * 2 * 2));
        Assert.That(demand.ReplacementEngines, Is.EqualTo(replacements * 2));
        var report = fleet.ProcessMonth(Delivery.NextMonth(), provider);
        Assert.That(provider.ConsumedEngineParts, Is.EqualTo(routineCycles * 2 * 2));
        Assert.That(provider.ReplacedEngines, Is.EqualTo(replacements * 2));
        Assert.That(report.Revenue, Is.EqualTo(provider.ConsumedEngineParts * 3 + provider.ReplacedEngines * 100
            + provider.ConsumedAirframeParts * 7));
        Assert.That(report.AircraftHours, Is.EqualTo(monthlyHours));
        var cohort = fleet.Cohorts.Single();
        Assert.That(cohort.FlightHours, Is.EqualTo(monthlyHours));
        Assert.That(cohort.Wear, Is.EqualTo(monthlyHours / 100000).Within(1e-9));
        Assert.That(cohort.EngineHoursSinceReplacement, Is.EqualTo(endingEngineHours));
        Assert.That(cohort.CompletedEngineServiceCycles, Is.EqualTo(endingCycles));
        Assert.That(cohort.CompletedServiceCycles, Is.EqualTo((int)(monthlyHours / 2000)));
    }

    [Test]
    public void TwoEngineShortagePreservesDebtHurtsReadinessAndReputationAndCatchesUpAfterStockArrives()
    {
        var fleet = Registry(utilization: 2400, engineInterval: 200, engineLife: 1100, engineCount: 2);
        var provider = new Provider { EnginePartsStock = 3 };
        var blocked = fleet.ProcessMonth(Delivery.NextMonth(), provider);
        Assert.That(provider.EnginePartsStock, Is.EqualTo(3), "All engines require four parts atomically.");
        Assert.That(blocked.GroundedUnits, Is.EqualTo(1)); Assert.That(blocked.GuaranteedShortfall, Is.EqualTo(1));
        Assert.That(blocked.AircraftHours, Is.EqualTo(200)); Assert.That(blocked.Revenue, Is.Zero);
        var reputation = new CompanyReputation();
        reputation.ProcessMonth(1, blocked);
        Assert.That(reputation.Score, Is.LessThan(50));
        var saved = fleet.CreateSnapshot(); var restored = new FleetRegistry(1); restored.RestoreSnapshot(saved);
        var stillBlocked = restored.ProcessMonth(Delivery.AddMonths(2), provider);
        Assert.That(stillBlocked.AircraftHours, Is.Zero); Assert.That(stillBlocked.AccidentLosses, Is.Zero);
        Assert.That(stillBlocked.Revenue, Is.Zero);
        Assert.That(restored.Cohorts.Single().EngineHoursSinceReplacement, Is.EqualTo(200));
        provider.EnginePartsStock = int.MaxValue;
        var resumed = restored.ProcessMonth(Delivery.AddMonths(3), provider);
        Assert.That(resumed.GroundedUnits, Is.Zero); Assert.That(resumed.AircraftHours, Is.EqualTo(2400));
        Assert.That(provider.ConsumedEngineParts, Is.EqualTo(48)); Assert.That(provider.ReplacedEngines, Is.EqualTo(4));
        Assert.That(resumed.Revenue, Is.EqualTo(565));
        var final = restored.Cohorts.Single();
        Assert.That(final.FlightHours, Is.EqualTo(2600)); Assert.That(final.EngineHoursSinceReplacement, Is.EqualTo(400));
        Assert.That(final.CompletedEngineServiceCycles, Is.EqualTo(2)); Assert.That(final.CompletedServiceCycles, Is.EqualTo(1));
    }

    [Test]
    public void TwoInstalledEnginesRequireTwoPhysicalReplacementsAndKeepTheirAgeWhileGrounded()
    {
        var fleet = Registry(utilization: 2400, engineInterval: 200, engineLife: 1100, engineCount: 2);
        var provider = new Provider { EngineStock = 1 };
        var blocked = fleet.ProcessMonth(Delivery.NextMonth(), provider);
        Assert.That(blocked.GroundedUnits, Is.EqualTo(1)); Assert.That(blocked.GuaranteedShortfall, Is.EqualTo(1));
        Assert.That(blocked.AircraftHours, Is.EqualTo(1100)); Assert.That(provider.EngineStock, Is.EqualTo(1));
        Assert.That(provider.ReplacedEngines, Is.Zero); Assert.That(provider.ConsumedEngineParts, Is.EqualTo(20));
        Assert.That(blocked.Revenue, Is.EqualTo(60));
        Assert.That(fleet.Cohorts.Single().EngineHoursSinceReplacement, Is.EqualTo(1100));
        Assert.That(fleet.ProcessMonth(Delivery.AddMonths(2), provider).Revenue, Is.Zero);
        Assert.That(fleet.Cohorts.Single().EngineHoursSinceReplacement, Is.EqualTo(1100));
        provider.EngineStock = 6;
        var resumed = fleet.ProcessMonth(Delivery.AddMonths(3), provider);
        Assert.That(resumed.GroundedUnits, Is.Zero); Assert.That(resumed.AircraftHours, Is.EqualTo(2400));
        Assert.That(provider.ReplacedEngines, Is.EqualTo(6)); Assert.That(provider.EngineStock, Is.Zero);
        Assert.That(fleet.Cohorts.Single().FlightHours, Is.EqualTo(3500));
        Assert.That(fleet.Cohorts.Single().EngineHoursSinceReplacement, Is.EqualTo(200));
    }

    [Test]
    public void NonMultipleEngineLifeMatchesCustomerQuoteAtTheAircraftRetirementEndpoint()
    {
        var fleet = Registry(utilization: 3000, contractLife: 2000, engineInterval: 500, engineLife: 1100, engineCount: 2, expectedLife: 2400);
        var initial = fleet.Cohorts.Single(); var provider = new Provider();
        Assert.That(initial.RetirementHours, Is.EqualTo(2200));
        var expected = LifecycleCostCalculator.Calculate(initial.RetirementHours, 0, 0, 0, initial.Offer);
        var demand = fleet.GetEngineServiceDemand(Delivery.NextMonth()).Single();
        Assert.That(demand.EngineParts, Is.EqualTo(16)); Assert.That(demand.ReplacementEngines, Is.EqualTo(2));
        var report = fleet.ProcessMonth(Delivery.NextMonth(), provider);
        Assert.That(report.Retirements, Is.EqualTo(1)); Assert.That(report.Revenue, Is.EqualTo(expected.ScheduledServiceCost));
        Assert.That(provider.ReplacedEngines, Is.EqualTo(2), "The replacement at retirement itself is unnecessary.");
        Assert.That(fleet.ProcessMonth(Delivery.AddMonths(2), provider).Revenue, Is.Zero);
    }

    [Test]
    public void NewlyProposedEngineTermsCannotRewritePreviouslyDeliveredOfferOrClock()
    {
        var fleet = Registry(utilization: 700, engineInterval: 200, engineLife: 1100, engineCount: 2);
        fleet.ProcessMonth(Delivery.NextMonth(), new Provider());
        var original = fleet.Cohorts.Single();
        var redesigned = original.Offer with
        { EngineMaintenance = original.Offer.EngineMaintenance! with { ServiceIntervalHours = 1000, ReplacementLifeHours = 4000, PartsPerService = 9 } };
        redesigned.Validate();
        var restored = new FleetRegistry(); restored.RestoreSnapshot(fleet.CreateSnapshot());
        Assert.That(restored.Cohorts.Single().Offer.EngineMaintenance!.ServiceIntervalHours, Is.EqualTo(200));
        Assert.That(restored.Cohorts.Single().Offer.EngineMaintenance!.ReplacementLifeHours, Is.EqualTo(1100));
        Assert.That(restored.Cohorts.Single().EngineHoursSinceReplacement, Is.EqualTo(700));
        Assert.That(restored.Cohorts.Single().CompletedEngineServiceCycles, Is.EqualTo(3));
        var provider = new Provider(); restored.ProcessMonth(Delivery.AddMonths(2), provider);
        Assert.That(provider.ReplacedEngines, Is.EqualTo(2));
        Assert.That(restored.Cohorts.Single().Offer, Is.EqualTo(original.Offer));
    }

    [Test]
    public void EnginePolicyUsesNamedPartsAndReplacementEconomicsAndFrozenExactVersion()
    {
        var engine = AircraftProductionFactoryTests.CreateCatalogItem("policy", 100, 1, 20).Aircraft.Engine;
        var terms = EngineMaintenancePolicy.Create(engine, "engine-v3", "anchored-parts-v1");
        Assert.That(terms.EngineDesignId, Is.EqualTo("engine-v3")); Assert.That(terms.PartsGroupId, Is.EqualTo("anchored-parts-v1"));
        Assert.That(terms.ServiceIntervalHours, Is.EqualTo(500)); Assert.That(terms.ReplacementLifeHours, Is.InRange(2000, 4000));
        Assert.That(terms.PartsPerService, Is.EqualTo(FleetServiceBalance.RequiredSpareParts(engine.Performance.Reliability)));
        Assert.That(terms.PartUnitPrice, Is.EqualTo(FleetServiceBalance.EnginePartUnitPrice(engine)).Within(1e-9));
        Assert.That(terms.ReplacementUnitPrice, Is.EqualTo(FleetServiceBalance.ReplacementEngineUnitPrice(engine)).Within(1e-9));
    }
}
