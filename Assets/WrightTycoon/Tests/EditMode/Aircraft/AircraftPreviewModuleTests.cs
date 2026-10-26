using System;
using System.Reflection;
using NUnit.Framework;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
[Category("Extended")]
public sealed class AircraftPreviewModuleTests
{
    private static readonly Type FactoryType = Type.GetType(
        "WrightTycoon.Game.UI.AircraftPreviewModuleFactory, WrightTycoon.Game",
        throwOnError: true)!;

    [TestCase(1, 0)]
    [TestCase(2, 2)]
    [TestCase(3, 2)]
    [TestCase(4, 4)]
    [TestCase(5, 4)]
    [TestCase(6, 6)]
    public void WingEngineStations_ReserveOddCenterlineEngine(int engineCount, int expectedWingEngines)
    {
        var method = FactoryType.GetMethod(
            "CalculateWingEngineStations",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        var stations = (float[])method!.Invoke(null, new object[] { engineCount })!;
        Assert.That(stations, Has.Length.EqualTo(expectedWingEngines));

        var stationsPerWing = stations.Length / 2;
        for (var index = 0; index < stationsPerWing; index++)
        {
            Assert.That(stations[index], Is.LessThan(0f));
            Assert.That(stations[stationsPerWing + index], Is.EqualTo(-stations[index]).Within(0.001f));
        }
    }

    [TestCase(EngineType.Piston, 0d, "Inline")]
    [TestCase(EngineType.Inline, 0d, "Inline")]
    [TestCase(EngineType.V, 0d, "V")]
    [TestCase(EngineType.Radial, 0d, "Radial")]
    [TestCase(EngineType.Turbojet, 0d, "Turbojet")]
    [TestCase(EngineType.Turbofan, 6d, "LowBypassTurbofan")]
    [TestCase(EngineType.Turbofan, 12d, "HighBypassTurbofan")]
    public void EngineModuleSelection_UsesDistinctVisibleArchitecture(
        EngineType engineType,
        double bypassRatio,
        string expectedModule)
    {
        var method = FactoryType.GetMethod(
            "ResolveEngineModuleName",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        Assert.That(method!.Invoke(null, new object[] { engineType, bypassRatio }), Is.EqualTo(expectedModule));
    }

    [Test]
    public void Layout_GrowsFromAirframeParametersWithinPreviewBounds()
    {
        var layoutType = Type.GetType(
            "WrightTycoon.Game.UI.AircraftPreviewLayout, WrightTycoon.Game",
            throwOnError: true)!;
        var method = layoutType.GetMethod("From", BindingFlags.Public | BindingFlags.Static);
        var parameters = new AirframeParameters
        {
            WingAreaSquareMeters = 50d,
            FuelAmountLiters = 1_200d,
            CrewCount = 4,
        };

        Assert.That(method, Is.Not.Null);
        var layout = method!.Invoke(null, new object[] { parameters })!;
        var span = ReadFloat(layout, "Span");
        var length = ReadFloat(layout, "FuselageLength");
        var radius = ReadFloat(layout, "FuselageRadius");

        Assert.That(span, Is.InRange(2.8f, 9.5f));
        Assert.That(length, Is.InRange(3.2f, 8.6f));
        Assert.That(radius, Is.InRange(0.32f, 0.78f));
    }

    [Test]
    public void PreviewSignature_ChangesWithWingAspectRatio()
    {
        var rendererType = Type.GetType(
            "WrightTycoon.Game.UI.AircraftPreview3DRenderer, WrightTycoon.Game",
            throwOnError: true)!;
        var method = rendererType.GetMethod("BuildSignature", BindingFlags.NonPublic | BindingFlags.Static);
        var lowAspect = new AirframeParameters { WingAspectRatio = 4d };
        var highAspect = lowAspect.Clone();
        highAspect.WingAspectRatio = 16d;

        Assert.That(method, Is.Not.Null);
        var lowSignature = method!.Invoke(null, new object[] { lowAspect, EngineType.Inline, 0d });
        var highSignature = method.Invoke(null, new object[] { highAspect, EngineType.Inline, 0d });

        Assert.That(highSignature, Is.Not.EqualTo(lowSignature));
    }

    private static float ReadFloat(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null);
        return (float)property!.GetValue(value)!;
    }
}
