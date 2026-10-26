using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Tests.Factories;

namespace WrightTycoon.Core.Tests.Contracts;

[Category("Extended")]
public sealed class ServiceIntervalRequirementTests
{
    [TestCase(1, 200, 200, true)]
    [TestCase(1, 200, 500, true)]
    [TestCase(1, 500, 200, false)]
    [TestCase(1, 500, 500, true)]
    [TestCase(1, 500, 1000, true)]
    [TestCase(2, 200, 200, true)]
    [TestCase(2, 200, 500, true)]
    [TestCase(2, 200, 1000, true)]
    [TestCase(2, 200, 2000, true)]
    [TestCase(2, 200, 4000, true)]
    [TestCase(2, 500, 200, false)]
    [TestCase(2, 500, 500, true)]
    [TestCase(2, 500, 1000, true)]
    public void CivilAndGovernmentContractsRequireAtLeastTheRequestedInterval(int version, int required, int actual, bool accepted)
    {
        var original = AircraftProductionFactoryTests.CreateCatalogItem("interval", 50, 1, 10).Aircraft;
        var type = new AircraftTypeDefinition { Id = original.TypeDefinition.Id, FamilyId = original.TypeDefinition.FamilyId,
            DisplayName = "Interval", ServiceIntervalHours = actual };
        var aircraft = new WrightTycoon.Core.Aircraft.Aircraft(original.FamilyDefinition, type, original.Engine,
            original.Airframe, original.Performance, original.Development);
        var civil = new ContractRequirements { LifecycleModelVersion = version, MinimumExpectedLifeHours = 1000,
            MaximumServiceIntervalHours = required, MaximumOperatingCostPerUnit = 1e9, ProcurementPreference = ProcurementPreference.PurchasePrice };
        var contract = new Contract("interval", "Buyer", 5, 12, civil, 0, 1e6, 5e5, 1);
        var report = new ContractEvaluator().Evaluate(contract, aircraft);
        Assert.That(report.CanDeliver, Is.EqualTo(accepted), string.Join("; ", report.UnmetRequirements));

        var government = new GovernmentContractRequirements { LifecycleModelVersion = version, MinimumExpectedLifeHours = 1000,
            MaximumServiceIntervalHours = required, MaximumOperatingCostPerUnit = 1e9, ProcurementPreference = ProcurementPreference.PurchasePrice,
            RequiredAircraftType = AircraftCategory.RegionalJet, MinimumMaximumSpeedMetersPerSecond = 1, MinimumRangeKilometers = 1,
            MinimumCrewSize = 1, MaximumRequiredRunwayLengthMeters = 10000 };
        var candidate = new GovernmentContractSubmissionCandidate("interval", "Interval", AircraftLifecycleStage.Completed,
            new GovernmentContractAircraftMetrics(AircraftCategory.RegionalJet, 200, 200, 200, 1000, 1000, .9, 50, 2, 1000, 10000,
                new[] { AircraftCategory.RegionalJet }, 20000, actual, 120, 10, 100000,
                CombinedServiceOffer: LifecycleCostCalculator.CreateEngineLifecycleServiceOffer(aircraft)));
        var compliance = new GovernmentContractComplianceEvaluator().Evaluate(government, candidate);
        Assert.That(compliance.IsSelectableInUi, Is.EqualTo(accepted), string.Join("; ", compliance.UnmetRequirements));
        Assert.That(civil.MinimumServiceIntervalHours, Is.EqualTo(required));
        Assert.That(government.MinimumServiceIntervalHours, Is.EqualTo(required));
        if (!accepted)
        {
            Assert.That(report.UnmetRequirements, Does.Contain("Service interval is below contractual minimum."));
            Assert.That(compliance.UnmetRequirements, Does.Contain("Service interval is below contractual minimum."));
            Assert.That(new GovernmentContractComplianceEvaluator().Evaluate(government, candidate,
                serviceIntervalAlreadyAccepted: true).IsSelectableInUi, Is.True, "Already signed frozen bids must not be invalidated by the corrected comparison.");
        }
    }
}
