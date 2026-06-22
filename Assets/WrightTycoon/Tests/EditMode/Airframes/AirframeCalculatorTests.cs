using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Physics;

namespace WrightTycoon.Core.Tests.Airframes;

[TestFixture]
public sealed class AirframeCalculatorTests
{
    [Test]
    public void LargerWingArea_IncreasesReferenceLiftAndDrag()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var compact = CreateParameters();
        compact.WingAreaSquareMeters = 28d;
        var large = compact.Clone();
        large.WingAreaSquareMeters = 42d;

        var compactReport = calculator.Calculate(definition, compact);
        var largeReport = calculator.Calculate(definition, large);

        Assert.That(largeReport.ReferenceLiftAtSeaLevel100Ms.Newtons, Is.GreaterThan(compactReport.ReferenceLiftAtSeaLevel100Ms.Newtons));
        Assert.That(largeReport.ReferenceDragAtSeaLevel100Ms.Newtons, Is.GreaterThan(compactReport.ReferenceDragAtSeaLevel100Ms.Newtons));
    }

    [Test]
    public void HigherWingAspectRatio_ReducesDragButIncreasesWeightAndLowersManeuverability()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var lowAspect = CreateParameters();
        lowAspect.WingAspectRatio = 4d;

        var reference = lowAspect.Clone();
        reference.WingAspectRatio = PlaneDesignerBalance.Airframe.WingAspectRatioDefault;

        var highAspect = lowAspect.Clone();
        highAspect.WingAspectRatio = 22d;

        var lowReport = calculator.Calculate(definition, lowAspect);
        var referenceReport = calculator.Calculate(definition, reference);
        var highReport = calculator.Calculate(definition, highAspect);

        Assert.That(referenceReport.DragCoefficient, Is.LessThan(lowReport.DragCoefficient));
        Assert.That(highReport.DragCoefficient, Is.LessThan(referenceReport.DragCoefficient));
        Assert.That(referenceReport.TotalWeight.Kilograms, Is.GreaterThan(lowReport.TotalWeight.Kilograms));
        Assert.That(highReport.TotalWeight.Kilograms, Is.GreaterThan(referenceReport.TotalWeight.Kilograms));
        Assert.That(referenceReport.Maneuverability, Is.LessThan(lowReport.Maneuverability));
        Assert.That(highReport.Maneuverability, Is.LessThan(referenceReport.Maneuverability));
    }

    [Test]
    public void RetractableGear_ReducesDragCoefficient()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();
        var fixedGear = CreateParameters();
        fixedGear.LandingGearType = LandingGearType.Fixed;
        var retractable = fixedGear.Clone();
        retractable.LandingGearType = LandingGearType.Retractable;

        var fixedReport = calculator.Calculate(definition, fixedGear);
        var retractableReport = calculator.Calculate(definition, retractable);

        Assert.That(retractableReport.DragCoefficient, Is.LessThan(fixedReport.DragCoefficient));
    }

    [Test]
    public void Biplane_IsLighterButHasHigherDragThanLowAndHighWing()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var lowWing = CreateParameters();
        lowWing.WingPosition = WingPosition.Low;

        var highWing = lowWing.Clone();
        highWing.WingPosition = WingPosition.High;

        var biplane = lowWing.Clone();
        biplane.WingPosition = WingPosition.Biplane;

        var lowWingReport = calculator.Calculate(definition, lowWing);
        var highWingReport = calculator.Calculate(definition, highWing);
        var biplaneReport = calculator.Calculate(definition, biplane);

        Assert.That(biplaneReport.TotalWeight.Kilograms, Is.LessThan(lowWingReport.TotalWeight.Kilograms));
        Assert.That(biplaneReport.TotalWeight.Kilograms, Is.LessThan(highWingReport.TotalWeight.Kilograms));
        Assert.That(biplaneReport.DragCoefficient, Is.GreaterThan(lowWingReport.DragCoefficient + 0.01d));
        Assert.That(biplaneReport.DragCoefficient, Is.GreaterThan(highWingReport.DragCoefficient));
        Assert.That(biplaneReport.LiftCoefficient, Is.GreaterThan(lowWingReport.LiftCoefficient + 0.12d));
        Assert.That(biplaneReport.LiftCoefficient, Is.GreaterThan(highWingReport.LiftCoefficient + 0.08d));
    }

    [Test]
    public void CompositeMaterial_ReducesWeightAndRaisesCost()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();
        var aluminum = CreateParameters();
        aluminum.MaterialType = MaterialType.Aluminum;
        var composite = aluminum.Clone();
        composite.MaterialType = MaterialType.Composite;

        var aluminumReport = calculator.Calculate(definition, aluminum);
        var compositeReport = calculator.Calculate(definition, composite);

        Assert.That(compositeReport.TotalWeight.Kilograms, Is.LessThan(aluminumReport.TotalWeight.Kilograms));
        Assert.That(compositeReport.ProductionCost, Is.GreaterThan(aluminumReport.ProductionCost));
    }

    [Test]
    public void ConstructionStandard_ModifiesWeightCostReliabilityManeuverabilityAndDrag()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var civil = CreateParameters();
        civil.ConstructionStandard = AircraftConstructionStandard.Civil;

        var ultralight = civil.Clone();
        ultralight.ConstructionStandard = AircraftConstructionStandard.Ultralight;

        var armyGrade = civil.Clone();
        armyGrade.ConstructionStandard = AircraftConstructionStandard.ArmyGrade;

        var civilReport = calculator.Calculate(definition, civil);
        var ultralightReport = calculator.Calculate(definition, ultralight);
        var armyGradeReport = calculator.Calculate(definition, armyGrade);

        Assert.That(ultralightReport.TotalWeight.Kilograms, Is.LessThan(civilReport.TotalWeight.Kilograms));
        Assert.That(ultralightReport.ProductionCost, Is.LessThan(civilReport.ProductionCost));
        Assert.That(ultralightReport.ProductionManDays, Is.LessThan(civilReport.ProductionManDays));
        Assert.That(ultralightReport.ReliabilityModifier, Is.LessThan(civilReport.ReliabilityModifier));
        Assert.That(ultralightReport.Maneuverability, Is.GreaterThan(civilReport.Maneuverability));
        Assert.That(ultralightReport.DragCoefficient, Is.GreaterThan(civilReport.DragCoefficient));

        Assert.That(armyGradeReport.TotalWeight.Kilograms, Is.GreaterThan(civilReport.TotalWeight.Kilograms));
        Assert.That(armyGradeReport.ProductionCost, Is.GreaterThan(civilReport.ProductionCost));
        Assert.That(armyGradeReport.ProductionManDays, Is.GreaterThan(civilReport.ProductionManDays));
        Assert.That(armyGradeReport.ReliabilityModifier, Is.GreaterThan(civilReport.ReliabilityModifier));
        Assert.That(armyGradeReport.Maneuverability, Is.LessThan(civilReport.Maneuverability));
        Assert.That(armyGradeReport.DragCoefficient, Is.GreaterThan(civilReport.DragCoefficient));
    }

    [Test]
    public void LeadingEdgeFlaps_IncreaseLiftAndProductionCost()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var baseline = CreateParameters();
        baseline.FlapType = FlapType.Plain;
        baseline.HasLeadingEdgeFlaps = false;

        var leadingEdge = baseline.Clone();
        leadingEdge.HasLeadingEdgeFlaps = true;

        var baselineReport = calculator.Calculate(definition, baseline);
        var leadingEdgeReport = calculator.Calculate(definition, leadingEdge);

        Assert.That(leadingEdgeReport.LiftCoefficient, Is.GreaterThan(baselineReport.LiftCoefficient));
        Assert.That(leadingEdgeReport.ProductionCost, Is.GreaterThan(baselineReport.ProductionCost));
    }

    [Test]
    public void UpdatedRangeLimits_AcceptUpperBounds()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();
        var parameters = CreateParameters();
        parameters.EngineCount = 6;
        parameters.CrewCount = 6;
        parameters.WingAreaSquareMeters = 1000d;
        parameters.FuelAmountLiters = 10000d;
        parameters.EngineCowling = 0.9d;

        var report = calculator.Calculate(definition, parameters);

        Assert.That(report.TotalWeight.Kilograms, Is.GreaterThan(0d));
    }

    [Test]
    public void ExpandedFuelAndCrewScale_AcceptsWideBodyStyleInputs()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();
        var parameters = CreateParameters();
        parameters.EngineCount = 4;
        parameters.CrewCount = 20;
        parameters.WingAreaSquareMeters = 520d;
        parameters.FuelAmountLiters = 180000d;
        parameters.EngineCowling = 0.85d;
        parameters.LandingGearType = LandingGearType.Retractable;

        var report = calculator.Calculate(definition, parameters);

        Assert.That(report.TotalWeight.Kilograms, Is.GreaterThan(150000d));
        Assert.That(report.DragCoefficient, Is.InRange(0.014d, 0.12d));
        Assert.That(report.LiftCoefficient, Is.InRange(0.5d, 1.8d));
    }

    [Test]
    public void WingAreaIncrease_HasStrongImpactOnTotalWeight()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var smallWing = CreateParameters();
        smallWing.WingAreaSquareMeters = 40d;
        smallWing.FuelAmountLiters = 2000d;

        var largeWing = smallWing.Clone();
        largeWing.WingAreaSquareMeters = 220d;

        var smallReport = calculator.Calculate(definition, smallWing);
        var largeReport = calculator.Calculate(definition, largeWing);

        Assert.That(largeReport.TotalWeight.Kilograms, Is.GreaterThan(smallReport.TotalWeight.Kilograms * 2.2d));
    }

    [Test]
    public void AerodynamicCleanliness_ReducesDragAndAppliesConfiguredProductionMultipliers()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var baseline = CreateParameters();
        baseline.AerodynamicCleanlinessPercent = 0d;

        var clean = baseline.Clone();
        clean.AerodynamicCleanlinessPercent = 20d;

        var baselineReport = calculator.Calculate(definition, baseline);
        var cleanReport = calculator.Calculate(definition, clean);

        Assert.That(cleanReport.DragCoefficient, Is.LessThan(baselineReport.DragCoefficient));
        Assert.That(cleanReport.ProductionCost / baselineReport.ProductionCost, Is.EqualTo(1.05d).Within(0.000001d));
        Assert.That(cleanReport.ProductionManDays / baselineReport.ProductionManDays, Is.EqualTo(1.20d).Within(0.000001d));
        Assert.That(cleanReport.CostBreakdown!.Total, Is.EqualTo(cleanReport.ProductionCost).Within(0.000001d));
        Assert.That(cleanReport.ProductionManDaysBreakdown!.Total, Is.EqualTo(cleanReport.ProductionManDays).Within(0.000001d));
        Assert.That(cleanReport.AircraftLengthMeters, Is.GreaterThan(0d));
        Assert.That(cleanReport.AirframeFrontalAreaSquareMeters, Is.GreaterThan(0d));
        Assert.That(cleanReport.EffectiveDragAreaSquareMeters, Is.GreaterThan(0d));
    }


    [Test]
    public void WoodAndAluminum_AreEqualWeightForVeryLightAircraft()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = new AirframeDefinition
        {
            Id = "air.light.compare",
            DisplayName = "Light Compare",
            BaseStructuralWeightKilograms = 140d,
            ReferenceWingAreaSquareMeters = 8d,
            BaseDragCoefficient = 0.035d,
            BaseLiftCoefficient = 1.0d,
            BaseReliabilityModifier = 1.0d,
            BaseCost = 12000d,
            BaseProductionManDays = 30d,
        };

        var aluminum = new AirframeParameters
        {
            EngineCount = 1,
            StructureType = StructureType.Monocoque,
            MaterialType = MaterialType.Aluminum,
            WingPosition = WingPosition.High,
            WingShape = WingShape.Straight,
            WingAreaSquareMeters = 8d,
            TailType = TailType.Classic,
            FlapType = FlapType.Plain,
            LandingGearType = LandingGearType.Fixed,
            EngineCowling = 0.4d,
            FuelAmountLiters = 50d,
            CrewCount = 1,
            AerodynamicCleanlinessPercent = 1d,
            RunwayType = RunwayType.Short,
        };

        var wood = aluminum.Clone();
        wood.MaterialType = MaterialType.WoodFabric;

        var aluminumReport = calculator.Calculate(definition, aluminum);
        var woodReport = calculator.Calculate(definition, wood);

        Assert.That(aluminumReport.TotalWeight.Kilograms, Is.LessThan(800d));
        Assert.That(woodReport.TotalWeight.Kilograms, Is.LessThan(800d));
        Assert.That(woodReport.TotalWeight.Kilograms, Is.EqualTo(aluminumReport.TotalWeight.Kilograms).Within(1.0d));
    }

    [Test]
    public void HeavyWoodAirframe_IsSignificantlyHeavierThanAluminum()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var aluminum = CreateParameters();
        aluminum.EngineCount = 4;
        aluminum.WingAreaSquareMeters = 320d;
        aluminum.FuelAmountLiters = 22000d;
        aluminum.CrewCount = 10;
        aluminum.LandingGearType = LandingGearType.Retractable;
        aluminum.MaterialType = MaterialType.Aluminum;

        var wood = aluminum.Clone();
        wood.MaterialType = MaterialType.WoodFabric;

        var aluminumReport = calculator.Calculate(definition, aluminum);
        var woodReport = calculator.Calculate(definition, wood);

        Assert.That(woodReport.TotalWeight.Kilograms, Is.GreaterThan(aluminumReport.TotalWeight.Kilograms * 1.12d));
    }

    [Test]
    public void BackupSystems_IncreaseReliabilityWeightAndDrag()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var baseline = CreateParameters();
        baseline.MaterialType = MaterialType.Aluminum;
        baseline.HasBackupSystems = false;

        var withBackup = baseline.Clone();
        withBackup.HasBackupSystems = true;

        var baselineReport = calculator.Calculate(definition, baseline);
        var withBackupReport = calculator.Calculate(definition, withBackup);

        Assert.That(withBackupReport.ReliabilityModifier, Is.GreaterThanOrEqualTo(baselineReport.ReliabilityModifier + 0.15d));
        Assert.That(withBackupReport.TotalWeight.Kilograms, Is.GreaterThanOrEqualTo(baselineReport.TotalWeight.Kilograms * PlaneDesignerBalance.Airframe.BackupSystemsWeightMultiplier));
        Assert.That(withBackupReport.DragCoefficient, Is.GreaterThan(baselineReport.DragCoefficient * 1.02d));
    }

    [Test]
    public void DeltaWing_ReducesLiftCoefficientByConfiguredMultiplierAgainstStraightWing()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var straight = CreateParameters();
        straight.WingShape = WingShape.Straight;

        var delta = straight.Clone();
        delta.WingShape = WingShape.Delta;

        var straightReport = calculator.Calculate(definition, straight);
        var deltaReport = calculator.Calculate(definition, delta);

        Assert.That(straightReport.LiftCoefficient, Is.GreaterThan(0d));
        Assert.That(deltaReport.LiftCoefficient, Is.EqualTo(straightReport.LiftCoefficient * PlaneDesignerBalance.Airframe.DeltaWingLiftMultiplier).Within(1e-6));
    }

    [Test]
    public void LaminarProfile_EffectFadesAsWingAreaGrowsFrom50To500()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var smallWingStandard = CreateParameters();
        smallWingStandard.WingAreaSquareMeters = 50d;
        smallWingStandard.WingProfile = WingProfileType.Standard;

        var smallWingLaminar = smallWingStandard.Clone();
        smallWingLaminar.WingProfile = WingProfileType.Laminar6SeriesAerodynamic;

        var largeWingStandard = CreateParameters();
        largeWingStandard.WingAreaSquareMeters = 500d;
        largeWingStandard.WingProfile = WingProfileType.Standard;

        var largeWingLaminar = largeWingStandard.Clone();
        largeWingLaminar.WingProfile = WingProfileType.Laminar6SeriesAerodynamic;

        var smallStandardReport = calculator.Calculate(definition, smallWingStandard);
        var smallLaminarReport = calculator.Calculate(definition, smallWingLaminar);
        var largeStandardReport = calculator.Calculate(definition, largeWingStandard);
        var largeLaminarReport = calculator.Calculate(definition, largeWingLaminar);

        var smallLiftGainRatio = (smallLaminarReport.LiftCoefficient / smallStandardReport.LiftCoefficient) - 1d;
        var largeLiftGainRatio = (largeLaminarReport.LiftCoefficient / largeStandardReport.LiftCoefficient) - 1d;
        var smallDragReductionRatio = 1d - (smallLaminarReport.DragCoefficient / smallStandardReport.DragCoefficient);
        var largeDragReductionRatio = 1d - (largeLaminarReport.DragCoefficient / largeStandardReport.DragCoefficient);

        Assert.That(smallLiftGainRatio, Is.GreaterThan(0.03d));
        Assert.That(smallDragReductionRatio, Is.GreaterThan(0.03d));
        Assert.That(smallLiftGainRatio, Is.GreaterThan(largeLiftGainRatio + 0.02d));
        Assert.That(smallDragReductionRatio, Is.GreaterThan(largeDragReductionRatio + 0.02d));
    }

    [Test]
    public void Naca4DigitLightProfile_ReducesAirframeWeightAndCost()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var standard = CreateParameters();
        standard.WingProfile = WingProfileType.Standard;

        var lightNaca = standard.Clone();
        lightNaca.WingProfile = WingProfileType.Naca4DigitLight;

        var standardReport = calculator.Calculate(definition, standard);
        var nacaReport = calculator.Calculate(definition, lightNaca);

        Assert.That(nacaReport.TotalWeight.Kilograms, Is.LessThan(standardReport.TotalWeight.Kilograms * 0.92d));
        Assert.That(nacaReport.ProductionCost, Is.LessThan(standardReport.ProductionCost * 0.92d));
    }

    [Test]
    public void SymmetricalAcrobaticProfile_IncreasesManeuverabilityAndCostButLowersLift()
    {
        var calculator = new AirframeCalculator(new AerodynamicsCalculator(), new IsaAtmosphereModel());
        var definition = CreateDefinition();

        var standard = CreateParameters();
        standard.WingProfile = WingProfileType.Standard;

        var acrobatic = standard.Clone();
        acrobatic.WingProfile = WingProfileType.SymmetricalAcrobatic;

        var standardReport = calculator.Calculate(definition, standard);
        var acrobaticReport = calculator.Calculate(definition, acrobatic);

        Assert.That(acrobaticReport.Maneuverability, Is.GreaterThan(standardReport.Maneuverability * 1.08d));
        Assert.That(acrobaticReport.ProductionCost, Is.GreaterThan(standardReport.ProductionCost * 1.18d));
        Assert.That(acrobaticReport.LiftCoefficient, Is.LessThan(standardReport.LiftCoefficient * 0.92d));
    }

    private static AirframeDefinition CreateDefinition() =>
        new()
        {
            Id = "air.medium.utility",
            DisplayName = "Utility Medium Frame",
            BaseStructuralWeightKilograms = 1150d,
            ReferenceWingAreaSquareMeters = 34d,
            BaseDragCoefficient = 0.031d,
            BaseLiftCoefficient = 1.05d,
            BaseReliabilityModifier = 1.0d,
            BaseCost = 62000d,
            BaseProductionManDays = 135d,
        };

    private static AirframeParameters CreateParameters() =>
        new()
        {
            EngineCount = 2,
            StructureType = StructureType.Monocoque,
            MaterialType = MaterialType.Aluminum,
            WingPosition = WingPosition.Low,
            WingShape = WingShape.Straight,
            WingAreaSquareMeters = 34d,
            TailType = TailType.Classic,
            FlapType = FlapType.Fowler,
            LandingGearType = LandingGearType.SemiRetractable,
            EngineCowling = 0.5d,
            FuelAmountLiters = 1400d,
            CrewCount = 2,
        };
}

