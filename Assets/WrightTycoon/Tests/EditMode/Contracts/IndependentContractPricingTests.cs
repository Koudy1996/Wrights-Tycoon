using System;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Tests.Factories;

namespace WrightTycoon.Core.Tests.Contracts;

public sealed class IndependentContractPricingTests
{
    [Test]
    public void CategoryReferenceRangesMatchApprovedBalance()
    {
        var ranges = new[]
        {
            (AircraftCategory.LightSportAndRecreational, 20d, 40d),
            (AircraftCategory.LightMultiRolePiston, 30d, 80d),
            (AircraftCategory.TurbopropRegional, 85d, 180d),
            (AircraftCategory.RegionalJet, 105d, 330d),
            (AircraftCategory.MediumTransportNarrowBody, 170d, 600d),
            (AircraftCategory.HeavyTransportWideBody, 650d, 1200d),
            (AircraftCategory.SpecialCivil, 40d, 300d),
            (AircraftCategory.BusinessVipJet, 80d, 250d),
            (AircraftCategory.MilitaryTactical, 110d, 370d),
            (AircraftCategory.MilitaryStrategicSupport, 375d, 950d),
            (AircraftCategory.LightCargo, 40d, 75d),
            (AircraftCategory.MediumCargo, 120d, 350d),
            (AircraftCategory.HeavyCargo, 375d, 900d),
            (AircraftCategory.LongRangeCargo, 750d, 1200d),
            (AircraftCategory.Bomber, 350d, 900d),
        };
        foreach (var (category, min, max) in ranges)
            Assert.That(WrightTycoon.Core.Balance.ContractBalance.ResolveReferenceStructuralIndexRange(category),
                Is.EqualTo((min, max)), category.ToString());
    }

    private static ContractRequirements Draft() => new()
    {
        LifecycleModelVersion = 2, MinimumExpectedLifeHours = 1000, MaximumOperatingCostPerUnit = 1,
        MinimumPayloadKilograms = 1, MaximumServiceIntervalHours = 1000,
        ProcurementPreference = ProcurementPreference.PurchasePrice,
    };

    [TestCase(5, 143750d)]
    [TestCase(49, 143750d, Category = "Extended")]
    [TestCase(50, 139437.50d, Category = "Extended")]
    [TestCase(149, 139437.50d, Category = "Extended")]
    [TestCase(150, 135125d, Category = "Extended")]
    [TestCase(500, 135125d, Category = "Extended")]
    public void PurchasePriceHasOnlyTheAgreedIndependentMultipliers(int units, double expected)
    {
        Assert.That(ContractPricingBalance.PurchasePrice(100000d, units), Is.EqualTo(expected));
    }

    [TestCase(OperationalCostPriority.PurchasePrice, 1.35d)]
    [TestCase(OperationalCostPriority.Balanced, 1.20d, Category = "Extended")]
    [TestCase(OperationalCostPriority.OperationalCosts, 1.10d)]
    public void EveryPriorityEnforcesOperationBudgetIncludingEqualityWithoutDiscountingPurchasePrice(OperationalCostPriority priority, double tolerance)
    {
        var calculator = new ContractPricingCalculator();
        var snapshot = calculator.CreateEconomicSnapshot(Draft(), new SimulationDate(1949, 1), 20, priority);
        var requirements = Draft().WithEconomics(snapshot);
        Assert.That(requirements.MaximumOperationCostPerHour, Is.EqualTo(snapshot.ReferenceOperationCostPerHour * tolerance).Within(1e-9));
        var original = AircraftProductionFactoryTests.CreateCatalogItem("budget", 100, 1, 20).Aircraft;
        var aircraft = new WrightTycoon.Core.Aircraft.Aircraft(original.FamilyDefinition, original.TypeDefinition,
            original.Engine, original.Airframe, original.Performance with { AverageCruiseFuelBurnKilogramsPerHour = 10d }, original.Development);
        var contract = new Contract("budget", "Buyer", 20, 12, requirements, 0, snapshot.OfferedUnitPrice);
        var life = LifecycleCalculator.CalculateExpectedLifeHours(aircraft.FamilyDefinition.Category, aircraft.Performance.OverallReliability,
            aircraft.TypeDefinition.ServiceIntervalHours, aircraft.TypeDefinition.ConstructionDurabilityLevel);
        var hours = LifecycleCostCalculator.ExpectedOperatingLifeHours(requirements.MinimumExpectedLifeHours, life);
        var fuelAtLimit = (requirements.MaximumOperationCostPerHour - snapshot.OfferedUnitPrice / hours) / 10d;
        ServiceOfferSnapshot FreeService(WrightTycoon.Core.Aircraft.Aircraft _, ContractRequirements __, double ___) =>
            LifecycleContractPolicy.ReferenceServiceOffer(0);
        var equal = new ContractEvaluator(maintenanceOfferProvider: FreeService, fuelPriceProvider: _ => fuelAtLimit);
        var above = new ContractEvaluator(maintenanceOfferProvider: FreeService, fuelPriceProvider: _ => fuelAtLimit + 1d);
        Assert.That(equal.Evaluate(contract, aircraft).CanDeliver, Is.True);
        Assert.That(above.Evaluate(contract, aircraft).UnmetRequirements, Does.Contain("Operational costs exceed customer budget."));
        Assert.That(calculator.CalculateQuote(requirements, 20).UnitPrice, Is.EqualTo(snapshot.OfferedUnitPrice));
        Assert.That(new ContractPricingCalculator(reputationPriceMultiplierProvider: () => 1.15d).CalculateQuote(requirements, 20).UnitPrice,
            Is.EqualTo(snapshot.OfferedUnitPrice), "Published price cannot change with current reputation.");
    }

