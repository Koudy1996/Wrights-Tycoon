using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Tests.Factories;

namespace WrightTycoon.Core.Tests.Contracts;

public sealed class ContractSupportOwnershipTests
{
    [Test]
    public void CivilEvaluationUsesTheSameCanonicalSharedPartTariffAsDelivery()
    {
        var aircraft = AircraftProductionFactoryTests.CreateCatalogItem("shared.tariff", 100, 2, 20).Aircraft;
        var ordinary = LifecycleCostCalculator.CreateEngineLifecycleServiceOffer(aircraft);
        var shared = ordinary with { SparePartUnitPrice = 0, ServicePricePerEvent = 0,
            EngineMaintenance = ordinary.EngineMaintenance! with { PartUnitPrice = 0, ReplacementUnitPrice = 0 } };
        const double requirementsLife = 2000;
        var hours = LifecycleCostCalculator.ExpectedOperatingLifeHours(requirementsLife, AircraftLifecycleSpecification.FromAircraft(aircraft).ExpectedLifeHours);
        const double purchase = 100000;
        var low = LifecycleCostCalculator.Calculate(hours, purchase, aircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour,
            LifecycleCostCalculator.FuelPricePerKilogram(aircraft), shared).OperationCosts;
        var high = LifecycleCostCalculator.Calculate(hours, purchase, aircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour,
            LifecycleCostCalculator.FuelPricePerKilogram(aircraft), ordinary).OperationCosts;
        Assert.That(high, Is.GreaterThan(low));
        var requirements = new ContractRequirements { LifecycleModelVersion = 1, MaintenanceModelVersion = 2,
            MinimumExpectedLifeHours = requirementsLife, MaximumServiceIntervalHours = 200, MinimumSupportMonths = 120,
            ProcurementPreference = ProcurementPreference.WholeLifeCost, MaximumOperatingCostPerUnit = (low + high) / 2 / hours * requirementsLife };
        var contract = new Contract("shared", "Buyer", 5, 12, requirements, 0, purchase, 50000, 1);
        Assert.That(new ContractEvaluator().Evaluate(contract, aircraft).CanDeliver, Is.False);
        var evaluator = new ContractEvaluator(maintenanceOfferProvider: (_, _, _) => shared);
        Assert.That(evaluator.Evaluate(contract, aircraft).CanDeliver, Is.True);
    }

    [Test]
    public void CivilContractUsesOnlySeparateFrozenAirframeAndEngineMaintenance()
    {
        var aircraft = AircraftProductionFactoryTests.CreateCatalogItem("parts.offer", 100, 2, 20).Aircraft;
        var requirements = new ContractRequirements { MaintenanceModelVersion = 2, LifecycleModelVersion = 2,
            MinimumExpectedLifeHours = 10000, MinimumSupportMonths = 240 };
        var offer = LifecycleCostCalculator.CreateContractServiceOffer(aircraft, requirements, .8);
        Assert.That(offer.MaintenanceModelVersion, Is.EqualTo(2));
        Assert.That(offer.GuaranteedSupportMonths, Is.EqualTo(240));
        Assert.That(offer.SparePartsPerService, Is.EqualTo(3));
        Assert.That(offer.EngineMaintenance, Is.Not.Null);
        Assert.That(offer.EngineCount, Is.EqualTo(2));
        Assert.That(offer.ExpectedRepairCostPerHour, Is.Zero);
        var quote = LifecycleCostCalculator.QuoteForContract(aircraft, requirements, 100000, .8);
        var cycles = System.Math.Max(0, System.Math.Ceiling(quote.ContractHours / offer.ServiceIntervalHours) - 1);
        Assert.That(quote.ScheduledServiceCost, Is.EqualTo(cycles * offer.ServicePricePerEvent
            + EngineOwnershipCostCalculator.Calculate(quote.ContractHours, offer.EngineMaintenance!, offer.EngineCount).TotalCost));
    }

