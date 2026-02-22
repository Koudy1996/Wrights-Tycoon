using System;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Core.Aircraft;

public sealed class Aircraft
{
    public Aircraft(
        AircraftFamilyDefinition familyDefinition,
        AircraftTypeDefinition typeDefinition,
        Engine engine,
        Airframe airframe,
        AircraftPerformanceReport performance,
        DevelopmentCostReport development)
    {
        FamilyDefinition = familyDefinition ?? throw new ArgumentNullException(nameof(familyDefinition));
        TypeDefinition = typeDefinition ?? throw new ArgumentNullException(nameof(typeDefinition));
        Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        Airframe = airframe ?? throw new ArgumentNullException(nameof(airframe));
        Performance = performance ?? throw new ArgumentNullException(nameof(performance));
        Development = development ?? throw new ArgumentNullException(nameof(development));
    }

    public AircraftFamilyDefinition FamilyDefinition { get; }

    public AircraftTypeDefinition TypeDefinition { get; }

    public Engine Engine { get; }

    public Airframe Airframe { get; }

    public AircraftPerformanceReport Performance { get; }

    public DevelopmentCostReport Development { get; }
}
