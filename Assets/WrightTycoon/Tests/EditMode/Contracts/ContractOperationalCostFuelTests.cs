using System;
using NUnit.Framework;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Tests.Factories;

namespace WrightTycoon.Core.Tests.Contracts;

[TestFixture]
public sealed class ContractOperationalCostFuelTests
{
    private static WrightTycoon.Core.Aircraft.Aircraft CreateAircraft()
    {
        var source = AircraftProductionFactoryTests.CreateCatalogItem("operation.fuel", 100, 2, 20).Aircraft;
        return new WrightTycoon.Core.Aircraft.Aircraft(source.FamilyDefinition, source.TypeDefinition, source.Engine,
            source.Airframe, source.Performance with { AverageCruiseFuelBurnKilogramsPerHour = 10d }, source.Development);
    }

    [Test]
    public void CivilInjectedFuelMarketAffectsEveryLifecycleOperationalOffer()
    {
        var aircraft = CreateAircraft();
        Contract Offer(int version) => new("fuel-v" + version, "Buyer", 20, 12, new ContractRequirements
        {
            LifecycleModelVersion = version, MinimumExpectedLifeHours = 1000d, MaximumOperatingCostPerUnit = 10000d,
            ProcurementPreference = ProcurementPreference.WholeLifeCost,
        }, 0, 1d);
        var calls = 0;
        ServiceOfferSnapshot FreeMaintenance(WrightTycoon.Core.Aircraft.Aircraft _, ContractRequirements __, double ___) =>
            LifecycleContractPolicy.ReferenceServiceOffer(0d);
        var low = new ContractEvaluator(maintenanceOfferProvider: FreeMaintenance, fuelPriceProvider: _ => { calls++; return 0.1d; });
        var high = new ContractEvaluator(maintenanceOfferProvider: FreeMaintenance, fuelPriceProvider: _ => { calls++; return 100d; });
        Assert.That(low.Evaluate(Offer(2), aircraft).CanDeliver, Is.True);
        Assert.That(high.Evaluate(Offer(2), aircraft).CanDeliver, Is.False);
        Assert.That(calls, Is.EqualTo(2));
        Assert.That(low.Evaluate(Offer(1), aircraft).CanDeliver, Is.True);
        Assert.That(high.Evaluate(Offer(1), aircraft).CanDeliver, Is.False);
        Assert.That(calls, Is.EqualTo(4), "Every lifecycle offer uses the current injected fuel market.");
    }

    [Test]
    public void GovernmentExplicitFuelQuoteIsFrozenAtAcceptanceAndSurvivesJsonRoundTrip()
    {
        var aircraft = CreateAircraft();
        const double quotedFuelPrice = 2.75d;
        var candidate = GovernmentContractSubmissionCandidateFactory.FromAircraft("operation.fuel", "Fuel candidate",
            AircraftLifecycleStage.Completed, aircraft, fuelPricePerKilogram: quotedFuelPrice);
        Assert.That(candidate.Metrics.FuelPricePerKilogram, Is.EqualTo(quotedFuelPrice));
        var requirements = new GovernmentContractRequirements
        {
            RequiredAircraftType = candidate.Metrics.AircraftType, MinimumMaximumSpeedMetersPerSecond = 1d,
            MinimumRangeKilometers = 1d, MinimumCrewSize = 1, MaximumRequiredRunwayLengthMeters = 5000d,
            LifecycleModelVersion = 2, MinimumExpectedLifeHours = 1000d, MaximumOperatingCostPerUnit = 1000000d,
            ProcurementPreference = ProcurementPreference.WholeLifeCost,
        };
        var contract = new GovernmentContract("fuel.locked", new SimulationDate(2000, 1), requirements);
        contract.Accept(new SimulationDate(2000, 1), candidate, 0d);
        contract.LockUnitPrice(100000d);
        var frozen = contract.LockedLifecycleCostQuote!;
        Assert.That(frozen.FuelCost, Is.EqualTo(((candidate.Metrics.ExpectedLifeHours + requirements.MinimumExpectedLifeHours) / 2d) * 10d * quotedFuelPrice).Within(1e-6d));
        var jsonConvert = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json", throwOnError: true)!;
        var serialize = jsonConvert.GetMethod("SerializeObject", new[] { typeof(object) })!;
        var deserialize = jsonConvert.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) })!;
        var json = (string)serialize.Invoke(null, new object[] { frozen })!;
        var restored = (LifecycleCostQuote)deserialize.Invoke(null, new object[] { json, typeof(LifecycleCostQuote) })!;
        Assert.That(restored, Is.EqualTo(frozen));
        Assert.That(restored.FuelCost / restored.ContractHours / 10d, Is.EqualTo(quotedFuelPrice).Within(1e-9d));
    }
}
