using System;
using NUnit.Framework;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Contracts;

[TestFixture]
public sealed class EngineOwnershipCostCalculatorTests
{
    private static EngineMaintenanceSpecification Terms => new("exact-engine-v7", "engine-parts-anchor", .9,
        500, 2000, 2, 10, 1000);

    [TestCase(0d, 0, 0, 0d)]
    [TestCase(499d, 0, 0, 0d)]
    [TestCase(500d, 0, 0, 0d)]
    [TestCase(501d, 1, 0, 80d)]
    [TestCase(999d, 1, 0, 80d)]
    [TestCase(1000d, 1, 0, 80d)]
    [TestCase(1001d, 2, 0, 160d)]
    [TestCase(1500d, 2, 0, 160d)]
    [TestCase(1501d, 3, 0, 240d)]
    [TestCase(2000d, 3, 0, 240d)]
    [TestCase(2001d, 3, 1, 4240d)]
    [TestCase(2500d, 3, 1, 4240d)]
    [TestCase(2501d, 4, 1, 4320d)]
    [TestCase(4000d, 6, 1, 4480d)]
    [TestCase(4001d, 6, 2, 8480d)]
    [Category("Extended")]
    public void CostsCountOnlyMaintenanceNeededBeforeTheExactOperatingHorizon(double hours, int cycles,
        int replacements, double expectedCost)
    {
        var cost = EngineOwnershipCostCalculator.Calculate(hours, Terms, 4);
        Assert.That(cost.RoutineCycles, Is.EqualTo(cycles)); Assert.That(cost.Replacements, Is.EqualTo(replacements));
        Assert.That(cost.RoutinePartsCost, Is.EqualTo(cycles * 4 * 2 * 10));
        Assert.That(cost.ReplacementCost, Is.EqualTo(replacements * 4 * 1000));
        Assert.That(cost.TotalCost, Is.EqualTo(expectedCost));
    }

    [TestCase(0d, 0d)]
    [TestCase(500d, 0d)]
    [TestCase(501d, 80d)]
    [TestCase(2000d, 240d)]
    [TestCase(2001d, 4240d)]
    [Category("Extended")]
    public void CustomerQuoteIncludesOriginalEnginesOnlyInPurchasePrice(double hours, double laterEngineCosts)
    {
        var offer = new ServiceOfferSnapshot(8000, 120, 1, 0, 0)
        {
            MaintenanceModelVersion = 2, SparePartsPerService = 1, SparePartUnitPrice = 1,
            AirframePartsGroupId = "airframe-anchor", EngineMaintenance = Terms, EngineCount = 4,
        };
        var quote = LifecycleCostCalculator.Calculate(hours, 100000, 0, 0, offer, 100);
        Assert.That(quote.PurchasePrice, Is.EqualTo(100000));
        Assert.That(quote.ScheduledServiceCost, Is.EqualTo(laterEngineCosts));
        Assert.That(quote.OperatingCost, Is.EqualTo(laterEngineCosts));
        Assert.That(quote.TotalCost, Is.EqualTo(100000 + laterEngineCosts));
    }

    [Test]
    public void ExactAirframeRetirementBoundaryDoesNotAddAServiceOrReplacementBill()
    {
        var offer = new ServiceOfferSnapshot(2000, 120, 25, 0, 0)
        {
            MaintenanceModelVersion = 2, SparePartsPerService = 1, SparePartUnitPrice = 25,
            AirframePartsGroupId = "airframe-anchor", EngineMaintenance = Terms, EngineCount = 4,
        };
        var ending = LifecycleCostCalculator.Calculate(2000, 100000, 0, 0, offer);
        var continuing = LifecycleCostCalculator.Calculate(2001, 100000, 0, 0, offer);
        Assert.That(ending.ScheduledServiceCost, Is.EqualTo(240));
        Assert.That(continuing.ScheduledServiceCost - ending.ScheduledServiceCost, Is.EqualTo(4025));
    }