    [Test]
    public void CustomerFuelAndMaintenanceChangeBudgetButNeverThePurchasePrice()
    {
        var r = Draft();
        var c = new ContractPricingCalculator();
        var snapshot = c.CreateEconomicSnapshot(r, new SimulationDate(1949, 1), 20);
        var reference = snapshot.Reference;
        var low = ContractOperatingBudgetCalculator.ReferenceCostPerHour(reference, snapshot.OfferedUnitPrice, r, .1d);
        var high = ContractOperatingBudgetCalculator.ReferenceCostPerHour(reference, snapshot.OfferedUnitPrice, r, 1d);
        Assert.That(high, Is.GreaterThan(low));
        var longer = new ContractRequirements { LifecycleModelVersion = 2, MinimumExpectedLifeHours = 1000,
            MinimumPayloadKilograms = 1, MaximumOperatingCostPerUnit = 999999999, MaximumServiceIntervalHours = 4000 };
        Assert.That(c.CreateEconomicSnapshot(longer, snapshot.GeneratedOn, 20).OfferedUnitPrice, Is.EqualTo(snapshot.OfferedUnitPrice));
        Assert.That(reference.DirectUnitCost, Is.EqualTo(reference.AirframeDirectCost + reference.EngineCount * reference.EngineDirectCost));
        var service = reference.ServiceOffer(1000, 120);
        Assert.That(service.EngineCount, Is.EqualTo(reference.EngineCount));
        Assert.That(service.EngineMaintenance!.ReplacementUnitPrice, Is.EqualTo(reference.EngineDirectCost * EngineMaintenancePolicy.ReplacementPriceMultiplier));
    }

    [Test]
    public void CivilOffersAndLegacyPricesSurviveRealJsonSnapshotRestore()
    {
        var generator = new ContractGenerator();
        var board = new ContractBoard(generator, new ContractEvaluator());
        board.GenerateContractsForNextMonth();
        var original = board.OpenContracts[0];
        Assert.That(original.Requirements.PricingModelVersion, Is.EqualTo(1));
        var snapshot = RoundTrip(board.CreateSnapshot());
        var restored = new ContractBoard(new ContractGenerator(), new ContractEvaluator());
        restored.RestoreSnapshot(snapshot);
        Assert.That(restored.OpenContracts[0].Requirements.EconomicSnapshot, Is.EqualTo(original.Requirements.EconomicSnapshot));
        Assert.That(restored.OpenContracts[0].UnitPrice, Is.EqualTo(original.UnitPrice));
        snapshot.OpenContracts[0].Requirements.EconomicSnapshot = null;
        snapshot.OpenContracts[0].UnitPrice = 0d;
        restored.RestoreSnapshot(RoundTrip(snapshot));
        Assert.That(restored.OpenContracts[0].UnitPrice, Is.Zero);
        Assert.That(restored.OpenContracts[0].Requirements.PricingModelVersion, Is.Zero);
    }

