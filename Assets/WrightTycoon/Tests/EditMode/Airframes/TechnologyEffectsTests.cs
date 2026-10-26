using System;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Physics;

namespace WrightTycoon.Core.Tests.Airframes;

[TestFixture]
public sealed class TechnologyEffectsTests
{
    private static AirframeTechnologySnapshot Snapshot(double structural, double aero) => new(1946, 2, structural, aero);
    private static AirframeCalculator Calculator() => new(new AerodynamicsCalculator(), new IsaAtmosphereModel());
    private static AirframeDefinition Definition() => new()
    {
        Id = "technology-test", DisplayName = "Technology test", BaseStructuralWeightKilograms = 1200,
        ReferenceWingAreaSquareMeters = 32, BaseDragCoefficient = .05, BaseLiftCoefficient = 1.1,
    };
    private static AirframeParameters Parameters() => new()
    {
        MaterialType = MaterialType.Aluminum, StructureType = StructureType.Monocoque,
        WingAreaSquareMeters = 32, FuelAmountLiters = 400, CrewCount = 2,
        EngineCount = 1, UseModularWingProfile = true,
    };

    [Test]
    public void AcceptedWeightsAndWorkedExampleAreExact()
    {
        var effects = AirframeTechnologyEffects.Resolve(Snapshot(1.1, 1.1), Snapshot(1.3, 1.3));
        var actual = new[] { effects.FuselageMass, effects.WingMass, effects.SystemsMass,
            effects.BodyDrag, effects.ProfileDrag, effects.Lift, effects.Materials, effects.Work };
        var shares = new[] { AirframeTechnologyBalance.FuselageMassFamilyShare, AirframeTechnologyBalance.WingMassFamilyShare,
            AirframeTechnologyBalance.SystemsMassFamilyShare, AirframeTechnologyBalance.BodyDragFamilyShare,
            AirframeTechnologyBalance.ProfileDragFamilyShare, AirframeTechnologyBalance.LiftFamilyShare,
            AirframeTechnologyBalance.MaterialsFamilyShare, AirframeTechnologyBalance.WorkFamilyShare };
        Assert.That(shares, Is.EqualTo(new[] { .7, .6, .2, .5, .75, .6, .6, .5 }));
        Assert.That(actual, Is.EqualTo(new[] { 1.16, 1.18, 1.26, 1.20, 1.15, 1.18, 1.18, 1.20 }).Within(1e-12));
        foreach (var share in shares) Assert.That(share, Is.InRange(0d, 1d));
    }

    [TestCase(.5)]
    [TestCase(1)]
    [TestCase(1.4)]
    public void IdenticalSnapshotsApplyIndexOnceIncludingBelowNeutral(double index)
    {
        var effects = AirframeTechnologyEffects.Resolve(Snapshot(index, index), Snapshot(index, index));
        Assert.That(effects.FuselageMass, Is.EqualTo(index).Within(1e-12));
        Assert.That(effects.WingMass, Is.EqualTo(index).Within(1e-12));
        Assert.That(effects.SystemsMass, Is.EqualTo(index).Within(1e-12));
        Assert.That(effects.BodyDrag, Is.EqualTo(index).Within(1e-12));
        Assert.That(effects.ProfileDrag, Is.EqualTo(index).Within(1e-12));
        Assert.That(effects.Lift, Is.EqualTo(index).Within(1e-12));
        Assert.That(effects.Materials, Is.EqualTo(index).Within(1e-12));
        Assert.That(effects.Work, Is.EqualTo(index).Within(1e-12));
    }

