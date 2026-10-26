using System;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class FleetLifecycleTests
{
    private static readonly SimulationDate Delivery = new(2026, 1);
    private sealed class Supply : IFleetEngineServiceProvider, IFleetMaintenanceCostProvider
    {
        public int AirframeParts = int.MaxValue;
        public double AirframeUnitCost;
        public double LastConsumptionCost { get; private set; }
        public int CompleteAirframeServices(string groupId, int requestedAircraft, int partsPerAircraft)
        {
            var count = System.Math.Min(requestedAircraft, AirframeParts / partsPerAircraft);
            AirframeParts -= count * partsPerAircraft; LastConsumptionCost = count * partsPerAircraft * AirframeUnitCost; return count;
        }
        public int CompleteEngineMaintenance(string engineDesignId, string groupId, int requestedAircraft,
            int engineCount, int partsPerEngine, bool replacement) { LastConsumptionCost = 0; return requestedAircraft; }
    }
    private static ServiceOfferSnapshot Offer(int interval = 500, int support = 120, int engines = 2) =>
        new(interval, support, 100, 0, 0)
        {
            SparePartsPerService = 1, SparePartUnitPrice = 100, AirframePartsGroupId = "airframe",
            EngineCount = engines,
            EngineMaintenance = new EngineMaintenanceSpecification("engine-v1", "engine-parts", .9, 8000, 1000000, 1, 0, 0),
        };
    private static FleetRegistry Registry(int count = 1, double utilization = 2500, double life = 100000, int support = 120, double expectedLife = 100000)
    {
        var fleet = new FleetRegistry(89);
        fleet.RegisterDelivery("delivery", "design", "Test", "contract", count, Delivery,
            new AircraftLifecycleSpecification(AircraftCategory.HeavyTransportWideBody, .9, 0, 500, expectedLife, support),
            new OperatingProfile("transport", utilization, 4), Offer(500, support), life, 2);
        return fleet;
    }
    [Test]
    public void DeliveryMonthHasNoRetroactiveExposureAndDuplicateDeliveryIsIdempotent()
    {
        var fleet = Registry();
        Assert.That(fleet.ProcessMonth(Delivery, new Supply()).AircraftHours, Is.Zero);
        Assert.That(fleet.RegisterDelivery("delivery", "design", "Other", "other", 20, Delivery,
            fleet.Cohorts[0].Specification, fleet.Cohorts[0].Profile, fleet.Cohorts[0].Offer, 10000), Is.False);
        Assert.That(fleet.ActiveUnits, Is.EqualTo(1));
    }
    [Test]
    public void MonthCrossesFiveAirframeServiceThresholds()
    {
        var fleet = Registry(); var supply = new Supply();
        var demand = fleet.GetServiceDemand(Delivery.NextMonth()).Single();
        Assert.That(demand.AirframeSpareParts, Is.EqualTo(5));
        var report = fleet.ProcessMonth(Delivery.NextMonth(), supply);
        Assert.That(report.AircraftHours, Is.EqualTo(2500));
        Assert.That(report.CompletedServices, Is.EqualTo(5)); Assert.That(report.Revenue, Is.EqualTo(500));
        Assert.That(fleet.Cohorts.Single().FlightHours, Is.EqualTo(2500));
    }
    [Test]
    public void ShortagePreservesServiceDebtAndGroundedAircraftHaveNoExposure()
    {
        var fleet = Registry(); var noSupply = new Supply { AirframeParts = 0 };
        var first = fleet.ProcessMonth(Delivery.NextMonth(), noSupply);
        Assert.That(first.AircraftHours, Is.EqualTo(500)); Assert.That(first.OverdueServices, Is.EqualTo(1));
        Assert.That(first.GuaranteedShortfall, Is.EqualTo(1)); Assert.That(first.Revenue, Is.Zero);
        var second = fleet.ProcessMonth(Delivery.AddMonths(2), noSupply);
        Assert.That(second.AircraftHours, Is.Zero); Assert.That(second.AccidentLosses, Is.Zero);
        Assert.That(fleet.Cohorts.Single().CompletedServiceCycles, Is.Zero);
        var resumed = fleet.ProcessMonth(Delivery.AddMonths(3), new Supply());
        Assert.That(resumed.AircraftHours, Is.EqualTo(2500)); Assert.That(resumed.CompletedServices, Is.EqualTo(6));
        Assert.That(fleet.Cohorts.Single().Wear, Is.EqualTo(.03d).Within(1e-10));
    }
    [Test]
    public void PartialFulfillmentSplitsPhasesAndConservesAllAircraft()
    {
        var fleet = Registry(40); var first = fleet.ProcessMonth(Delivery.NextMonth(), new Supply { AirframeParts = 1 });
        Assert.That(first.CompletedServices, Is.EqualTo(1)); Assert.That(fleet.Cohorts.Count, Is.GreaterThan(20));
        Assert.That(fleet.Cohorts.Sum(c => c.OriginalCount), Is.EqualTo(40));
        foreach (var c in fleet.Cohorts)
            Assert.That(c.OriginalCount, Is.EqualTo(c.ActiveCount + c.AccidentLosses + c.RetiredCount));
        Assert.That(fleet.Cohorts.Select(c => c.CohortId).Distinct().Count(), Is.EqualTo(fleet.Cohorts.Count));
        Assert.That(fleet.Cohorts.Any(c => c.FlightHours == 1000), Is.True);
        Assert.That(fleet.Cohorts.Any(c => c.FlightHours == 500), Is.True);
    }
    [Test]
    public void RetirementOccursAtImmutableOperatingLifeThresholdBeforeFurtherExposure()
    {
        var fleet = Registry(20, 2500, 100, expectedLife: 100);
        var thresholds = fleet.Cohorts.Select(c => c.RetirementHours).ToArray();
        Assert.That(thresholds.Min(), Is.GreaterThanOrEqualTo(90)); Assert.That(thresholds.Max(), Is.LessThanOrEqualTo(110));
        var report = fleet.ProcessMonth(Delivery.NextMonth(), new Supply());
        Assert.That(fleet.ActiveUnits, Is.Zero); Assert.That(report.Retirements + report.AccidentLosses, Is.EqualTo(20));
        Assert.That(report.CompletedServices, Is.Zero); Assert.That(report.AircraftHours, Is.EqualTo(thresholds.Sum()).Within(.0001));
    }
    [Test]
    public void SavedRandomStateAndCohortPhasesReproduceSubsequentMonths()
    {
        var fleet = Registry(1000); fleet.ProcessMonth(Delivery.NextMonth(), new Supply { AirframeParts = 100 });
        var saved = fleet.CreateSnapshot(); var restored = new FleetRegistry(1); restored.RestoreSnapshot(saved);
        var report = fleet.ProcessMonth(Delivery.AddMonths(2), new Supply());
        var restoredReport = restored.ProcessMonth(Delivery.AddMonths(2), new Supply());
        Assert.That(restoredReport, Is.EqualTo(report));
        Assert.That(restored.CreateSnapshot().AccidentRandomState, Is.EqualTo(fleet.CreateSnapshot().AccidentRandomState));
        Assert.That(restored.Cohorts, Is.EqualTo(fleet.Cohorts));
        Assert.Throws<InvalidOperationException>(() => restored.ProcessMonth(Delivery.AddMonths(2), new Supply()));
    }
    [Test]
    public void ForecastDoesNotConsumeRandomStateAndGuaranteesOutliveExpectedRetirement()
    {
        var fleet = Registry(10, 100, 100, expectedLife: 100); var before = fleet.CreateSnapshot();
        var forecast = fleet.GetSupportForecast("design", Delivery, 4);
        Assert.That(forecast.Last().GuaranteedUnits, Is.EqualTo(10)); Assert.That(forecast.Last().ActiveUnits, Is.Zero);
        Assert.That(fleet.CreateSnapshot().AccidentRandomState, Is.EqualTo(before.AccidentRandomState));
    }
    [Test]
    public void WarrantyDoesNotBlockEndingSupportAndSupportCanBeResumed()
    {
        var fleet = Registry(10);
        Assert.That(fleet.TryEndSupport("design", Delivery, false, out var affected), Is.True); Assert.That(affected, Is.EqualTo(10));
        Assert.That(fleet.IsSupported("design"), Is.False);
        var report = fleet.ProcessMonth(Delivery.NextMonth(), new Supply()); Assert.That(report.CompletedServices, Is.Zero);
        fleet.ResumeSupport("design"); Assert.That(fleet.IsSupported("design"), Is.True);
        Assert.That(fleet.ProcessMonth(Delivery.AddMonths(2), new Supply()).CompletedServices, Is.GreaterThan(0));
    }
    [Test]
    public void SupportGuaranteeIncludesLastMonthAndExpiresAfterIt()
    {
        var fleet = Registry(support: 12);
        Assert.That(fleet.TryEndSupport("design", Delivery.AddMonths(11), false, out var affected), Is.True);
        Assert.That(affected, Is.EqualTo(1));
        Assert.That(fleet.TryEndSupport("design", Delivery.AddMonths(12), false, out _), Is.True);
    }
    [Test]
    public void HourBasedAndFlightBasedRatesRespectTheirOwnExposureUnits()
    {
        var hourly = new AccidentRate(10, AccidentExposureUnit.FlightHours100000, 1, .25, .8, "test", true);
        var flights = hourly with { Unit = AccidentExposureUnit.Flights1000000 };
        var shortHourly = FleetAccidentCatalog.LossIntensity(hourly, 1000, 1, .8, 0, 0, false);
        Assert.That(shortHourly, Is.EqualTo(.01d * 10d * .75d * .5d).Within(1e-12));
        Assert.That(FleetAccidentCatalog.LossIntensity(hourly, 1000, 1, .8, 0, 0, true),
            Is.EqualTo(.01d * 10d * .25d * .5d).Within(1e-12));
        Assert.That(FleetAccidentCatalog.LossIntensity(hourly, 1000, 4, .8, 0, 0, false), Is.EqualTo(shortHourly));
        Assert.That(FleetAccidentCatalog.LossIntensity(flights, 1000, 1, .8, 0, 0, false),
            Is.EqualTo(4 * FleetAccidentCatalog.LossIntensity(flights, 1000, 4, .8, 0, 0, false)));
        Assert.That(FleetAccidentCatalog.LossIntensity(hourly, 0, 1, .8, 2, 2, true), Is.Zero);
    }
    [Test]
    public void TechnicalRiskRespondsToWearButOperationalRiskDoesNotDisappear()
    {
        var rate = FleetAccidentCatalog.Get(Delivery, AircraftCategory.LightSportAndRecreational, "ga");
        var normal = FleetAccidentCatalog.LossIntensity(rate, 100, 1, .75, 1, 1, true);
        Assert.That(FleetAccidentCatalog.LossIntensity(rate, 100, 1, .75, 1.5, 1.2, true), Is.GreaterThan(normal));
        Assert.That(FleetAccidentCatalog.LossIntensity(rate, 100, 1, 1, 0, 0, false), Is.GreaterThan(0));
    }
    [Test]
    [Category("Extended")]
    public void RestoreRejectsNonFiniteHoursAndUnknownDelivery()
    {
        var fleet = Registry(); var snapshot = fleet.CreateSnapshot();
        var corrupted = snapshot with { Cohorts = snapshot.Cohorts.Select(c => c with { FlightHours = double.PositiveInfinity }).ToArray() };
        Assert.Throws<ArgumentOutOfRangeException>(() => fleet.RestoreSnapshot(corrupted));
        Assert.Throws<ArgumentException>(() => fleet.RestoreSnapshot(snapshot with { RegisteredDeliveryIds = Array.Empty<string>() }));
    }

    [Test]
    public void ReferenceFleetLossMeanAndVarianceMatchChosenGameIntensityAcrossSeeds()
    {
        var date = new SimulationDate(1960, 1);
        var rate = FleetAccidentCatalog.Get(date, AircraftCategory.LightSportAndRecreational, "ga");
        var probability = 1 - System.Math.Exp(-rate.Rate * rate.HullLossFraction * .01d * .5d);
        var expectedMean = 1000 * probability;
        var expectedVariance = expectedMean * (1 - probability);
        var outcomes = new double[256];
        for (var i = 0; i < outcomes.Length; i++)
        {
            var fleet = new FleetRegistry((ulong)(i + 1));
            fleet.RegisterDelivery("sample", "sample", "sample", "sample", 1000, date.AddMonths(-1),
                new AircraftLifecycleSpecification(AircraftCategory.LightSportAndRecreational, .75, 0, 8000, 100000, 120),
                new OperatingProfile("ga", 1000, 1), Offer(8000, 120, 1), 100000);
            outcomes[i] = fleet.ProcessMonth(date, new Supply()).AccidentLosses;
        }
        var mean = outcomes.Average();
        var variance = outcomes.Sum(x => (x - mean) * (x - mean)) / (outcomes.Length - 1);
        Assert.That(mean, Is.EqualTo(expectedMean).Within(5 * System.Math.Sqrt(expectedVariance / outcomes.Length)));
        Assert.That(variance, Is.InRange(expectedVariance * .65d, expectedVariance * 1.35d));
    }

    [Test]
    public void SingleAircraftBinsHaveCorrectLossProbabilityAndTechnicalCauseDistribution()
    {
        var date = new SimulationDate(1960, 1);
        var rate = FleetAccidentCatalog.Get(date, AircraftCategory.LightSportAndRecreational, "ga");
        var probability = 1 - System.Math.Exp(-rate.Rate * rate.HullLossFraction * .01d * .5d);
        var losses = 0; var technical = 0;
        for (var i = 1; i <= 256; i++)
        {
            var fleet = new FleetRegistry((ulong)i);
            // Twenty retirement bins each contain exactly one aircraft. A binomial boundary error
            // must not make these singleton bins immortal or force their causes to operational.
            fleet.RegisterDelivery("single", "single", "single", "single", 20, date.AddMonths(-1),
                new AircraftLifecycleSpecification(AircraftCategory.LightSportAndRecreational, .75, 0, 8000, 100000, 120),
                new OperatingProfile("ga", 1000, 1), Offer(8000, 120, 1), 100000);
            var report = fleet.ProcessMonth(date, new Supply());
            losses += report.AccidentLosses; technical += report.TechnicalLosses;
        }
        var expected = 256 * 20 * probability;
        Assert.That(losses, Is.EqualTo(expected).Within(5 * System.Math.Sqrt(expected * (1 - probability))));
        var expectedTechnical = losses * rate.TechnicalFraction;
        Assert.That(technical, Is.EqualTo(expectedTechnical).Within(5 * System.Math.Sqrt(expectedTechnical * (1 - rate.TechnicalFraction))));
        Assert.That(technical, Is.GreaterThan(0));
    }
    [Test]
    public void RetirementIsCenteredOnMeanOfContractAndDesignLife()
    {
        var fleet = Registry(20, life: 20000, expectedLife: 40000);
        Assert.That(fleet.Cohorts.Average(c => c.RetirementHours), Is.EqualTo(30000).Within(1e-7));
        Assert.That(fleet.Cohorts.All(c => c.RetirementHours >= 27000 && c.RetirementHours <= 33000), Is.True);
        Assert.That(fleet.Cohorts.All(c => c.ContractLifeHours == 20000), Is.True);
    }

    [Test]
    public void PreviousRetirementPolicyMigratesOnceWithoutResettingClocksOrRandomState()
    {
        var fleet = Registry(life: 20000, expectedLife: 40000);
        fleet.ProcessMonth(Delivery.NextMonth(), new Supply { AirframeParts = 1 });
        var current = fleet.CreateSnapshot();
        var previous = current with { Cohorts = current.Cohorts.Select(c => c with
        { LifetimePolicyVersion = 0, RetirementHours = c.ContractLifeHours * (c.RetirementHours / 30000d + .1d) }).ToArray() };
        var restored = new FleetRegistry(1); restored.RestoreSnapshot(previous);
        Assert.That(restored.Cohorts.Single().RetirementHours, Is.EqualTo(current.Cohorts.Single().RetirementHours).Within(1e-7));
        Assert.That(restored.Cohorts.Single() with { RetirementHours = current.Cohorts.Single().RetirementHours }, Is.EqualTo(current.Cohorts.Single()));
        Assert.That(restored.CreateSnapshot().AccidentRandomState, Is.EqualTo(current.AccidentRandomState));
        var roundtrip = restored.CreateSnapshot(); restored.RestoreSnapshot(roundtrip);
        Assert.That(restored.Cohorts, Is.EqualTo(roundtrip.Cohorts));
    }

    [Test]
    public void WarrantyCountsUniqueAircraftAndFailureOverridesEarlierCompletedService()
    {
        var fleet = Registry();
        var report = fleet.ProcessMonth(Delivery.NextMonth(), new Supply { AirframeParts = 1 });
        Assert.That(report.CompletedServices, Is.EqualTo(1));
        Assert.That(report.WarrantyAircraft, Is.EqualTo(1));
        Assert.That(report.WarrantyServicedAircraft, Is.Zero);
        Assert.That(report.WarrantyMissedAircraft, Is.EqualTo(1));
    }

    [Test]
    public void ActualConsumedProductionCostAndRevenueAggregateByDesign()
    {
        var fleet = Registry(); var first = fleet.Cohorts.Single();
        fleet.RegisterDelivery("second", "second-design", "Second", "contract-2", 1, Delivery,
            first.Specification, first.Profile, first.Offer, first.ContractLifeHours, first.EngineCount);
        var report = fleet.ProcessMonth(Delivery.NextMonth(), new Supply { AirframeUnitCost = 20 });
        Assert.That(report.CompletedServices, Is.EqualTo(10));
        Assert.That(report.WarrantyServicedAircraft, Is.EqualTo(2), "Five services count once per aircraft.");
        Assert.That(report.ProductionCost, Is.EqualTo(200));
        Assert.That(report.Revenue, Is.EqualTo(1000));
        Assert.That(report.MaintenanceByDesign.Count, Is.EqualTo(2));
        Assert.That(report.MaintenanceByDesign["design"].Revenue, Is.EqualTo(500));
        Assert.That(report.MaintenanceByDesign["design"].ProductionCost, Is.EqualTo(100));
        Assert.That(report.MaintenanceByDesign["design"].Profit, Is.EqualTo(400));
    }

    [Test]
    public void WarrantyCalendarDoesNotChangeMaintenanceProcessingPriority()
    {
        var fleet = Registry(support: 1); var first = fleet.Cohorts.Single();
        var guaranteed = first.Offer with { GuaranteedSupportMonths = 120 };
        fleet.RegisterDelivery("new", "new-design", "New", "new-contract", 1, Delivery,
            first.Specification, first.Profile, guaranteed, first.ContractLifeHours, first.EngineCount);
        var report = fleet.ProcessMonth(Delivery.NextMonth(), new Supply { AirframeParts = 1 });
        Assert.That(report.MaintenanceByDesign.ContainsKey("design"), Is.True);
        Assert.That(report.MaintenanceByDesign.ContainsKey("new-design"), Is.False);
        Assert.That(report.WarrantyMissedAircraft, Is.EqualTo(1));
    }

    [Test]
    public void RemainingWarrantyDemandIncludesDebtAndDisabledSupportAndExcludesExpiredMonths()
    {
        var fleet = Registry(utilization: 1000, support: 3); var saved = fleet.CreateSnapshot();
        fleet.RestoreSnapshot(saved with { Cohorts = saved.Cohorts.Select(c => c with
        { Offer = c.Offer with { EngineMaintenance = c.Offer.EngineMaintenance! with
            { ServiceIntervalHours = 500, ReplacementLifeHours = 1000, PartsPerService = 2 } } }).ToArray() });
        var full = fleet.GetGuaranteeMaintenanceDemand("design", Delivery.NextMonth());
        Assert.That(full, Is.EqualTo(new GuaranteeMaintenanceDemand(4, 8, 4)));
        fleet.TryEndSupport("design", Delivery, false, out _);
        Assert.That(fleet.GetGuaranteeMaintenanceDemand("design", Delivery.NextMonth()), Is.EqualTo(full));
        fleet.ProcessMonth(Delivery.NextMonth(), new Supply());
        var remaining = fleet.GetGuaranteeMaintenanceDemand("design", Delivery.AddMonths(2));
        Assert.That(remaining, Is.EqualTo(new GuaranteeMaintenanceDemand(3, 8, 2)));
        Assert.That(fleet.GetGuaranteeMaintenanceDemand("design", Delivery.AddMonths(3)),
            Is.EqualTo(new GuaranteeMaintenanceDemand(0, 0, 0)));
    }

    [Test]
    public void BaselineUsesHistoricalReferenceReliabilityAndActualFlownExposure()
    {
        var fleet = Registry(utilization: 100); var initial = fleet.Cohorts.Single();
        var rate = FleetAccidentCatalog.Get(Delivery.NextMonth(), initial.Specification.Category, initial.Profile.Id);
        var report = fleet.ProcessMonth(Delivery.NextMonth(), new Supply());
        Assert.That(report.ExpectedAccidentLosses,
            Is.EqualTo(FleetAccidentCatalog.BaselineLossProbability(rate, 100, initial.Profile.AverageFlightHours)).Within(1e-12));
        var stopped = Registry(); stopped.ProcessMonth(Delivery.NextMonth(), new Supply { AirframeParts = 0 });
        var grounded = stopped.ProcessMonth(Delivery.AddMonths(2), new Supply { AirframeParts = 0 });
        Assert.That(grounded.ExpectedAccidentLosses, Is.Zero);
        Assert.That(grounded.ActiveAircraftAtStart, Is.EqualTo(1));
        Assert.That(grounded.WarrantyMissedAircraft, Is.EqualTo(1));
    }

}
