using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

[Category("Extended")]
public sealed class LandingGearUiTests
{
    [TestCase(1280)]
    [TestCase(1920)]
    public void GearBreakdownRendersAtDesignerWidthWithoutChangingReport(int width)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Gear UI requires graphics-enabled EditMode tests.");
        var go = new GameObject("Landing gear UI test");
        go.SetActive(false);
        var runner = go.AddComponent<WrightTycoonLaunchRunner>();
        var gear = LandingGearCalculator.Calculate(3750, LandingGearType.Retractable, .78, .78, 1, 1, 1);
        var before = gear with { };
        var window = ScriptableObject.CreateInstance<FactoryPlannerGuiTestWindow>();
        window.position = new Rect(10, 10, width, 900);
        var draw = typeof(WrightTycoonLaunchRunner).GetMethod("DrawLandingGearDetails", BindingFlags.Instance | BindingFlags.NonPublic)!;
        window.Draw = () =>
        {
            GUILayout.BeginVertical(GUILayout.Width(width * .5f));
            try { draw.Invoke(runner, new object[] { gear, LandingGearType.Retractable }); }
            finally { GUILayout.EndVertical(); }
        };
        try
        {
            window.ShowUtility();
            window.SendEvent(new Event { type = EventType.Layout });
            window.SendEvent(new Event { type = EventType.Repaint });
            Assert.That(window.Error, Is.Null, window.Error?.ToString());
            Assert.That(window.DrawCount, Is.GreaterThan(0));
            Assert.That(gear, Is.EqualTo(before));
        }
        finally
        {
            window.Draw = null;
            window.Close();
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
