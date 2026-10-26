using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

[Category("Extended")]
public sealed class ContractLifecycleTextTests
{
    [Test]
    public void EveryNewPricingPriorityDisplaysAnEnforcedLimitRatherThanAReference()
    {
        var draft = new ContractRequirements { LifecycleModelVersion = 2, MinimumExpectedLifeHours = 1000,
            MaximumOperatingCostPerUnit = 1, MinimumPayloadKilograms = 1, ProcurementPreference = ProcurementPreference.PurchasePrice };
        foreach (OperationalCostPriority priority in Enum.GetValues(typeof(OperationalCostPriority)))
        {
            var snapshot = new ContractPricingCalculator().CreateEconomicSnapshot(draft, new WrightTycoon.Core.Simulation.SimulationDate(1949, 1), 20, priority);
            var text = string.Join("\n", ContractLifecycleText.Requirements(draft.WithEconomics(snapshot)));
            Assert.That(text, Does.Contain("Operational costs: <= $").And.Not.Contain("(reference)"));
            Assert.That(text, Does.Contain(priority == OperationalCostPriority.Balanced ? "Priority: balanced"
                : priority == OperationalCostPriority.PurchasePrice ? "Priority: purchase price" : "Priority: operational costs"));
        }
    }
    [Test]
    public void OldSavedRequirementNameIsReadAsMinimumAndShownWithCorrectComparison()
    {
        const string json = "{\"LifecycleModelVersion\":2,\"MinimumExpectedLifeHours\":15000,\"MaximumServiceIntervalHours\":200}";
        var civil = Newtonsoft.Json.JsonConvert.DeserializeObject<ContractRequirements>(json)!;
        var government = Newtonsoft.Json.JsonConvert.DeserializeObject<GovernmentContractRequirements>(json)!;
        Assert.That(civil.MinimumServiceIntervalHours, Is.EqualTo(200));
        Assert.That(government.MinimumServiceIntervalHours, Is.EqualTo(200));
        Assert.That(ContractLifecycleText.Requirements(civil), Does.Contain("- Service interval: >= 200 h"));
        Assert.That(ContractLifecycleText.Requirements(government), Does.Contain("- Service interval: >= 200 h"));
    }

    [TestCase(1)]
    [TestCase(2)]
    public void ContractSupportShowsOnlyCalendarDurationNotMonthlyFlightUtilization(int version)
    {
        var civil = new ContractRequirements { LifecycleModelVersion = version, MinimumExpectedLifeHours = 10000,
            MaximumServiceIntervalHours = 2000, MinimumSupportMonths = 600, UtilizationHoursPerMonth = 220,
            MaximumOperatingCostPerUnit = 250000, ProcurementPreference = ProcurementPreference.WholeLifeCost };
        var state = new GovernmentContractRequirements { LifecycleModelVersion = version, MinimumExpectedLifeHours = 10000,
            MaximumServiceIntervalHours = 2000, MinimumSupportMonths = 600, UtilizationHoursPerMonth = 220,
            MaximumOperatingCostPerUnit = 250000, ProcurementPreference = ProcurementPreference.WholeLifeCost };
        var lines = ContractLifecycleText.Requirements(civil);
        Assert.That(ContractLifecycleText.Requirements(state), Is.EqualTo(lines));
        Assert.That(lines.Single(s => s.StartsWith("- Support:")),
            Is.EqualTo("- Support: 50 years/delivery"));
        Assert.That(lines.All(s => s.StartsWith("- ")), Is.True);
        var text = string.Join("\n", lines);
        Assert.That(text, Does.Not.Contain("h/month").And.Not.Contain("monthly").And.Not.Contain("220"));
        Assert.That(text, Does.Contain("Operational costs: <= $25,00/h"));
        Assert.That(text, Does.Contain("Service interval: >= 2 000 h"));
        Assert.That(civil.UtilizationHoursPerMonth, Is.EqualTo(220));
        Assert.That(state.UtilizationHoursPerMonth, Is.EqualTo(220));
    }
    [Test]
    public void CivilAndGovernmentCardsAndNotebookShareVisibleLifeCostAndContractSupportLabels()
    {
        var civil = new ContractRequirements { LifecycleModelVersion = 1, MinimumExpectedLifeHours = 12000,
            MaximumServiceIntervalHours = 2000, MinimumSupportMonths = 240, MaximumOperatingCostPerUnit = 500000,
            ProcurementPreference = ProcurementPreference.WholeLifeCost };
        var state = new GovernmentContractRequirements { LifecycleModelVersion = 1, MinimumExpectedLifeHours = 12000,
            MaximumServiceIntervalHours = 2000, MinimumSupportMonths = 240, MaximumOperatingCostPerUnit = 500000,
            ProcurementPreference = ProcurementPreference.WholeLifeCost };
        var lines = ContractLifecycleText.Requirements(civil);
        Assert.That(ContractLifecycleText.Requirements(state), Is.EqualTo(lines));
        var text = string.Join("\n", lines);
        Assert.That(text, Does.Contain("Expected life").And.Contain("12 000"));
        Assert.That(text, Does.Contain("Operational costs: <= $41,67/h"));
        Assert.That(text, Does.Contain("Support: 20 years/delivery"));
        Assert.That(text, Does.Contain("operational costs"));
    }

