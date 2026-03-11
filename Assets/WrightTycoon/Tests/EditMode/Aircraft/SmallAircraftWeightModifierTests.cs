using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Physics;
using WrightTycoon.Core.Simulation;

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

    [Test]
    public void LightSport1946_Under60KilowattsAndUnder10SquareMeters_StaysNear600KilogramsAnd150KilometersPerHour()
    {
        var atmosphere = new IsaAtmosphereModel();
        var aerodynamics = new AerodynamicsCalculator();
        var currentDate = new SimulationDate(1946, 1);
        var technologySource = new HistoricalCampaignRuntimeTechnologyModifierSource(
            new HistoricalCampaignTechnologyProgression(),
            () => currentDate);

        var engineBuilder = new EngineBuilder(new EngineCalculator(), atmosphere, technologySource);
        var engine = engineBuilder.Build(
            new EngineDefinition
            {
                Id = "eng.piston.sport.probe",
                DisplayName = "Sport Piston Probe",
                EngineType = EngineType.Inline,
                BaseVolumetricEfficiency = 0.93d,
                BasePowerPerLiterKilowatts = 30d,
                PropFuelConsumptionPerKilowattHour = 0.245d,
                BaseReliability = 0.86d,
                BaseWeightKilograms = 48d,
                WeightPerDisplacementLiter = 6.8d,
                BaseCost = 12000d,
                CostPerOutputUnit = 82d,
                BaseProductionManDays = 34d,
                ComplexityFactor = 0.86d,
            },
            new EngineParameters
            {
                DisplacementLiters = 2.4d,
                CylinderCount = 4,
                CompressionRatio = 7.2d,
                BoostType = BoostType.None,
                PistonInjectionType = PistonInjectionType.Carburetor,
                OptimizationAltitudeMeters = 1200d,
                MaximumSpeed = 1800d,
                CoolingEfficiency = 0.82d,
            });

        var airframeBuilder = new AirframeBuilder(new AirframeCalculator(aerodynamics, atmosphere), technologySource);
        var airframe = airframeBuilder.Build(
            new AirframeDefinition
            {
                Id = "air.sport.light.probe",
                DisplayName = "Light Sport Probe",
                BaseStructuralWeightKilograms = 320d,
                ReferenceWingAreaSquareMeters = 12d,
                BaseDragCoefficient = 0.031d,
                BaseLiftCoefficient = 0.95d,
                BaseReliabilityModifier = 0.96d,
                BaseCost = 22000d,
                BaseProductionManDays = 72d,
            },
            new AirframeParameters
            {
                EngineCount = 1,
                StructureType = StructureType.Truss,
                MaterialType = MaterialType.WoodFabric,
                WingPosition = WingPosition.Biplane,
                WingShape = WingShape.Straight,
                WingProfile = WingProfileType.Standard,
                WingAreaSquareMeters = 9.8d,
                TailType = TailType.Classic,
                FlapType = FlapType.Plain,
                HasLeadingEdgeFlaps = false,
                HasBackupSystems = false,
                LandingGearType = LandingGearType.Retractable,
                RunwayType = RunwayType.Short,
                EngineCowling = 0.9d,
                AerodynamicCleanlinessPercent = 20d,
                FuelAmountLiters = 100d,
                CrewCount = 1,
            });

        var report = new AircraftPerformanceCalculator(atmosphere, aerodynamics)
            .Calculate(engine, airframe, AircraftCategory.LightSportAndRecreational);
        var seaLevelPower = engine.Performance.GetPerformanceAtAltitude(0d).Output;
        var seaLevelSpeedKilometersPerHour = report.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond * 3.6d;

        Assert.That(seaLevelPower, Is.GreaterThan(35d).And.LessThan(60d));
        Assert.That(airframe.Parameters.WingAreaSquareMeters, Is.LessThan(10d));
        Assert.That(report.AircraftWeightWithFuelKilograms, Is.InRange(560d, 680d));
        Assert.That(seaLevelSpeedKilometersPerHour, Is.InRange(140d, 165d));
    }
}
