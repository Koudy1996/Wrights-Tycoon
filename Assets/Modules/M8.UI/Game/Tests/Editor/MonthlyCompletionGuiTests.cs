using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Game.Launch;
using WrightTycoon.Game.UI;

namespace WrightTycoon.Game.Tests;

[Category("Extended")]
public sealed class MonthlyCompletionGuiTests
{
    [UnityTest]
    public IEnumerator SummarySupportsLayoutAndContinueClick() => VerifySummaryClick(1280f, 5);

    [UnityTest]
    public IEnumerator LongSummarySupportsScrollingAndContinueClick() => VerifySummaryClick(1920f, 80);

    private IEnumerator VerifySummaryClick(float width, int count)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Monthly completion GUI requires a graphics-enabled editor.");
        var go = new GameObject("MonthlyCompletionGui"); go.SetActive(false);
        var runner = go.AddComponent<WrightTycoonLaunchRunner>();
        var summary = (MonthlyCompletionSummary)MonthlyCompletionSummaryTests.Get(runner, "_monthlyCompletions")!;
        var entries = new string[count];
        for (var i = 0; i < count; i++) entries[i] = $"Completed research or factory {i + 1}";
        summary.Publish(20, entries);
        MonthlyCompletionSummaryTests.Set(runner, "_currentDate", new SimulationDate(1947, 9));
        var window = ScriptableObject.CreateInstance<MonthlyCompletionGuiTestWindow>();
        window.position = new Rect(10, 10, width, 900);
        window.Draw = () => MonthlyCompletionSummaryTests.Invoke(runner, "DrawMonthlyCompletionsDialog", new Vector2(width, 900));
        try
        {
            window.ShowUtility();
            window.position = new Rect(10, 10, width, 900);
            window.Focus();
            window.Repaint();
            yield return null;
            yield return null;
            window.SendEvent(new Event { type = EventType.Layout });
            window.SendEvent(new Event { type = EventType.Repaint });
            Assert.That(window.Error, Is.Null, window.Error?.ToString());
            var rect = (Rect)MonthlyCompletionSummaryTests.Get(runner, "_monthlyCompletionsContinueScreenRect")!;
            Assert.That(rect.height, Is.GreaterThan(0));
            var button = rect.center - window.GuiOrigin;
            Assert.That(button.x, Is.InRange(0f, window.position.width));
            Assert.That(button.y, Is.InRange(0f, window.position.height));
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, clickCount = 1, mousePosition = button });
            window.SendEvent(new Event { type = EventType.MouseUp, button = 0, clickCount = 1, mousePosition = button });
            yield return null;
            Assert.That(window.Error, Is.Null, window.Error?.ToString());
            Assert.That(summary.HasPending, Is.False, $"Continue must dismiss the single monthly summary. Screen button: {rect}, origin: {window.GuiOrigin}, local: {button}, window: {window.position}");
        }
        finally { window.Draw = null; window.Close(); UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void ApprovalFinalMonthsCanLayoutAndRepaintAtDoubleSize()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Approval GUI requires a graphics-enabled editor.");
        var go = new GameObject("ApprovalFinalMonthsGui"); go.SetActive(false);
        var runner = go.AddComponent<WrightTycoonLaunchRunner>();
        var draw = typeof(WrightTycoonLaunchRunner).GetMethod("DrawResearchApprovalTimeRow",
            BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int), typeof(string) }, null)!;
        var window = ScriptableObject.CreateInstance<MonthlyCompletionGuiTestWindow>();
        window.position = new Rect(10, 10, 640, 400);
        window.Draw = () => draw.Invoke(runner, new object[] { 16, "Engineering Time (months)" });
        try
        {
            window.ShowUtility(); window.position = new Rect(10, 10, 640, 400);
            window.SendEvent(new Event { type = EventType.Layout });
            window.SendEvent(new Event { type = EventType.Repaint });
            Assert.That(window.Error, Is.Null, window.Error?.ToString());
            Assert.That(window.DrawCount, Is.GreaterThan(0));
        }
        finally { window.Draw = null; window.Close(); UnityEngine.Object.DestroyImmediate(go); }
    }
}

public sealed class MonthlyCompletionGuiTestWindow : EditorWindow
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
        BeginWindows();
        try { Draw(); DrawCount++; }
        catch (Exception error) { Error = error; }
        finally { EndWindows(); }
    }
}
