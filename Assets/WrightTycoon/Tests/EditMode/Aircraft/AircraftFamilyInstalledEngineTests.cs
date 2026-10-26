using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
[Category("Extended")]
public sealed class AircraftFamilyInstalledEngineTests
{
    [Test]
    public void WithInstalledEngine_ReplacesEngineAndPreservesFamilyStructure()
    {
        var family = CreateFamily(AircraftAllowedEngineFamily.Propeller, EngineType.Radial);
        var replacementParameters = new EngineParameters
        {
            DisplacementLiters = 18d,
            CompressionRatio = 9d,
            CoolingEfficiency = 0.8d,
        };

        var updated = family.WithInstalledEngine(
            "eng.replacement",
            EngineType.Inline,
            replacementParameters);

        Assert.That(updated.EngineDefinitionId, Is.EqualTo("eng.replacement"));
        Assert.That(updated.LockedEngineType, Is.EqualTo(EngineType.Inline));
        Assert.That(updated.BaseEngineParameters.DisplacementLiters, Is.EqualTo(18d));
        Assert.That(updated.Id, Is.EqualTo(family.Id));
        Assert.That(updated.AirframeDefinitionId, Is.EqualTo(family.AirframeDefinitionId));
        Assert.That(updated.LockedEngineCount, Is.EqualTo(family.LockedEngineCount));
        Assert.That(updated.BaseAirframeParameters, Is.Not.SameAs(family.BaseAirframeParameters));
        Assert.That(updated.BaseEngineParameters, Is.Not.SameAs(replacementParameters));
    }

    [Test]
    public void WithInstalledEngine_RejectsEngineOutsideFamilyPolicy()
    {
        var family = CreateFamily(AircraftAllowedEngineFamily.Propeller, EngineType.Radial);

        Assert.Throws<System.InvalidOperationException>(() => family.WithInstalledEngine(
            "eng.jet",
            EngineType.Turbojet,
            new EngineParameters()));
    }

    [Test]
    public void WithInstalledEngine_RejectsEngineOutsideFamilyClassification()
    {
        var family = CreateFamily(
            AircraftAllowedEngineFamily.Any,
            EngineType.Radial,
            AircraftEngineClassification.Piston);

        Assert.Throws<System.InvalidOperationException>(() => family.WithInstalledEngine(
            "eng.jet",
            EngineType.Turbojet,
            new EngineParameters()));
    }

    private static AircraftFamilyDefinition CreateFamily(
        AircraftAllowedEngineFamily allowedEngineFamily,
        EngineType engineType,
        AircraftEngineClassification engineClassification = AircraftEngineClassification.Any) =>
        new()
        {
            Id = "fam.engine.swap",
            DisplayName = "Engine Swap Family",
            EngineDefinitionId = "eng.source",
            AirframeDefinitionId = "air.source",
            LockedEngineType = engineType,
            AllowedEngineFamily = allowedEngineFamily,
            EngineClassification = engineClassification,
            LockedStructureType = StructureType.Monocoque,
            LockedMaterialType = MaterialType.Aluminum,
            LockedEngineCount = 2,
            BaseEngineParameters = new EngineParameters(),
            BaseAirframeParameters = new AirframeParameters(),
        };
}
