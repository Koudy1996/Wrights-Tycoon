using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Physics;

namespace WrightTycoon.Core.Tests.Aircraft;

public sealed class LandingGearTests
{
    [Test]
    public void AcceptedSizingTableAppliesTechnologyPrecisionAndDurabilityOnce()
    {
        var fixedGear = Gear(LandingGearType.Fixed);
        var semi = Gear(LandingGearType.SemiRetractable);
        var retract = Gear(LandingGearType.Retractable);
        Assert.That(fixedGear.ReferenceMassKilograms, Is.EqualTo(6000d));
        Assert.That(fixedGear.MassKilograms, Is.EqualTo(192d / .78d).Within(1e-10));
        Assert.That(semi.MassKilograms, Is.EqualTo(216d / .78d).Within(1e-10));
        Assert.That(retract.MassKilograms, Is.EqualTo(252d / .78d).Within(1e-10));
        Assert.That(retract.ExtraMassOverFixedKilograms, Is.EqualTo(60d / .78d).Within(1e-10));
        Assert.That(retract.CruiseDragAreaSquareMeters, Is.EqualTo(.004d * System.Math.Pow(6d, 2d / 3d) / .78d).Within(1e-12));
        Assert.That(retract.ExtendedDragAreaSquareMeters, Is.EqualTo(fixedGear.CruiseDragAreaSquareMeters));
        var modified = LandingGearCalculator.Calculate(3750, LandingGearType.Retractable, .78, .78, .9, .8, 1.2);
        Assert.That(modified.MassKilograms, Is.EqualTo(retract.MassKilograms * .9 * 1.2).Within(1e-10));
        Assert.That(modified.CruiseDragAreaSquareMeters, Is.EqualTo(retract.CruiseDragAreaSquareMeters * .8).Within(1e-12));
    }

    [Test]
    public void AircraftAddsPhysicalGearOnceAndKeepsItIndependentOfRunwayFuelAndCrew()
    {
        var p = Parameters();
        var baseline = Build(p);
        p.LandingGearType = LandingGearType.Retractable;
        var retract = Build(p);
        Assert.That(retract.LandingGear!.MassKilograms, Is.GreaterThan(baseline.LandingGear!.MassKilograms));
        Assert.That(retract.MaxSpeedByAltitude.Max(x => x.MaxSpeedMetersPerSecond),
            Is.GreaterThan(baseline.MaxSpeedByAltitude.Max(x => x.MaxSpeedMetersPerSecond)));
        Assert.That(retract.MaximumTakeoffWeightKilograms, Is.EqualTo(baseline.MaximumTakeoffWeightKilograms).Within(1e-8));
        var gearBefore = retract.LandingGear;
        p.RunwayType = RunwayType.Long; p.FuelAmountLiters = 900; p.CrewCount = 3;
        Assert.That(Build(p).LandingGear, Is.EqualTo(gearBefore));
        p.PrecisionEngineeringLevel = 8;
        var precision = Build(p);
        Assert.That(precision.LandingGear!.ReferenceMassKilograms, Is.EqualTo(gearBefore!.ReferenceMassKilograms));
        Assert.That(precision.LandingGear.MassKilograms,
            Is.EqualTo(gearBefore.MassKilograms * PrecisionEngineeringBalance.GetEffects(8).WeightMultiplier).Within(1e-8));
        var speedFactor = (double)typeof(AircraftPerformanceCalculator).GetMethod("CalculateSpeedWeightFactor",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null,
            new object[] { retract.MaxSpeedByAltitude.Max(x => x.MaxSpeedMetersPerSecond) * 3.6d })!;
        var fixedSpeedFactor = (double)typeof(AircraftPerformanceCalculator).GetMethod("CalculateSpeedWeightFactor",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null,
            new object[] { baseline.MaxSpeedByAltitude.Max(x => x.MaxSpeedMetersPerSecond) * 3.6d })!;
        Assert.That((retract.EmptyAircraftWeightKilograms - gearBefore.MassKilograms) / speedFactor,
            Is.EqualTo((baseline.EmptyAircraftWeightKilograms - baseline.LandingGear.MassKilograms) / fixedSpeedFactor).Within(1e-8));
    }

    [Test, Category("Extended")]
    public void GearReferenceDoesNotMoveWithCalendarTechnologyAndJsonPreservesBreakdown()
    {
        var p = Parameters();
        var oldReport = Build(p, .78);
        var newReport = Build(p, 1.2);
        Assert.That(oldReport.LandingGear!.ReferenceMassKilograms, Is.EqualTo(newReport.LandingGear!.ReferenceMassKilograms));
        Assert.That(newReport.LandingGear.MassKilograms, Is.EqualTo(oldReport.LandingGear.MassKilograms * .78 / 1.2).Within(1e-8));
        Assert.That(JsonConvert.DeserializeObject<AircraftPerformanceReport>(JsonConvert.SerializeObject(oldReport))!.LandingGear,
            Is.EqualTo(oldReport.LandingGear));
    }

    [Test, Category("Extended")]
    public void GearValidationRejectsUnknownTypesAndInvalidOrOverflowingInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Gear((LandingGearType)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => LandingGearCalculator.Calculate(double.NaN, LandingGearType.Fixed, 1, 1, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LandingGearCalculator.Calculate(1000, LandingGearType.Fixed, 0, 1, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LandingGearCalculator.Calculate(double.MaxValue, LandingGearType.Fixed, 1, 1, 1, 1, 1));
    }

    private static LandingGearReport Gear(LandingGearType type) => LandingGearCalculator.Calculate(3750, type, .78, .78, 1, 1, 1);
    private static AirframeParameters Parameters() => new()
    {
        EngineCount = 1, WingAreaSquareMeters = 32.4, FuelAmountLiters = 200,
        MaterialType = MaterialType.Aluminum, StructureType = StructureType.Monocoque,
        UseModularWingProfile = true, WingDesignSpeedKph = 250, WingPayloadAgility = -100,
        WingProfileEfficiency = 45, LandingGearType = LandingGearType.Fixed,
    };
    private static AircraftPerformanceReport Build(AirframeParameters p, double technology = .78)
    {
        var atmosphere = new IsaAtmosphereModel();
        var aero = new AerodynamicsCalculator();
        var engine = new EngineBuilder(new EngineCalculator(), atmosphere).Build(
            new EngineDefinition { Id = "gear-engine", DisplayName = "Gear engine", EngineType = EngineType.V },
            new EngineParameters { DisplacementLiters = 16, CylinderCount = 8 });
        var effects = AirframeTechnologyEffects.Resolve(new AirframeTechnologySnapshot(1946, 1, technology, technology),
            new AirframeTechnologySnapshot(1946, 1, technology, technology));
        var airframe = new AirframeBuilder(new AirframeCalculator(aero, atmosphere)).Build(
            new AirframeDefinition { Id = "gear-frame", DisplayName = "Gear frame", BaseStructuralWeightKilograms = 320,
                ReferenceWingAreaSquareMeters = 12 }, p, effects);
        return new AircraftPerformanceCalculator(atmosphere, aero).Calculate(engine, airframe,
            AircraftCategory.LightSportAndRecreational, AircraftNavigationEquipment.None,
            AircraftCategoryRoleRequirement.Unrestricted, 0, 1000, effects);
    }
}
