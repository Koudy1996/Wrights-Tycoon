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
    public void SavedAircraftDecommissionState_RoundTripsAndLegacyStateDefaultsToActive()
    {
        var runnerType = Type.GetType(
            "WrightTycoon.Game.Launch.WrightTycoonLaunchRunner, WrightTycoon.Game",
            throwOnError: true)!;
        var stateType = runnerType.GetNestedType(
            "SavedAircraftDesignState",
            BindingFlags.NonPublic)!;
        var decommissionedProperty = stateType.GetProperty(
            "IsDecommissioned",
            BindingFlags.Public | BindingFlags.Instance)!;
        var jsonConvertType = Type.GetType(
            "Newtonsoft.Json.JsonConvert, Newtonsoft.Json",
            throwOnError: true)!;
        var serializeMethod = jsonConvertType.GetMethod(
            "SerializeObject",
            new[] { typeof(object) })!;
        var deserializeMethod = jsonConvertType.GetMethod(
            "DeserializeObject",
            new[] { typeof(string), typeof(Type) })!;

        var state = Activator.CreateInstance(stateType, nonPublic: true)!;
        decommissionedProperty.SetValue(state, true);
        var json = (string)serializeMethod.Invoke(null, new[] { state })!;
        var restored = deserializeMethod.Invoke(null, new object[] { json, stateType })!;
        var legacyRestored = deserializeMethod.Invoke(null, new object[] { "{}", stateType })!;

        Assert.That(decommissionedProperty.GetValue(restored), Is.True);
        Assert.That(decommissionedProperty.GetValue(legacyRestored), Is.False);
    }

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
