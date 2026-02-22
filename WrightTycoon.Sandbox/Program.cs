using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Data;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Physics;

var repoRoot = ResolveRepositoryRoot();
var engineJsonPath = Path.Combine(repoRoot, "Data", "Examples", "engine-definitions.json");
var airframeJsonPath = Path.Combine(repoRoot, "Data", "Examples", "airframe-definitions.json");

var engineRepository = new JsonEngineDefinitionRepository().LoadFromFile(engineJsonPath);
var airframeRepository = new JsonAirframeDefinitionRepository().LoadFromFile(airframeJsonPath);

var atmosphere = new IsaAtmosphereModel();
var aerodynamics = new AerodynamicsCalculator();
var engineBuilder = new EngineBuilder(new EngineCalculator(), atmosphere);
var airframeBuilder = new AirframeBuilder(new AirframeCalculator(aerodynamics, atmosphere));
var aircraftFactory = new AircraftFactory(
    engineBuilder,
    airframeBuilder,
    new AircraftPerformanceCalculator(atmosphere, aerodynamics),
    new DevelopmentCostCalculator(),
    engineRepository,
    airframeRepository);

var familySingle = CreateFamily("fam.sandbox.single", "Prototype Single", lockedEngineCount: 1);
var familyTwin = CreateFamily("fam.sandbox.twin", "Prototype Twin", lockedEngineCount: 2);
var variant = new AircraftTypeDefinition
{
    Id = "type.sandbox.variant",
    FamilyId = familySingle.Id,
    DisplayName = "Cargo Variant",
    EngineDelta = new EngineParameterDelta
    {
        CompressionRatioDelta = 0.4d,
        BoostTypeOverride = BoostType.Turbo,
        OptimizationAltitudeMetersDelta = 1200d,
    },
    AirframeDelta = new AirframeParameterDelta
    {
        WingAreaSquareMetersDelta = 5d,
        FuelAmountLitersDelta = 350d,
        LandingGearTypeOverride = LandingGearType.Retractable,
    },
};

var singleAircraft = aircraftFactory.CreateAircraft(familySingle, variant);
variant = new AircraftTypeDefinition
{
    Id = "type.sandbox.variant",
    FamilyId = familyTwin.Id,
    DisplayName = "Cargo Variant",
    EngineDelta = new EngineParameterDelta
    {
        CompressionRatioDelta = 0.4d,
        BoostTypeOverride = BoostType.Turbo,
        OptimizationAltitudeMetersDelta = 1200d,
    },
    AirframeDelta = new AirframeParameterDelta
    {
        WingAreaSquareMetersDelta = 5d,
        FuelAmountLitersDelta = 350d,
        LandingGearTypeOverride = LandingGearType.Retractable,
    },
};
var twinAircraft = aircraftFactory.CreateAircraft(familyTwin, variant);

Console.WriteLine("Wright Tycoon Sandbox");
Console.WriteLine("=====================");
Console.WriteLine();
Console.WriteLine("Single-Engine Aircraft");
PrintAircraftReport(singleAircraft);
Console.WriteLine();
Console.WriteLine("Twin-Engine Aircraft");
PrintAircraftReport(twinAircraft);
Console.WriteLine();
Console.WriteLine("Comparison (Twin - Single)");
Console.WriteLine($"Sea level max speed delta: {twinAircraft.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond - singleAircraft.Performance.MaxSpeedByAltitude[0].MaxSpeedMetersPerSecond:0.0} m/s");
Console.WriteLine($"Range delta: {twinAircraft.Performance.RangeKilometers - singleAircraft.Performance.RangeKilometers:0.0} km");
Console.WriteLine($"Payload delta: {twinAircraft.Performance.PayloadCapacity.Kilograms - singleAircraft.Performance.PayloadCapacity.Kilograms:0.0} kg");
Console.WriteLine($"Production cost delta: {twinAircraft.Performance.TotalProductionCost - singleAircraft.Performance.TotalProductionCost:0}");
Console.WriteLine($"Reliability delta: {twinAircraft.Performance.OverallReliability - singleAircraft.Performance.OverallReliability:0.000}");

