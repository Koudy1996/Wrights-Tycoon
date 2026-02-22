using NUnit.Framework;
using WrightTycoon.Core.Airframes;
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
        Assert.That(biplaneReport.DragCoefficient, Is.GreaterThan(highWingReport.DragCoefficient + 0.01d));
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
    public void AerodynamicCleanliness_ReducesDragButRaisesProductionManDays()
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
        Assert.That(cleanReport.ProductionManDays, Is.GreaterThan(baselineReport.ProductionManDays));
        Assert.That(cleanReport.AircraftLengthMeters, Is.GreaterThan(0d));
        Assert.That(cleanReport.AirframeFrontalAreaSquareMeters, Is.GreaterThan(0d));
        Assert.That(cleanReport.EffectiveDragAreaSquareMeters, Is.GreaterThan(0d));
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
