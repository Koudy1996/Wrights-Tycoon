using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

[Category("Extended")]
public sealed class ModularWingProfileGuiTests
{
    [TestCase(1280, 100d, -100d, 0d)]
    [TestCase(1280, 350d, 0d, 50d)]
    [TestCase(1920, 2000d, 100d, 100d)]
    public void FamilyProfileSection_LayoutsAndRepaintsThreeSteppedInputsInsteadOfEnum(
        int width, double speed, double orientation, double efficiency)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Family profile IMGUI layout requires a graphics-enabled EditMode run.");
        var go = new GameObject("ModularWingProfileGuiTest");
        go.SetActive(false);
        var runner = go.AddComponent<WrightTycoonLaunchRunner>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var parameters = new AirframeParameters
        {
            WingDesignSpeedKph = speed,
            WingPayloadAgility = orientation,
            WingProfileEfficiency = efficiency,
        };
        typeof(WrightTycoonLaunchRunner).GetField("_baseAirframeParameters", flags)!.SetValue(runner, parameters);
        var draw = typeof(WrightTycoonLaunchRunner).GetMethod("DrawModularWingProfileControls", flags)!;
        var window = ScriptableObject.CreateInstance<FactoryPlannerGuiTestWindow>();
        window.position = new Rect(10, 10, width, 900);
        window.Draw = () =>
        {
            // This is the exact production family-architecture profile section,
            // rendered at the designer's half-window content width.
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width * .5f));
            try { draw.Invoke(runner, Array.Empty<object>()); }
            finally { GUILayout.EndVertical(); }
        };
        try
        {
            window.ShowUtility();
            window.SendEvent(new Event { type = EventType.Layout });
            window.SendEvent(new Event { type = EventType.Repaint });
            Assert.That(window.Error, Is.Null, window.Error?.ToString());
            Assert.That(window.DrawCount, Is.GreaterThan(0));
            var fields = (Dictionary<string, string>)typeof(WrightTycoonLaunchRunner)
                .GetField("_doubleSliderInputBuffer", flags)!.GetValue(runner)!;
            Assert.That(fields.Count, Is.EqualTo(3));
            Assert.That(fields.Keys.Any(key => key.Contains("Wing Design Speed at 0 m")), Is.True);
            Assert.That(fields.Keys.Any(key => key.Contains("Payload (-100) / Agility (+100)")), Is.True);
            Assert.That(fields.Keys.Any(key => key.Contains("Profile Efficiency (higher cost / better performance)")), Is.True);
            Assert.That(typeof(WrightTycoonLaunchRunner).GetMethod("DrawWingProfileSelector", flags), Is.Null);
            Assert.That(parameters.UseModularWingProfile, Is.True);
            Assert.That(parameters.WingDesignSpeedKph, Is.EqualTo(speed));
            Assert.That(parameters.WingPayloadAgility, Is.EqualTo(orientation));
            Assert.That(parameters.WingProfileEfficiency, Is.EqualTo(efficiency));
        }
        finally
        {
            window.Draw = null;
            window.Close();
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