static void PrintAircraftReport(Aircraft aircraft)
{
    Console.WriteLine($"Family: {aircraft.FamilyDefinition.DisplayName}");
    Console.WriteLine($"Type: {aircraft.TypeDefinition.DisplayName}");
    Console.WriteLine($"Engine type: {aircraft.Engine.Definition.EngineType}");
    Console.WriteLine($"Engine count: {aircraft.Airframe.Parameters.EngineCount}");
    Console.WriteLine($"Engine output kind: {aircraft.Engine.Performance.OutputKind}");
    Console.WriteLine("Engine altitude performance:");
    foreach (var point in aircraft.Engine.Performance.AltitudePerformance)
    {
        Console.WriteLine($"  {point.AltitudeMeters,6:0} m | output {point.Output,10:0.0} | fuel {point.FuelConsumptionKilogramsPerHour,8:0.0} kg/h");
    }

    Console.WriteLine($"Airframe weight: {aircraft.Airframe.Performance.TotalWeight.Kilograms:0.0} kg");
    Console.WriteLine($"Airframe Cd/Cl: {aircraft.Airframe.Performance.DragCoefficient:0.000} / {aircraft.Airframe.Performance.LiftCoefficient:0.000}");
    Console.WriteLine($"Airframe maneuverability: {aircraft.Airframe.Performance.Maneuverability:0.0}");
    Console.WriteLine("Aircraft max speed:");
    foreach (var point in aircraft.Performance.MaxSpeedByAltitude)
    {
        Console.WriteLine($"  {point.AltitudeMeters,6:0} m | {point.MaxSpeedMetersPerSecond,8:0.0} m/s");
    }

    Console.WriteLine($"Range: {aircraft.Performance.RangeKilometers:0.0} km");
    Console.WriteLine($"Optimal cruise speed: {aircraft.Performance.OptimalCruiseSpeedMetersPerSecond:0.0} m/s @ {aircraft.Performance.OptimalCruiseAltitudeMeters:0} m");
    Console.WriteLine($"Cruise fuel burn (avg/full/empty): {aircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour:0.0} / {aircraft.Performance.FullFuelCruiseBurnKilogramsPerHour:0.0} / {aircraft.Performance.EmptyFuelCruiseBurnKilogramsPerHour:0.0} kg/h");
    Console.WriteLine($"Payload: {aircraft.Performance.PayloadCapacity.Kilograms:0.0} kg");
    Console.WriteLine($"Total weight: {aircraft.Performance.TotalWeight.Kilograms:0.0} kg");
    Console.WriteLine($"Production cost: {aircraft.Performance.TotalProductionCost:0}");
    Console.WriteLine($"Production man-days: {aircraft.Performance.TotalProductionManDays:0.0}");
    Console.WriteLine($"Overall reliability: {aircraft.Performance.OverallReliability:0.000}");
    Console.WriteLine($"Development cost: {aircraft.Development.DevelopmentCost:0}");
    Console.WriteLine($"Development man-days: {aircraft.Development.DevelopmentManDays:0.0}");
}

static AircraftFamilyDefinition CreateFamily(string id, string displayName, int lockedEngineCount) =>
    new()
    {
        Id = id,
        DisplayName = displayName,
        EngineDefinitionId = "eng.radial.main",
        AirframeDefinitionId = "air.medium.main",
        LockedEngineType = EngineType.Radial,
        LockedStructureType = StructureType.Monocoque,
        LockedMaterialType = MaterialType.Aluminum,
        LockedEngineCount = lockedEngineCount,
        BaseDevelopmentCost = 92000d,
        BaseDevelopmentManDays = 240d,
        BaseEngineParameters = new EngineParameters
        {
            DisplacementLiters = 17d,
            CompressionRatio = 8.6d,
            BoostType = BoostType.None,
            OptimizationAltitudeMeters = 1800d,
            CoolingEfficiency = 0.77d,
        },
        BaseAirframeParameters = new AirframeParameters
        {
            EngineCount = lockedEngineCount,
            StructureType = StructureType.Monocoque,
            MaterialType = MaterialType.Aluminum,
            WingPosition = WingPosition.Low,
            WingShape = WingShape.Straight,
            WingAreaSquareMeters = 36d,
            TailType = TailType.Classic,
            FlapType = FlapType.Fowler,
            LandingGearType = LandingGearType.SemiRetractable,
            EngineCowling = 0.55d,
            FuelAmountLiters = 1500d,
            CrewCount = 2,
        },
    };

static string ResolveRepositoryRoot()
{
    var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    if (Directory.Exists(Path.Combine(candidate, "Assets", "Modules", "M1.Core")))
    {
        return candidate;
    }

    // Backward compatibility for pre-v0.2 layout.
    if (Directory.Exists(Path.Combine(candidate, "Assets", "WrightTycoon", "Core")))
    {
        return candidate;
    }

    return Directory.GetCurrentDirectory();
}
