using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class DesignerFamilyAutoSaveTests
{
    [Test]
    public void EnsureFamilySaved_AddsMissingFamilyExactlyOnce()
    {
        var runnerType = Type.GetType(
            "WrightTycoon.Game.Launch.WrightTycoonLaunchRunner, WrightTycoon.Game",
            throwOnError: true)!;
        var runner = FormatterServices.GetUninitializedObject(runnerType);
        var familiesField = runnerType.GetField(
            "_savedFamilies",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var families = (IList)Activator.CreateInstance(familiesField.FieldType)!;
        familiesField.SetValue(runner, families);
        var ensureMethod = runnerType.GetMethod(
            "EnsureFamilySaved",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var family = new AircraftFamilyDefinition
        {
            Id = "fam.auto.saved",
            DisplayName = "Auto Saved Family",
            EngineDefinitionId = "eng.test",
            AirframeDefinitionId = "air.test",
            LockedEngineType = EngineType.Radial,
            LockedEngineCount = 2,
            LockedStructureType = StructureType.Monocoque,
            LockedMaterialType = MaterialType.Aluminum,
            BaseEngineParameters = new EngineParameters(),
            BaseAirframeParameters = new AirframeParameters
            {
                WingAreaSquareMeters = 42d,
                WingAspectRatio = 9d,
            },
        };

        var firstResult = ensureMethod.Invoke(runner, new object[] { family.Id, family, "Player Family" });
        var secondResult = ensureMethod.Invoke(runner, new object[] { family.Id, family, "Player Family" });

        Assert.That(firstResult, Is.True);
        Assert.That(secondResult, Is.False);
        Assert.That(families.Count, Is.EqualTo(1));
    }
}
