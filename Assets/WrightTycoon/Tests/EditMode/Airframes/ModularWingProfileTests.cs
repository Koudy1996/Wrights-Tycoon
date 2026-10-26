using System;
using NUnit.Framework;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Physics;

namespace WrightTycoon.Core.Tests.Airframes;

[TestFixture]
public sealed class ModularWingProfileTests
{
    [Test]
    public void AerobaticLowSpeedProfileUsesSymmetricNacaReference()
    {
        var profile = WingProfileCalculator.Calculate(new AirframeParameters { WingPayloadAgility = 100 });
        Assert.That(profile.Name, Is.EqualTo("NACA 0012 — modified"));
    }

    [Test]
    public void EveryEfficiencyStepTradesMoreCostForBetterTechnicalParameters()
    {
        for (var speed = 100; speed <= 2000; speed += 50)
        for (var orientation = -100; orientation <= 100; orientation += 5)
        {
            var p = new AirframeParameters { WingDesignSpeedKph = speed, WingPayloadAgility = orientation, WingProfileEfficiency = 0 };
            var previous = WingProfileCalculator.Calculate(p);
            for (var efficiency = 5; efficiency <= 100; efficiency += 5)
            {
                p.WingProfileEfficiency = efficiency;
                var next = WingProfileCalculator.Calculate(p);
                Assert.That(next.WingMassMultiplier, Is.LessThan(previous.WingMassMultiplier));
                Assert.That(next.ProfileDragCoefficient, Is.LessThan(previous.ProfileDragCoefficient));
                Assert.That(next.MaximumLiftCoefficient, Is.GreaterThan(previous.MaximumLiftCoefficient));
                Assert.That(next.DesignLoadFactor, Is.GreaterThan(previous.DesignLoadFactor));
                Assert.That(next.WingCostMultiplier, Is.GreaterThan(previous.WingCostMultiplier));
                Assert.That(next.WingWorkMultiplier, Is.GreaterThan(previous.WingWorkMultiplier));
                previous = next;
            }
        }
    }

    [TestCase(100, 2.10, .0200, .18, 1.10, 1.05, "NACA 4418")]
    [TestCase(200, 1.85, .0140, .15, .95, .95, "NACA 4415")]
    [TestCase(350, 1.60, .0100, .12, 1.00, 1.00, "NACA 23012")]
    [TestCase(500, 1.40, .0080, .12, 1.05, 1.15, "NACA 63-412")]
    [TestCase(750, 1.20, .0075, .10, 1.15, 1.35, "NACA 65-210")]
    [TestCase(1000, 1.05, .0085, .08, 1.30, 1.65, "NASA SC(2)-0410")]
    [TestCase(1400, .85, .0100, .05, 1.55, 2.10, "NACA 64A204")]
    [TestCase(2000, .65, .0120, .03, 1.90, 2.80, "NACA 64A003")]
    [Category("Extended")]
    public void SpeedAnchorsMatchAcceptedTable(double speed, double lift, double drag, double thickness, double mass, double cost, string reference)
    {
        var profile = WingProfileCalculator.Calculate(new AirframeParameters { WingDesignSpeedKph = speed });
        Assert.That(profile.MaximumLiftCoefficient, Is.EqualTo(lift).Within(1e-10));
        Assert.That(profile.ProfileDragCoefficient, Is.EqualTo(drag).Within(1e-10));
        Assert.That(profile.ThicknessRatio, Is.EqualTo(thickness).Within(1e-10));
        Assert.That(profile.WingMassMultiplier, Is.EqualTo(mass).Within(1e-10));
        Assert.That(profile.WingCostMultiplier, Is.EqualTo(cost).Within(1e-10));
        Assert.That(profile.WingWorkMultiplier, Is.EqualTo(cost).Within(1e-10));
        Assert.That(profile.Name, Is.EqualTo(reference + " — modified"));
        Assert.That(profile.Inspiration, Is.EqualTo(reference));
    }

    [Test]
    public void AcceptedWorkedExampleMatches()
    {
        var profile = WingProfileCalculator.Calculate(new AirframeParameters { WingPayloadAgility = -100, WingProfileEfficiency = 75 });
        Assert.That(profile.MaximumLiftCoefficient, Is.EqualTo(2.05).Within(1e-10));
        Assert.That(profile.ProfileDragCoefficient, Is.EqualTo(.01035).Within(1e-10));
        Assert.That(profile.WingMassMultiplier, Is.EqualTo(.864).Within(1e-10));
        Assert.That(profile.WingCostMultiplier, Is.EqualTo(1.1875).Within(1e-10));
        Assert.That(profile.DesignLoadFactor, Is.EqualTo(2.5625));
    }

    [Test]
    public void AllThreeInputsInterpolateContinuously()
    {
        var p = new AirframeParameters { WingDesignSpeedKph = 275, WingPayloadAgility = -75, WingProfileEfficiency = 12.5 };
        var profile = WingProfileCalculator.Calculate(p);
        Assert.That(profile.MaximumLiftCoefficient, Is.EqualTo(1.725 * 1.185 * .925).Within(1e-10));
        Assert.That(profile.ProfileDragCoefficient, Is.EqualTo(.012 * 1.11 * 1.15).Within(1e-10));
        Assert.That(profile.DesignLoadFactor, Is.EqualTo(2.75 * .925).Within(1e-10));
    }