    [TestCase(0)]
    [TestCase(1)]
    [Category("Extended")]
    public void UnsupportedMaintenanceVersionsAreRejected(int version)
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            new ContractRequirements { MaintenanceModelVersion = version }.Validate());
    }

    [Test]
    public void WarrantyDurationChangesNeitherServiceTariffCustomerCostsNorContractPrice()
    {
        var aircraft = AircraftProductionFactoryTests.CreateCatalogItem("warranty", 100, 2, 20).Aircraft;
        ContractRequirements Terms(int months) => new()
        {
            RequiredCategory = aircraft.FamilyDefinition.Category, LifecycleModelVersion = 2,
            MinimumExpectedLifeHours = 10000, MinimumSupportMonths = months,
            MaximumOperatingCostPerUnit = 1e9, ProcurementPreference = ProcurementPreference.WholeLifeCost,
        };
        var shortTerms = Terms(60); var longTerms = Terms(240);
        Assert.That(LifecycleCostCalculator.QuoteForContract(aircraft, shortTerms, 100000),
            Is.EqualTo(LifecycleCostCalculator.QuoteForContract(aircraft, longTerms, 100000)));
        var pricing = new ContractPricingCalculator();
        Assert.That(pricing.CalculateQuote(shortTerms, 20).UnitPrice,
            Is.EqualTo(pricing.CalculateQuote(longTerms, 20).UnitPrice));
        Assert.That(LifecycleCostCalculator.CreateContractServiceOffer(aircraft, longTerms).GuaranteedSupportMonths, Is.EqualTo(240));
    }

    [TestCase(60)]
    [TestCase(240)]
    public void ContractDurationOverridesAircraftSettingAndDoesNotChangeServiceTariff(int supportMonths)
    {
        var a = AircraftProductionFactoryTests.CreateCatalogItem("contract.support", 100, 2, 20).Aircraft;
        var type = new AircraftTypeDefinition { Id = a.TypeDefinition.Id, FamilyId = a.TypeDefinition.FamilyId,
            DisplayName = "Short aircraft support", ServiceIntervalHours = 1000, GuaranteedSupportMonths = 1 };
        var shortSupport = new WrightTycoon.Core.Aircraft.Aircraft(a.FamilyDefinition, type, a.Engine, a.Airframe, a.Performance, a.Development);
        var requirements = new ContractRequirements { MaintenanceModelVersion = 2, LifecycleModelVersion = 1, MinimumExpectedLifeHours = 10000,
            MinimumSupportMonths = supportMonths, MaximumServiceIntervalHours = 1000, MaximumOperatingCostPerUnit = 1e9 };
        var offered = LifecycleCostCalculator.CreateContractServiceOffer(shortSupport, requirements);
        Assert.That(offered.GuaranteedSupportMonths, Is.EqualTo(supportMonths));
        Assert.That(offered.ServicePricePerEvent, Is.EqualTo(LifecycleCostCalculator.CreateServiceOffer(a).ServicePricePerEvent));
        var quote = LifecycleCostCalculator.QuoteForContract(shortSupport, requirements, 100000);
        Assert.That(quote, Is.EqualTo(LifecycleCostCalculator.QuoteForContract(a, requirements, 100000)));
        Assert.That(quote.SupportRiskCost, Is.Zero);

        var fleet = new FleetRegistry(17);
        var spec = AircraftLifecycleSpecification.FromAircraft(shortSupport) with { GuaranteedSupportMonths = offered.GuaranteedSupportMonths };
        var date = new WrightTycoon.Core.Simulation.SimulationDate(2000, 1);
        fleet.RegisterDelivery("delivered", "design", "Design", "contract", 5, date, spec,
            new OperatingProfile("piston", 100, 2), offered, 10000, 2);
        Assert.That(fleet.Cohorts[0].SupportEndMonth, Is.EqualTo(SimulationMonthId.FromDate(date).Value + supportMonths - 1));
        Assert.That(fleet.Cohorts[0].Specification.GuaranteedSupportMonths, Is.EqualTo(supportMonths));
    }
}