    [Test]
    public void GovernmentOfferPriceIsFrozenBeforeFinalOrderSizeAndLegacyRemainsLegacy()
    {
        var generator = new GovernmentContractGenerator(new SystemGovernmentContractRandomProvider());
        var contract = generator.Generate(new SimulationDate(1949, 1), 0);
        var economic = contract.Requirements.EconomicSnapshot!;
        var requirements = RoundTrip(contract.Requirements);
        requirements.Validate();
        Assert.That(requirements.EconomicSnapshot, Is.EqualTo(economic));
        Assert.That(economic.QuantityMultiplier, Is.EqualTo(1d));
        var pricing = new GovernmentContractPricingService(() => 1.15d);
        Assert.That(pricing.CalculateQuote(requirements, 1).UnitPrice, Is.EqualTo(economic.OfferedUnitPrice));
        var volume = pricing.CalculateQuote(requirements, 1500);
        Assert.That(volume.UnitPrice, Is.EqualTo(economic.OfferedUnitPrice));
        Assert.That(volume.TotalContractValue, Is.EqualTo(ContractPricingBalance.RoundCurrency(economic.OfferedUnitPrice * 1500)));
        Assert.Throws<InvalidOperationException>(() => contract.LockUnitPrice(economic.OfferedUnitPrice + 1));
    }

    [Test]
    [Category("Extended")]
    public void InvalidNewSnapshotsFailRatherThanSilentlyRepricing()
    {
        var snapshot = new ContractPricingCalculator().CreateEconomicSnapshot(Draft(), new SimulationDate(1949, 1), 20);
        Assert.Throws<ArgumentOutOfRangeException>(() => Draft().WithEconomics(snapshot with { OfferedUnitPrice = 0 }));
        var r = Draft().WithEconomics(snapshot);
        Assert.Throws<InvalidOperationException>(() => new Contract("bad", "Buyer", 20, 12, r, 0, snapshot.OfferedUnitPrice + 1));
    }

    [Test]
    public void GovernmentPurchasePreferenceAlsoEnforcesIndependentOperatingLimit()
    {
        var aircraft = AircraftProductionFactoryTests.CreateCatalogItem("gov.budget", 100, 1, 20).Aircraft;
        var candidate = GovernmentContractSubmissionCandidateFactory.FromAircraft("gov.budget", "Candidate", AircraftLifecycleStage.Completed, aircraft);
        var draft = new GovernmentContractRequirements
        {
            RequiredAircraftType = candidate.Metrics.QualifiedAircraftTypes[0],
            MinimumMaximumSpeedMetersPerSecond = 1, MinimumRangeKilometers = 1, MinimumCrewSize = 1,
            MaximumRequiredRunwayLengthMeters = 5000, LifecycleModelVersion = 2, MinimumExpectedLifeHours = 1000,
            MaximumOperatingCostPerUnit = 1, ProcurementPreference = ProcurementPreference.PurchasePrice,
        };
        var snapshot = new ContractPricingCalculator().CreateEconomicSnapshot(draft.AsCivilRequirements(), new SimulationDate(1949, 1), 1);
        var r = draft.WithEconomics(snapshot);
        var equalQuote = new LifecycleCostQuote(snapshot.OfferedUnitPrice,
            snapshot.AllowedOperationCostPerHour * 1000d - snapshot.OfferedUnitPrice, 0, 0, 0, 0, 1000);
        var evaluator = new GovernmentContractComplianceEvaluator();
        Assert.That(evaluator.Evaluate(r, candidate, snapshot.OfferedUnitPrice, equalQuote).UnmetRequirements,
            Does.Not.Contain("Operational costs exceed customer budget."));
        Assert.That(evaluator.Evaluate(r, candidate, snapshot.OfferedUnitPrice, equalQuote with { FuelCost = equalQuote.FuelCost + 1000 }).UnmetRequirements,
            Does.Contain("Operational costs exceed customer budget."));
    }

    [TestCase(1946)]
    [TestCase(1960)]
    [TestCase(1980)]
    [Category("Extended")]
    public void GeneratedMarketReferenceMatrixHasPositivePricesAndConsistentOperatingBudgets(int year)
    {
        var generator = new ContractGenerator(currentDateProvider: () => new SimulationDate(year, 1));
        for (var month = 1; month <= 12; month++)
        foreach (var contract in generator.GenerateMonthlyContracts(month))
        {
            var e = contract.Requirements.EconomicSnapshot!;
            e.Validate();
            Assert.That(contract.UnitPrice, Is.GreaterThan(0));
            Assert.That(contract.Requirements.MaximumOperationCostPerHour, Is.EqualTo(e.AllowedOperationCostPerHour).Within(1e-6));
            Assert.That(e.ReferenceFuelPricePerKilogram,
                Is.EqualTo(new MonthlyAviationFuelMarket().Get(e.GeneratedOn, e.Reference.UsesAviationGasoline).DollarsPerKilogram));
        }
    }

    private static T RoundTrip<T>(T source)
    {
        var json = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json", true)!;
        var text = (string)json.GetMethod("SerializeObject", new[] { typeof(object) })!.Invoke(null, new object[] { source! })!;
        return (T)json.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) })!.Invoke(null, new object[] { text, typeof(T) })!;
    }
}
