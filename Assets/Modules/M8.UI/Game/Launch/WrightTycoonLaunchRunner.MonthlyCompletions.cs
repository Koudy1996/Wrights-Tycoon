using System.Collections.Generic;
using UnityEngine;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private readonly MonthlyCompletionSummary _monthlyCompletions = new();
    private Rect _monthlyCompletionsRect;
    private Vector2 _monthlyCompletionsScroll;
    private Rect _monthlyCompletionsContinueScreenRect;

    private Dictionary<string, (bool Building, bool Expanding)> CaptureFactoryConstructionStates()
    {
        var states = new Dictionary<string, (bool, bool)>();
        if (_productionFactory is not null)
            foreach (var factory in _productionFactory.Factories)
                states[factory.FactoryId] = (factory.ConstructionMonthsRemaining > 0, factory.ExpansionMonthsRemaining > 0);
        foreach (var factory in _engineManufacturing.Sites)
            states[factory.SiteId] = (factory.ConstructionMonthsRemaining > 0, factory.ExpansionMonthsRemaining > 0);
        return states;
    }

    private void CollectCompletedFactoryConstruction(Dictionary<string, (bool Building, bool Expanding)> before, List<string> events)
    {
        if (_productionFactory is not null)
            foreach (var factory in _productionFactory.Factories)
                if (before.TryGetValue(factory.FactoryId, out var old))
                {
                    if (old.Building && factory.ConstructionMonthsRemaining == 0)
                        events.Add($"Aircraft factory completed: {factory.DisplayName} ({factory.BuildingSize})");
                    if (old.Expanding && factory.ExpansionMonthsRemaining == 0)
                        events.Add($"Aircraft factory expanded: {factory.DisplayName} ({factory.BuildingSize})");
                }
        foreach (var factory in _engineManufacturing.Sites)
            if (before.TryGetValue(factory.SiteId, out var old))
            {
                if (old.Building && factory.ConstructionMonthsRemaining == 0)
                    events.Add($"Engine factory completed: {factory.SiteId} ({factory.BuildingSize})");
                if (old.Expanding && factory.ExpansionMonthsRemaining == 0)
                    events.Add($"Engine factory expanded: {factory.SiteId} ({factory.BuildingSize})");
            }
    }

    private void DrawMonthlyCompletionsDialog(Vector2 screen)
    {
        if (!_monthlyCompletions.HasPending || _pendingEmergencyLoanOffer is not null) return;
        DrawRect(new Rect(0, 0, screen.x, screen.y), new Color(0f, 0f, 0f, .72f));
        if (_monthlyCompletionsRect.width <= 0)
        {
            var width = Mathf.Min(760f, screen.x - 40f);
            var height = Mathf.Min(520f, screen.y - 40f);
            _monthlyCompletionsRect = new Rect((screen.x - width) / 2, (screen.y - height) / 2, width, height);
        }
        ClampPopupWindowRect(ref _monthlyCompletionsRect, screen, new Vector2(400f, 220f), 20f);
        _monthlyCompletionsRect = GUI.Window(9410, _monthlyCompletionsRect, DrawMonthlyCompletionsContents,
            $"Monthly Completions — {FormatSimulationDate(_currentDate)}");
        GUI.BringWindowToFront(9410);
    }

    private void DrawMonthlyCompletionsContents(int id)
    {
        GUILayout.BeginArea(new Rect(16, 32, _monthlyCompletionsRect.width - 32, _monthlyCompletionsRect.height - 48));
        GUILayout.Label("Research & Construction", new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold });
        _monthlyCompletionsScroll = GUILayout.BeginScrollView(_monthlyCompletionsScroll);
        var entryStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
        foreach (var entry in _monthlyCompletions.Entries) GUILayout.Label("• " + entry, entryStyle);
        GUILayout.EndScrollView();
        var continueRect = GUILayoutUtility.GetRect(new GUIContent("Continue"), GUI.skin.button, GUILayout.Height(36f));
        if (Event.current.type == EventType.Repaint)
            _monthlyCompletionsContinueScreenRect = GUIUtility.GUIToScreenRect(continueRect);
        if (GUI.Button(continueRect, "Continue")) _monthlyCompletions.Dismiss();
        GUILayout.EndArea();
        GUI.DragWindow(new Rect(0, 0, _monthlyCompletionsRect.width, 28));
    }
}