    [Test]
    public void LegacyContractsExplicitlySayTermsWereNotSpecifiedInsteadOfInventingNewRequirements()
    {
        var civil = ContractLifecycleText.Requirements(new ContractRequirements());
        var state = ContractLifecycleText.Requirements(new GovernmentContractRequirements());
        Assert.That(state, Is.EqualTo(civil));
        Assert.That(civil.All(s => s.StartsWith("- ") && s.Contains("unspecified")), Is.True);
        Assert.That(string.Join("\n", civil), Does.Not.Contain("$0"));
    }

    [Test]
    public void ComparisonDistinguishesAircraftLifetimeAndCustomerLifecycleCostsFromManufacturerProfit()
    {
        var quote = new LifecycleCostQuote(100000, 10000, 20000, 1000, 2000, 3000, 12000);
        var text = string.Join("\n", ContractLifecycleText.Comparison(14000, 12000, quote, 1));
        Assert.That(text, Does.Contain("Aircraft Expected Life: 14 000"));
        Assert.That(text, Does.Contain("Operational Costs (Customer): $10,83"));
        Assert.That(text, Does.Contain("fuel").And.Contain("scheduled maintenance").And.Contain("Downtime and risk are excluded"));
    }

    [Test]
    public void NewOperationalRequirementsAndComparisonUseHourlyCostsAndExcludeLegacyRiskAllowances()
    {
        var r = new GovernmentContractRequirements { LifecycleModelVersion = 2, MinimumExpectedLifeHours = 10000,
            MaximumOperatingCostPerUnit = 250000, ProcurementPreference = ProcurementPreference.WholeLifeCost };
        var requirements = string.Join("\n", ContractLifecycleText.Requirements(r));
        Assert.That(requirements, Does.Contain("Operational costs: <= $25,00/h"));
        Assert.That(requirements, Does.Not.Contain("depreciation").And.Not.Contain("legacy"));
        var quote = new LifecycleCostQuote(100000, 10000, 20000, 1000, 2000, 3000, 10000);
        var text = string.Join("\n", ContractLifecycleText.Comparison(12000, 10000, quote, 2));
        Assert.That(text, Does.Contain("Operational Costs (Customer): $13,00 / flight hour"));
        Assert.That(text, Does.Contain("Downtime and risk are excluded"));
    }

    [Test]
    public void DemandLifecycleReferencesTrackGeneratorPolicyForEveryCategory()
    {
        foreach (var profile in WrightTycoon.Core.Categories.AircraftCategoryCatalog.GetAllProfiles())
        {
            var category = profile.Category;
            var text = string.Join("\n", DemandLifecycleText.ForCategory(category));
            Assert.That(text, Does.Contain(WrightTycoon.Core.Presentation.FinancialValueFormatter.FormatNumber(LifecycleContractPolicy.RequiredLife(category)) + " flight hours"));
            Assert.That(DemandLifecycleText.ForCategory(category).Count, Is.EqualTo(2));
            Assert.That(text, Does.Contain("Civil Maximum Fuel-burn Requirement Range"));
            foreach (var hidden in new[] { "Minimum Airframe Service Interval", "Guaranteed Support", "Operating Profile",
                "Reference Maximum Operational Costs", "Operational Costs", "Buyer Preference", "Engine Service", "Budget reference" })
                Assert.That(text, Does.Not.Contain(hidden), $"{category}: '{hidden}' must stay hidden in Demand only.");
        }
    }

    [Test]
    public void DemandAvailabilityMatchesContractYearGates()
    {
        foreach (var profile in WrightTycoon.Core.Categories.AircraftCategoryCatalog.GetAllProfiles())
        {
            var text = DemandLifecycleText.Availability(profile.Category, 1946, 1949);
            var expected = WrightTycoon.Core.Balance.ContractBalance.IsCategoryAvailableForYear(profile.Category, 1946)
                ? "1946: available by year" : "1946: not yet available";
            Assert.That(text, Does.Contain(expected).And.Contain("1949:").And.Contain("campaign unlocks"));
        }
    }

    [Test]
    public void AgreedSupportLabelUsesTheFrozenOfferRatherThanTheCurrentContractMinimum()
    {
        var offer = new WrightTycoon.Core.Fleet.ServiceOfferSnapshot(1000, 180, 100, 1, 1);
        Assert.That(ContractLifecycleText.AgreedSupport(offer), Does.Contain("Frozen").And.Contain("15 years"));
    }

    [Test]
    public void DesignerNoLongerHasAPlayerEditableGuaranteedSupportField()
    {
        Assert.That(typeof(WrightTycoonLaunchRunner).GetField("_typeGuaranteedSupportMonths",
            BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
    }
}
