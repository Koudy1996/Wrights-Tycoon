using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WrightTycoon.Core.Factories;
using WrightTycoon.Game.Launch;
using WrightTycoon.Game.UI;

namespace WrightTycoon.Game.Tests;

[Category("Extended")]
public sealed class FactoryPlannerGuiTests
{
    [TestCase(1280, false, 0)]
    [TestCase(1920, true, 0)]
    [TestCase(1280, true, 1)]
    [TestCase(1280, true, 2)]
    public void PlannerCanLayoutAndRepaintEmptyOrMixedFactoryNetworks(int width, bool withSites, int typeFilter)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("IMGUI view tests require a graphics-enabled EditMode run; the canonical headless suite validates planner data and icons separately.");
        var go = new GameObject("FactoryPlannerGuiTest");
        go.SetActive(false);
        var runner = go.AddComponent<WrightTycoonLaunchRunner>();
        var aircraft = new AircraftProductionFactory();
        var engines = new EngineManufacturingNetwork();
        if (withSites)
        {
            aircraft.TryStartFactoryConstruction(double.MaxValue, out _, out _);
            for (var month = 0; month < 18; month++) aircraft.AdvanceMonth();
            aircraft.TryStartFactoryConstruction(double.MaxValue, out _, out _);
            engines.TryConstruct(double.MaxValue, out _, out _);
        }
        void Set(string name, object value) => typeof(WrightTycoonLaunchRunner).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(runner, value);
        Set("_productionFactory", aircraft); Set("_engineManufacturing", engines); Set("_factoryPlannerTab", typeFilter == 2 ? 1 : 0);
        var draw = typeof(WrightTycoonLaunchRunner).GetMethod("DrawFactoryPlanner", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var window = ScriptableObject.CreateInstance<FactoryPlannerGuiTestWindow>();
        window.position = new Rect(10, 10, width, 900);
        window.Draw = () => draw.Invoke(runner, new object[] { (float)width, 900f });
        try
        {
            window.ShowUtility();
            window.SendEvent(new Event { type = EventType.Layout });
            window.SendEvent(new Event { type = EventType.Repaint });
            Assert.That(window.Error, Is.Null, window.Error?.ToString());
            Assert.That(window.DrawCount, Is.GreaterThan(0));
            Assert.That(aircraft.Factories.Count, Is.EqualTo(withSites ? 2 : 0), "Rendering must not construct or remove factories.");
            Assert.That(engines.Sites.Count, Is.EqualTo(withSites ? 1 : 0));
        }
        finally
        {
            window.Draw = null;
            window.Close();
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}

public sealed class FactoryPlannerGuiTestWindow : EditorWindow
{
    public Action? Draw;
    public Exception? Error;
    public int DrawCount;
    public Vector2 GuiOrigin;
    private void OnGUI()
    {
        if (Draw is null) return;
        GuiOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
        using var theme = WrightTycoonUiTheme.Begin(true);
        GUILayout.BeginArea(new Rect(0, 0, position.width, position.height));
        try { Draw(); DrawCount++; }
        catch (Exception e) { Error = e; }
        finally { GUILayout.EndArea(); }
    }
}
