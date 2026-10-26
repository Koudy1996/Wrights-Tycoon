using System.Reflection;
using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Data;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class ModularWingPerformanceTests
{
    [Test]
    [Category("Extended")]
    public void RealAircraft_ProfileGrid_ProducesFiniteConsistentReports(
        [Values(100d, 350d, 750d, 1000d, 1400d, 2000d)] double designSpeed,
        [Values(-100d, 0d, 100d)] double orientation,
        [Values(0d, 50d, 100d)] double efficiency)
    {
        var aircraft = BuildAircraft(designSpeed, orientation, efficiency);
        var report = aircraft.Performance;
        AssertFiniteNonnegative(aircraft.Airframe.Performance.LiftCoefficient, "lift");
        AssertFiniteNonnegative(report.TotalProductionCost, "parts");
        AssertFiniteNonnegative(report.TotalProductionManDays, "work");
        AssertFiniteNonnegative(report.EmptyAircraftWeightKilograms, "empty mass");
        AssertFiniteNonnegative(report.PayloadCapacity.Kilograms, "payload");
        AssertFiniteNonnegative(report.RangeKilometers, "range");
        AssertFiniteNonnegative(report.AverageCruiseFuelBurnKilogramsPerHour, "fuel");
        foreach (var point in report.MaxSpeedByAltitude)
            AssertFiniteNonnegative(point.MaxSpeedMetersPerSecond, "speed");
        Assert.That(report.CostBreakdown!.Total, Is.EqualTo(report.TotalProductionCost).Within(1e-6d));
        Assert.That(report.ProductionManDaysBreakdown!.Total, Is.EqualTo(report.TotalProductionManDays).Within(1e-6d));
        Assert.That(report.TotalProductionCost, Is.EqualTo(report.AirframeProductionCost + report.EnginesProductionCost).Within(1e-6d));
        Assert.That(report.TotalProductionManDays, Is.EqualTo(report.AirframeProductionManDays + report.EnginesProductionManDays).Within(1e-6d));
    }

    [Test]
    public void RealAircraft_PlayerPriorities_HaveMeaningfulTradeoffs()
    {
        var neutral = BuildAircraft(350d, 0d, 50d);
        var cargo = BuildAircraft(350d, -100d, 50d);
        var agile = BuildAircraft(350d, 100d, 50d);
        var economical = BuildAircraft(350d, 0d, 0d);
        var efficient = BuildAircraft(350d, 0d, 100d);
        Assert.That(neutral.Performance.PayloadCapacity.Kilograms, Is.GreaterThan(0d));
        Assert.That(cargo.Performance.PayloadCapacity.Kilograms, Is.GreaterThan(neutral.Performance.PayloadCapacity.Kilograms));
        Assert.That(cargo.Performance.MaximumTakeoffWeightKilograms, Is.GreaterThan(neutral.Performance.MaximumTakeoffWeightKilograms));
        Assert.That(agile.Airframe.Performance.Maneuverability, Is.GreaterThan(neutral.Airframe.Performance.Maneuverability));
        Assert.That(efficient.Airframe.Performance.WingProfileDragCoefficient, Is.LessThan(economical.Airframe.Performance.WingProfileDragCoefficient));
        Assert.That(efficient.Airframe.Performance.TotalWeight.Kilograms, Is.LessThan(economical.Airframe.Performance.TotalWeight.Kilograms));
        Assert.That(efficient.Airframe.Performance.LiftCoefficient, Is.GreaterThan(economical.Airframe.Performance.LiftCoefficient));
        Assert.That(efficient.Performance.EmptyAircraftWeightKilograms, Is.LessThan(economical.Performance.EmptyAircraftWeightKilograms));
        Assert.That(efficient.Performance.PayloadCapacity.Kilograms, Is.GreaterThan(economical.Performance.PayloadCapacity.Kilograms));
        Assert.That(efficient.Performance.AirframeProductionCost, Is.GreaterThan(economical.Performance.AirframeProductionCost));
        Assert.That(efficient.Performance.AirframeProductionManDays, Is.GreaterThan(economical.Performance.AirframeProductionManDays));
        Assert.That(efficient.Engine.Performance.Cost, Is.EqualTo(economical.Engine.Performance.Cost));
    }

    [TestCase(100d, 100d)]
    [TestCase(350d, 600d)]
    [TestCase(2000d, 2600d)]
    public void SpeedSolver_BalancesActualSpeedDependentDrag(double designSpeedKph, double initialSpeedKph)
    {
        const double density = 1.225d;
        const double area = 20d;
        const double bodyCd = .004d;
        const double wingCd = .012d;
        var initialSpeed = initialSpeedKph / 3.6d;
        var thrust = .5d * density * area * initialSpeed * initialSpeed * (bodyCd + wingCd);
        var speed = Invoke("CalculateModularMaximumSpeedMetersPerSecond", thrust, density, area,
            bodyCd, wingCd, false, designSpeedKph);
        var actualCd = Invoke("CalculateModularDragCoefficient", bodyCd, wingCd, false, designSpeedKph, speed);
        Assert.That(.5d * density * area * speed * speed * actualCd,
            Is.EqualTo(thrust).Within(thrust * 1e-10d));
        Assert.That(speed, Is.LessThanOrEqualTo(initialSpeed + 1e-9d));
    }

    [Test]
    public void ProfileEfficiencyAndOverspeed_NeverDiscountBodyDrag()
    {
        var basic = Invoke("CalculateModularDragCoefficient", .03d, .01d, false, 350d, 350d / 3.6d);
        var efficient = Invoke("CalculateModularDragCoefficient", .03d, .008d, false, 350d, 350d / 3.6d);
        var overspeed = Invoke("CalculateModularDragCoefficient", .03d, .01d, false, 350d, 700d / 3.6d);
        Assert.That(basic, Is.EqualTo(.04d).Within(1e-12d));
        Assert.That(efficient, Is.EqualTo(.038d).Within(1e-12d));
        Assert.That(overspeed, Is.EqualTo(.08d).Within(1e-12d));
    }

    [Test]
    [Category("Extended")]
    public void ModularLift_DoesNotSaturateAtLegacyTakeoffLimit()
    {
        var atmosphere = new IsaAtmosphereModel();
        var aerodynamics = new AerodynamicsCalculator();
        var engine = new EngineBuilder(new EngineCalculator(), atmosphere).Build(
            new EngineDefinition { Id = "eng.modular.test", DisplayName = "Modular test" },
            new EngineParameters());
        var parameters = new AirframeParameters { UseModularWingProfile = true, WingAreaSquareMeters = 25d };
        var builder = new AirframeBuilder(new AirframeCalculator(aerodynamics, atmosphere));
        var airframe = builder.Build(new AirframeDefinition { Id = "air.modular.test", DisplayName = "Modular test" }, parameters);
        var calculator = new AircraftPerformanceCalculator(atmosphere, aerodynamics);
        var lower = calculator.Calculate(engine, new Airframe(airframe.Definition, parameters,
            airframe.Performance with { LiftCoefficient = 2.5d }));
        var higher = calculator.Calculate(engine, new Airframe(airframe.Definition, parameters,
            airframe.Performance with { LiftCoefficient = 3d }));
        Assert.That(higher.MaximumTakeoffWeightKilograms, Is.GreaterThan(lower.MaximumTakeoffWeightKilograms));
        Assert.That(higher.MaximumTakeoffWeightKilograms / lower.MaximumTakeoffWeightKilograms,
            Is.EqualTo(System.Math.Sqrt(3d / 2.5d)).Within(1e-9d));
        parameters.UseModularWingProfile = false;
        var legacyLower = calculator.Calculate(engine, new Airframe(airframe.Definition, parameters,
            airframe.Performance with { LiftCoefficient = 2.5d }));
        var legacyHigher = calculator.Calculate(engine, new Airframe(airframe.Definition, parameters,
            airframe.Performance with { LiftCoefficient = 3d }));
        Assert.That(legacyHigher.MaximumTakeoffWeightKilograms, Is.EqualTo(legacyLower.MaximumTakeoffWeightKilograms));
    }

    [Test]
    public void CruiseFuel_UsesSameDragAreaAndOverspeedCurveAsMaximumSpeed()
    {
        var atmosphere = new IsaAtmosphereModel();
        var aerodynamics = new AerodynamicsCalculator();
        var engine = new EngineBuilder(new EngineCalculator(), atmosphere).Build(
            new EngineDefinition { Id = "eng.cruise.test", DisplayName = "Cruise test" }, new EngineParameters());
        var parameters = new AirframeParameters { UseModularWingProfile = true, WingDesignSpeedKph = 100d };
        var airframe = new AirframeBuilder(new AirframeCalculator(aerodynamics, atmosphere)).Build(
            new AirframeDefinition { Id = "air.cruise.test", DisplayName = "Cruise test" }, parameters);
        var report = new AircraftPerformanceCalculator(atmosphere, aerodynamics).Calculate(engine, airframe);
        var density = atmosphere.GetDensityAtAltitude(report.OptimalCruiseAltitudeMeters).KilogramsPerCubicMeter;
        var requiredThrust = .5d * density * report.OptimalCruiseSpeedMetersPerSecond
            * report.OptimalCruiseSpeedMetersPerSecond * report.TotalDragAreaSquareMeters;
        var availableThrust = engine.Performance.GetThrustNewtonsAtAltitude(report.OptimalCruiseAltitudeMeters) * parameters.EngineCount;
        var throttle = System.Math.Min(1d, requiredThrust / availableThrust);
        var fullFuel = engine.Performance.GetPerformanceAtAltitude(report.OptimalCruiseAltitudeMeters).FuelConsumptionKilogramsPerHour
            * parameters.EngineCount * WrightTycoon.Core.Categories.AircraftCategoryCatalog.GetProfile(
                WrightTycoon.Core.Categories.AircraftCategory.LightSportAndRecreational).FuelConsumptionMultiplier;
        Assert.That(report.AverageCruiseFuelBurnKilogramsPerHour,
            Is.EqualTo(System.Math.Max(WrightTycoon.Core.Balance.PlaneDesignerBalance.Performance.CruiseMinimumFuelBurnKilogramsPerHour,
                fullFuel * throttle)).Within(1e-8d));
    }

    private static double Invoke(string method, params object[] args) => (double)typeof(AircraftPerformanceCalculator)
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args)!;

    private static void AssertFiniteNonnegative(double value, string label)
    {
        Assert.That(double.IsNaN(value) || double.IsInfinity(value), Is.False, label);
        Assert.That(value, Is.GreaterThanOrEqualTo(0d), label);
    }

    private static WrightTycoon.Core.Aircraft.Aircraft BuildAircraft(double designSpeed, double orientation, double efficiency)
    {
        var atmosphere = new IsaAtmosphereModel();
        var aerodynamics = new AerodynamicsCalculator();
        var engineDefinition = new EngineDefinition
        {
            Id = "eng.modular.grid", DisplayName = "Grid turbine", EngineType = EngineType.Turbojet,
            BaseJetThrustPerSquareMeterNewtons = 100000d,
        };
        var airframeDefinition = new AirframeDefinition
        {
            Id = "air.modular.grid", DisplayName = "Grid structure",
            BaseStructuralWeightKilograms = 2200d, ReferenceWingAreaSquareMeters = 40d,
        };
        var factory = new AircraftFactory(
            new EngineBuilder(new EngineCalculator(), atmosphere),
            new AirframeBuilder(new AirframeCalculator(aerodynamics, atmosphere)),
            new AircraftPerformanceCalculator(atmosphere, aerodynamics), new DevelopmentCostCalculator(),
            new EngineDefinitionRepository(new[] { engineDefinition }),
            new AirframeDefinitionRepository(new[] { airframeDefinition }));
        var family = new AircraftFamilyDefinition
        {
            Id = "fam.modular.grid", DisplayName = "Grid family",
            EngineDefinitionId = engineDefinition.Id, AirframeDefinitionId = airframeDefinition.Id,
            LockedEngineType = EngineType.Turbojet, LockedEngineCount = 2,
            LockedStructureType = StructureType.Monocoque, LockedMaterialType = MaterialType.Aluminum,
            BaseEngineParameters = new EngineParameters { TurbineDiameterMeters = 1.5d },
            BaseAirframeParameters = new AirframeParameters
            {
                UseModularWingProfile = true, WingDesignSpeedKph = designSpeed,
                WingPayloadAgility = orientation, WingProfileEfficiency = efficiency,
                WingAreaSquareMeters = 40d, FuelAmountLiters = 500d,
                EngineCount = 2, StructureType = StructureType.Monocoque, MaterialType = MaterialType.Aluminum,
            },
        };
        return factory.CreateAircraft(family,
            new AircraftTypeDefinition { Id = "type.modular.grid", FamilyId = family.Id, DisplayName = "Grid aircraft" });
    }
}