    [Test]
    public void GovernmentBoardJsonRetainsFrozenCombinedTermsThroughAcceptanceAndDelivery()
    {
        var requirements = new GovernmentContractRequirements
        {
            RequiredAircraftType = AircraftCategory.RegionalJet, MinimumMaximumSpeedMetersPerSecond = 100,
            MinimumRangeKilometers = 100, MinimumCrewSize = 1, MaximumRequiredRunwayLengthMeters = 5000,
            LifecycleModelVersion = 1, MinimumExpectedLifeHours = 5000, MaximumServiceIntervalHours = 1000,
            MinimumSupportMonths = 240, MaximumOperatingCostPerUnit = 1000000000,
            ProcurementPreference = ProcurementPreference.WholeLifeCost,
        };
        var combined = new ServiceOfferSnapshot(1000, 120, 25, 0, 0)
        {
            MaintenanceModelVersion = 2, SparePartsPerService = 1, SparePartUnitPrice = 25,
            AirframePartsGroupId = "frozen-airframe-anchor", EngineCount = 4, EngineMaintenance = Terms,
        };
        var candidate = new GovernmentContractSubmissionCandidate("aircraft-design", "Aircraft", AircraftLifecycleStage.Completed,
            new GovernmentContractAircraftMetrics(AircraftCategory.RegionalJet, 200, 200, 200, 1000, 1000, .9, 50, 2, 1000, 10000,
                new[] { AircraftCategory.RegionalJet }, 12000, 1000, 120, 0, 100000,
                MaintenanceModelVersion: 2, CombinedServiceOffer: combined));
        var date = new SimulationDate(2000, 1);
        var board = new GovernmentContractBoard(new Generator(requirements), new ZeroRandom());
        board.ProcessMonth(date);
        Assert.That(board.TryAcceptActiveContract(date, candidate).IsAccepted, Is.True);
        var locked = board.ActiveContract!.LockedServiceOffer!;
        var quoted = board.ActiveContract.LockedLifecycleCostQuote;
        Assert.That(locked.GuaranteedSupportMonths, Is.EqualTo(240));
        var restored = new GovernmentContractBoard(new Generator(requirements), new ZeroRandom());
        restored.RestoreSnapshot(JsonRoundTrip(board.CreateSnapshot()));
        Assert.That(restored.ActiveContract!.LockedServiceOffer, Is.EqualTo(locked));
        Assert.That(restored.ActiveContract.AcceptedSubmission!.Metrics.CombinedServiceOffer, Is.EqualTo(combined));
        Assert.That(restored.ActiveContract.LockedLifecycleCostQuote, Is.EqualTo(quoted));
        for (var offset = 1; offset <= 6; offset++) restored.ProcessMonth(date.AddMonths(offset));
        Assert.That(restored.ActiveDeliveryContract, Is.Not.Null);
        var delivery = restored.ActiveDeliveryContract!;
        Assert.That(delivery.ServiceOffer, Is.EqualTo(locked));
        Assert.That(delivery.ServiceOffer!.EngineCount, Is.EqualTo(4));
        Assert.That(delivery.ServiceOffer.EngineMaintenance, Is.EqualTo(Terms));
        Assert.That(delivery.LifecycleAircraftMetrics!.Value.CombinedServiceOffer, Is.EqualTo(combined));
        board.RestoreSnapshot(JsonRoundTrip(restored.CreateSnapshot()));
        Assert.That(board.ActiveDeliveryContract!.ServiceOffer, Is.EqualTo(locked));
        Assert.That(board.ActiveDeliveryContract.LifecycleCostQuote, Is.EqualTo(quoted));
    }

    private static GovernmentContractBoardSnapshot JsonRoundTrip(GovernmentContractBoardSnapshot snapshot)
    {
        // Core tests retain no compile-time dependency on the optional JSON assembly.
        var json = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json", throwOnError: true)!;
        var serialized = (string)json.GetMethod("SerializeObject", new[] { typeof(object) })!.Invoke(null, new object[] { snapshot })!;
        return (GovernmentContractBoardSnapshot)json.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) })!
            .Invoke(null, new object[] { serialized, typeof(GovernmentContractBoardSnapshot) })!;
    }

    private sealed class Generator : IGovernmentContractGenerator
    {
        private readonly GovernmentContractRequirements _requirements;
        public Generator(GovernmentContractRequirements requirements) => _requirements = requirements;
        public GovernmentContract Generate(SimulationDate generatedOn, int sequence) => new("engine-quote-" + sequence, generatedOn, _requirements);
    }
    private sealed class ZeroRandom : IGovernmentContractRandomProvider
    {
        public int NextIntInclusive(int minInclusive, int maxInclusive) => minInclusive;
        public double NextDouble() => 0;
        public double NextDouble(double minInclusive, double maxExclusive) => minInclusive;
    }
}
