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
    [Category("Extended")]
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
                UseModularWingProfile = true,
                WingDesignSpeedKph = 650d,
                WingPayloadAgility = -35d,
                WingProfileEfficiency = 85d,
            },
        };

        var firstResult = ensureMethod.Invoke(runner, new object[] { family.Id, family, "Player Family" });
        var secondResult = ensureMethod.Invoke(runner, new object[] { family.Id, family, "Player Family" });

        Assert.That(firstResult, Is.True);
        Assert.That(secondResult, Is.False);
        Assert.That(families.Count, Is.EqualTo(1));
        var saved = families[0]!;
        var savedType = saved.GetType();
        Assert.That(savedType.GetProperty("UseModularWingProfile")!.GetValue(saved), Is.True);
        Assert.That(savedType.GetProperty("WingDesignSpeedKph")!.GetValue(saved), Is.EqualTo(650d));
        Assert.That(savedType.GetProperty("WingPayloadAgility")!.GetValue(saved), Is.EqualTo(-35d));
        Assert.That(savedType.GetProperty("WingProfileEfficiency")!.GetValue(saved), Is.EqualTo(85d));
    }

    [Test]
    [Category("Extended")]
    public void NewFamily_DefaultsToModularProfileBeforeFirstDraw()
    {
        var runnerType = GetRunnerType();
        var create = runnerType.GetMethod("CreateDefaultAirframeParameters", BindingFlags.NonPublic | BindingFlags.Static)!;
        var parameters = (AirframeParameters)create.Invoke(null, new object[] { 1 })!;
        Assert.That(parameters.UseModularWingProfile, Is.True);
        Assert.That(parameters.WingDesignSpeedKph, Is.EqualTo(350d));
        Assert.That(parameters.WingPayloadAgility, Is.Zero);
        Assert.That(parameters.WingProfileEfficiency, Is.EqualTo(50d));
    }

    [TestCase(324d, 100d, 2000d, 50d, 300d)]
    [TestCase(325d, 100d, 2000d, 50d, 350d)]
    [TestCase(2100d, 100d, 2000d, 50d, 2000d)]
    [TestCase(-33d, -100d, 100d, 5d, -35d)]
    [TestCase(83d, 0d, 100d, 5d, 85d)]
    [Category("Extended")]
    public void ProfileInputs_SnapToMeaningfulPlayerSteps(double value, double min, double max, double step, double expected)
    {
        var snap = GetRunnerType().GetMethod("SnapWingProfileValue", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.That(snap.Invoke(null, new object[] { value, min, max, step }), Is.EqualTo(expected));
    }

    [Test]
    public void FamilyProfileSaveState_RoundTripsAllModularSettings()
    {
        var stateType = GetRunnerType().GetNestedType("SavedFamilyDesignState", BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(stateType, nonPublic: true)!;
        stateType.GetProperty("UseModularWingProfile")!.SetValue(state, true);
        stateType.GetProperty("WingDesignSpeedKph")!.SetValue(state, 1450d);
        stateType.GetProperty("WingPayloadAgility")!.SetValue(state, 70d);
        stateType.GetProperty("WingProfileEfficiency")!.SetValue(state, 95d);
        var jsonType = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json", throwOnError: true)!;
        var json = (string)jsonType.GetMethod("SerializeObject", new[] { typeof(object) })!.Invoke(null, new[] { state })!;
        var restored = jsonType.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) })!
            .Invoke(null, new object[] { json, stateType })!;
        foreach (var name in new[] { "UseModularWingProfile", "WingDesignSpeedKph", "WingPayloadAgility", "WingProfileEfficiency" })
            Assert.That(stateType.GetProperty(name)!.GetValue(restored), Is.EqualTo(stateType.GetProperty(name)!.GetValue(state)), name);
    }

    [TestCase(0)]
    [TestCase(13)]
    [TestCase(17)]
    [Category("Extended")]
    public void IncompatibleSave_IsRejectedBeforeMutatingRuntimeState(int version)
    {
        var runnerType = GetRunnerType();
        var runner = FormatterServices.GetUninitializedObject(runnerType);
        var name = runnerType.GetField("_playerCompanyName", BindingFlags.NonPublic | BindingFlags.Instance)!;
        name.SetValue(runner, "Preserved company");
        var stateType = runnerType.GetNestedType("RunnerGameSavePayload", BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(stateType, nonPublic: true)!;
        stateType.GetProperty("Version")!.SetValue(state, version);
        var load = runnerType.GetMethod("loadGameState", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var error = Assert.Throws<TargetInvocationException>(() => load.Invoke(runner, new[] { state }));
        Assert.That(error!.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(error.InnerException!.Message, Does.Contain("incompatible"));
        Assert.That(name.GetValue(runner), Is.EqualTo("Preserved company"));
    }

    private static Type GetRunnerType() => Type.GetType(
        "WrightTycoon.Game.Launch.WrightTycoonLaunchRunner, WrightTycoon.Game", throwOnError: true)!;
}