    [TestCase(100, 200, 1)]
    [TestCase(200, 200, 1)]
    [TestCase(220, 200, 1.04)]
    [TestCase(250, 200, 1.25)]
    [TestCase(300, 200, 2)]
    [TestCase(400, 200, 5)]
    [Category("Extended")]
    public void SpeedPenaltyMatchesTable(double speed, double optimum, double multiplier) =>
        Assert.That(WingProfileCalculator.CalculateSpeedDragMultiplier(speed, optimum), Is.EqualTo(multiplier).Within(1e-10));

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(99)]
    [TestCase(2001)]
    [Category("Extended")]
    public void InvalidSpeedRejected(double speed) => Assert.Throws<ArgumentOutOfRangeException>(() =>
        WingProfileCalculator.Calculate(new AirframeParameters { WingDesignSpeedKph = speed }));

    [Test]
    [Category("Extended")]
    public void InvalidOtherInputsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AirframeParameters { WingPayloadAgility = double.NaN }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new AirframeParameters { WingPayloadAgility = 101 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new AirframeParameters { WingProfileEfficiency = double.PositiveInfinity }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new AirframeParameters { WingProfileEfficiency = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => WingProfileCalculator.CalculateSpeedDragMultiplier(double.NaN, 350));
    }

    [Test]
    public void CloneRetainsSettings()
    {
        var original = new AirframeParameters { UseModularWingProfile = true, WingDesignSpeedKph = 800, WingPayloadAgility = -35, WingProfileEfficiency = 85 };
        var copy = original.Clone();
        Assert.That(copy.UseModularWingProfile, Is.True);
        Assert.That(WingProfileCalculator.Calculate(copy), Is.EqualTo(WingProfileCalculator.Calculate(original)));
    }

    [Test]
    [Category("Extended")]
    public void ModularModeIgnoresAllLegacyProfileBonuses()
    {
        var p = new AirframeParameters { UseModularWingProfile = true };
        var baseline = Calculate(p);
        foreach (WingProfileType legacy in Enum.GetValues(typeof(WingProfileType)))
        {
            p.WingProfile = legacy;
            var result = Calculate(p);
            Assert.That(result.TotalWeight, Is.EqualTo(baseline.TotalWeight));
            Assert.That(result.LiftCoefficient, Is.EqualTo(baseline.LiftCoefficient));
            Assert.That(result.DragCoefficient, Is.EqualTo(baseline.DragCoefficient));
            Assert.That(result.Maneuverability, Is.EqualTo(baseline.Maneuverability));
            Assert.That(result.ProductionCost, Is.EqualTo(baseline.ProductionCost));
            Assert.That(result.ProductionManDays, Is.EqualTo(baseline.ProductionManDays));
        }
    }

    [Test]
    public void TransportAndAerobaticSettingsHaveRealTradeoffs()
    {
        var p = new AirframeParameters { UseModularWingProfile = true, WingPayloadAgility = -100 };
        var cargo = Calculate(p);
        p.WingPayloadAgility = 100;
        var aerobatic = Calculate(p);
        Assert.That(cargo.LiftCoefficient, Is.GreaterThan(1.8));
        Assert.That(cargo.LiftCoefficient, Is.GreaterThan(aerobatic.LiftCoefficient));
        Assert.That(cargo.TotalWeight.Kilograms, Is.LessThan(aerobatic.TotalWeight.Kilograms));
        Assert.That(cargo.ProductionCost, Is.LessThan(aerobatic.ProductionCost));
        Assert.That(cargo.Maneuverability, Is.LessThan(aerobatic.Maneuverability));
    }

    [Test]
    public void CheaperEfficiencyHasNoWeightLiftOrAgilityAdvantage()
    {
        var p = new AirframeParameters { UseModularWingProfile = true, WingProfileEfficiency = 0 };
        var simple = Calculate(p);
        p.WingProfileEfficiency = 100;
        var precision = Calculate(p);
        Assert.That(precision.DragCoefficient, Is.EqualTo(simple.DragCoefficient));
        Assert.That(precision.WingProfileDragCoefficient / simple.WingProfileDragCoefficient, Is.EqualTo(.8 / 1.2).Within(1e-10));
        Assert.That(precision.LiftCoefficient, Is.GreaterThan(simple.LiftCoefficient));
        Assert.That(precision.TotalWeight.Kilograms, Is.LessThan(simple.TotalWeight.Kilograms));
        Assert.That(precision.Maneuverability, Is.GreaterThan(simple.Maneuverability));
        Assert.That(precision.ProductionCost / simple.ProductionCost, Is.InRange(1d, 1.60 / .75));
        Assert.That(precision.ProductionManDays / simple.ProductionManDays, Is.InRange(1d, 1.60 / .75));
        Assert.That(precision.CostBreakdown!.Total, Is.EqualTo(precision.ProductionCost).Within(1e-8));
        Assert.That(precision.ProductionManDaysBreakdown!.Total, Is.EqualTo(precision.ProductionManDays).Within(1e-8));
    }

    private static AirframePerformanceReport Calculate(AirframeParameters parameters) =>
        new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel()).Calculate(
            new AirframeDefinition { Id = "modular.test", DisplayName = "Modular test" }, parameters);
}
