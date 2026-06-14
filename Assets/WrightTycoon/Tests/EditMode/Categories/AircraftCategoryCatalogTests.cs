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
        AssertModifier(AircraftCategory.LightSportAndRecreational, weight: -350d, speed: 0d, reliability: 0d, fuelMultiplier: 1.02d);
        AssertModifier(AircraftCategory.LightMultiRolePiston, weight: 75d, speed: -6d, reliability: 0d, fuelMultiplier: 0.98d);
        AssertModifier(AircraftCategory.TurbopropRegional, weight: 500d, speed: 0d, reliability: 0d, fuelMultiplier: 0.9d);
        AssertModifier(AircraftCategory.RegionalJet, weight: 1500d, speed: 0d, reliability: 0d, fuelMultiplier: 0.88d);
        AssertModifier(AircraftCategory.MediumTransportNarrowBody, weight: 3000d, speed: 6d, reliability: 0.1d, fuelMultiplier: 0.84d);
        AssertModifier(AircraftCategory.HeavyTransportWideBody, weight: 5000d, speed: 8d, reliability: 0.12d, fuelMultiplier: 0.72d);
        AssertModifier(AircraftCategory.SpecialCivil, weight: 2000d, speed: -8d, reliability: 0d, fuelMultiplier: 0.92d);
        AssertModifier(AircraftCategory.BusinessVipJet, weight: 1500d, speed: 3d, reliability: 0.1d, fuelMultiplier: 0.8d);
        AssertModifier(AircraftCategory.MilitaryTactical, weight: 3000d, speed: 45d, reliability: -0.1d, fuelMultiplier: 1.08d);
        AssertModifier(AircraftCategory.MilitaryStrategicSupport, weight: 4000d, speed: -5d, reliability: 0.12d, fuelMultiplier: 0.78d);
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
