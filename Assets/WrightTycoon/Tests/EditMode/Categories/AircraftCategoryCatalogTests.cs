using NUnit.Framework;
using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.Tests.Categories;

[TestFixture]
public sealed class AircraftCategoryCatalogTests
{
    [Test]
    public void Catalog_ContainsAllTenProfiles()
    {
        var profiles = AircraftCategoryCatalog.GetAllProfiles();

        Assert.That(profiles.Count, Is.EqualTo(10));
        for (var index = 0; index < profiles.Count; index++)
        {
            var profile = profiles[index];
            Assert.That(AircraftCategoryCatalog.GetProfile(profile.Category), Is.SameAs(profile));
        }
    }

    [Test]
    public void Profiles_HaveConsistentRanges()
    {
        var profiles = AircraftCategoryCatalog.GetAllProfiles();
        for (var index = 0; index < profiles.Count; index++)
        {
            var profile = profiles[index];
            Assert.That(profile.MaxCrew, Is.GreaterThanOrEqualTo(profile.MinCrew));
            Assert.That(profile.MaxMaximumWeightKilograms, Is.GreaterThanOrEqualTo(profile.MinMaximumWeightKilograms));
            Assert.That(profile.MaxRequiredSpeedKilometersPerHour, Is.GreaterThanOrEqualTo(profile.MinRequiredSpeedKilometersPerHour));
            Assert.That(profile.MaxRequiredRangeKilometers, Is.GreaterThanOrEqualTo(profile.MinRequiredRangeKilometers));
            Assert.That(profile.MaxRequiredPayloadKilograms, Is.GreaterThanOrEqualTo(profile.MinRequiredPayloadKilograms));
        }
    }

    [Test]
    public void Profiles_ExposeRequestedGameplayModifiers()
    {
        AssertModifier(AircraftCategory.LightSportAndRecreational, weight: 50d, speed: -20d, reliability: 0d, fuelMultiplier: 1d);
        AssertModifier(AircraftCategory.LightMultiRolePiston, weight: 75d, speed: -10d, reliability: 0d, fuelMultiplier: 1d);
        AssertModifier(AircraftCategory.TurbopropRegional, weight: 250d, speed: 0d, reliability: 0d, fuelMultiplier: 1d);
        AssertModifier(AircraftCategory.RegionalJet, weight: 500d, speed: 0d, reliability: 0d, fuelMultiplier: 1d);
        AssertModifier(AircraftCategory.MediumTransportNarrowBody, weight: 1000d, speed: 10d, reliability: 0.1d, fuelMultiplier: 0.97d);
        AssertModifier(AircraftCategory.HeavyTransportWideBody, weight: 2500d, speed: 15d, reliability: 0.12d, fuelMultiplier: 0.95d);
        AssertModifier(AircraftCategory.SpecialCivil, weight: 500d, speed: -20d, reliability: 0d, fuelMultiplier: 1d);
        AssertModifier(AircraftCategory.BusinessVipJet, weight: 500d, speed: 5d, reliability: 0.1d, fuelMultiplier: 1d);
        AssertModifier(AircraftCategory.MilitaryTactical, weight: 2000d, speed: 20d, reliability: -0.1d, fuelMultiplier: 1d);
        AssertModifier(AircraftCategory.MilitaryStrategicSupport, weight: 4000d, speed: -15d, reliability: 0.12d, fuelMultiplier: 0.95d);
    }

    private static void AssertModifier(
        AircraftCategory category,
        double weight,
        double speed,
        double reliability,
        double fuelMultiplier)
    {
        var profile = AircraftCategoryCatalog.GetProfile(category);
        Assert.That(profile.WeightModifierKilograms, Is.EqualTo(weight).Within(0.0001d));
        Assert.That(profile.SpeedModifierMetersPerSecond, Is.EqualTo(speed).Within(0.0001d));
        Assert.That(profile.ReliabilityModifierDelta, Is.EqualTo(reliability).Within(0.0001d));
        Assert.That(profile.FuelConsumptionMultiplier, Is.EqualTo(fuelMultiplier).Within(0.0001d));
    }
}
