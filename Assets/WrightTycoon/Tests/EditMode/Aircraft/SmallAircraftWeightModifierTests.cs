using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Physics;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class SmallAircraftWeightModifierTests
{
    [Test]
    public void LightSportWoodTrussWithAbout50KilowattPiston_MatchesAluminumWeightWhenBelow800Kilograms()
    {
        var atmosphere = new IsaAtmosphereModel();
        var aerodynamics = new AerodynamicsCalculator();

        var engineBuilder = new EngineBuilder(new EngineCalculator(), atmosphere);
        var engine = engineBuilder.Build(
            new EngineDefinition
            {
                Id = "eng.piston.light",
                DisplayName = "Light Utility Piston",
                EngineType = EngineType.Piston,
                BaseVolumetricEfficiency = 0.83d,
                BasePowerPerLiterKilowatts = 24d,
                PropFuelConsumptionPerKilowattHour = 0.29d,
                BaseReliability = 0.88d,
                BaseWeightKilograms = 45d,
                WeightPerDisplacementLiter = 7d,
                BaseCost = 9000d,
                CostPerOutputUnit = 55d,
                BaseProductionManDays = 20d,
                ComplexityFactor = 0.88d,
            },
            new EngineParameters
            {
                DisplacementLiters = 3.6d,
                CylinderCount = 4,
                CompressionRatio = 7.2d,
                BoostType = BoostType.None,
                PistonInjectionType = PistonInjectionType.Carburetor,
                OptimizationAltitudeMeters = 1200d,
                MaximumSpeed = 1800d,
                CoolingEfficiency = 0.78d,
            });

        var airframeBuilder = new AirframeBuilder(new AirframeCalculator(aerodynamics, atmosphere));
        var woodParameters = new AirframeParameters
        {
            EngineCount = 1,
            StructureType = StructureType.Truss,
            MaterialType = MaterialType.WoodFabric,
            WingPosition = WingPosition.High,
            WingShape = WingShape.Straight,
            WingAreaSquareMeters = 10d,
            TailType = TailType.Classic,
            FlapType = FlapType.Plain,
            LandingGearType = LandingGearType.Fixed,
            EngineCowling = 0.4d,
            FuelAmountLiters = 50d,
            CrewCount = 1,
            AerodynamicCleanlinessPercent = 2d,
            RunwayType = RunwayType.Short,
        };

        var aluminumParameters = woodParameters.Clone();
        aluminumParameters.MaterialType = MaterialType.Aluminum;

        var definition = new AirframeDefinition
        {
            Id = "air.light.woodtruss",
            DisplayName = "Wood Truss Light Frame",
            BaseStructuralWeightKilograms = 320d,
            ReferenceWingAreaSquareMeters = 12d,
            BaseDragCoefficient = 0.031d,
            BaseLiftCoefficient = 0.95d,
            BaseReliabilityModifier = 0.96d,
            BaseCost = 22000d,
            BaseProductionManDays = 72d,
        };

        var woodAirframe = airframeBuilder.Build(definition, woodParameters);
        var aluminumAirframe = airframeBuilder.Build(definition, aluminumParameters);

        var calculator = new AircraftPerformanceCalculator(atmosphere, aerodynamics);
        var woodPerformance = calculator.Calculate(engine, woodAirframe, AircraftCategory.LightSportAndRecreational);
        var aluminumPerformance = calculator.Calculate(engine, aluminumAirframe, AircraftCategory.LightSportAndRecreational);
        var seaLevelPowerKilowatts = engine.Performance.GetPerformanceAtAltitude(0d).Output;

        Assert.That(engine.Performance.OutputKind, Is.EqualTo(EngineOutputKind.PowerKilowatts));
        Assert.That(seaLevelPowerKilowatts, Is.InRange(45d, 50d));
        Assert.That(woodPerformance.TotalWeight.Kilograms, Is.LessThan(800d));
        Assert.That(aluminumPerformance.TotalWeight.Kilograms, Is.LessThan(800d));
        Assert.That(woodPerformance.TotalWeight.Kilograms, Is.EqualTo(aluminumPerformance.TotalWeight.Kilograms).Within(1.5d));
    }
}