    [Test]
    [Category("Extended")]
    public void SnapshotRejectsInvalidOrUnboundedInputInsteadOfChangingFrozenValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AirframeTechnologySnapshot(1946, 13, 1, 1).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => Snapshot(double.NaN, 1).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => Snapshot(1, double.PositiveInfinity).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => Snapshot(0, 1).Validate());
        Assert.Throws<ArgumentNullException>(() => AirframeTechnologyEffects.Resolve(null!, Snapshot(1, 1)));
        Assert.Throws<InvalidOperationException>(() => new AircraftTypeDefinition
            { Id = "type", FamilyId = "family", DisplayName = "Type", TechnologyModelVersion = 2 }.Validate());
        Assert.That(new AircraftTypeDefinition().TechnologyModelVersion, Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [Category("Extended")]
    public void DirectCalculatorCannotUseInvalidTechnologyEffects(double invalid)
    {
        var effects = new AirframeTechnologyEffects(invalid, 1, 1, 1, 1, 1, 1, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculator().Calculate(Definition(), Parameters(), null, effects));
    }

    [Test]
    public void StructuralTechnologyNeverReducesPhysicalFuelOrCrewMass()
    {
        var p = Parameters();
        var baseline = Calculator().Calculate(Definition(), p, null,
            AirframeTechnologyEffects.Resolve(Snapshot(1, 1), Snapshot(1, 1)));
        var improved = Calculator().Calculate(Definition(), p, null,
            AirframeTechnologyEffects.Resolve(Snapshot(2, 1), Snapshot(2, 1)));
        var physical = p.FuelAmountLiters * PlaneDesignerBalance.Performance.FuelDensityKilogramsPerLiter + p.CrewCount * 88d;
        Assert.That(improved.TotalWeight.Kilograms,
            Is.EqualTo((baseline.TotalWeight.Kilograms - physical) / 2d + physical).Within(1e-9));
    }

    [Test]
    public void AeroAndProductionChannelsUseDifferentIndicesAndBreakdownsStillSum()
    {
        var p = Parameters();
        var calculator = Calculator();
        var neutral = AirframeTechnologyEffects.Resolve(Snapshot(1, 1), Snapshot(1, 1));
        var basis = calculator.Calculate(Definition(), p, null, neutral);
        var effects = AirframeTechnologyEffects.Resolve(Snapshot(1.1, 1.1), Snapshot(1.3, 1.3));
        var report = calculator.Calculate(Definition(), p, null, effects);
        Assert.That(report.DragCoefficient, Is.EqualTo(basis.DragCoefficient / 1.2).Within(1e-12));
        Assert.That(report.WingProfileDragCoefficient, Is.EqualTo(basis.WingProfileDragCoefficient / 1.15).Within(1e-12));
        Assert.That(report.LiftCoefficient, Is.EqualTo(basis.LiftCoefficient * 1.18).Within(1e-12));
        Assert.That(report.ProductionCost, Is.EqualTo(basis.ProductionCost / 1.18).Within(1e-9));
        Assert.That(report.ProductionManDays, Is.EqualTo(basis.ProductionManDays / 1.2).Within(1e-9));
        Assert.That(report.CostBreakdown!.Items.Sum(x => x.Amount), Is.EqualTo(report.ProductionCost).Within(1e-9));
        Assert.That(report.ProductionManDaysBreakdown!.Items.Sum(x => x.Amount), Is.EqualTo(report.ProductionManDays).Within(1e-9));
        Assert.That(report.ReliabilityModifier, Is.EqualTo(basis.ReliabilityModifier));
    }

    [Test]
    public void ExplicitEffectsIgnoreChangingCurrentCalendarTechnology()
    {
        var effects = AirframeTechnologyEffects.Resolve(Snapshot(1.1, 1.1), Snapshot(1.3, 1.3));
        var calculator = Calculator();
        var p = Parameters();
        var early = calculator.Calculate(Definition(), p, HistoricalCampaignTechnologyModifiers.Neutral, effects);
        var late = calculator.Calculate(Definition(), p, new HistoricalCampaignTechnologyModifiers(2, 2, 2, 2), effects);
        Assert.That(late.TotalWeight, Is.EqualTo(early.TotalWeight));
        Assert.That(late.DragCoefficient, Is.EqualTo(early.DragCoefficient));
        Assert.That(late.ProductionCost, Is.EqualTo(early.ProductionCost));
    }
}
