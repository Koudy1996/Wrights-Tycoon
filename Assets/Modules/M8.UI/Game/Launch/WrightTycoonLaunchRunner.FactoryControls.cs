using System;
using System.Collections.Generic;
using UnityEngine;
using WrightTycoon.Game.UI;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    // Screen-space bounds are also consumed by real mouse-event integration tests.
    private readonly Dictionary<string, Rect> _factoryPlannerControlRects = new();
    private readonly Dictionary<string, bool> _factoryPlannerControlEnabled = new();
    private string _factoryChoiceOpen = string.Empty;
    private Vector2 _factoryChoiceScroll;

    private void RecordFactoryControl(string id)
    {
        if (Event.current.type != EventType.Repaint) return;
        var rect = GUILayoutUtility.GetLastRect();
        rect.position = GUIUtility.GUIToScreenPoint(rect.position);
        _factoryPlannerControlRects[id] = rect;
        _factoryPlannerControlEnabled[id] = GUI.enabled;
    }

    private bool PlannerButton(string id, string label, params GUILayoutOption[] options)
    {
        var clicked = GUILayout.Button(label, ModernStyle(WrightTycoonUiTheme.SecondaryButtonStyleName), options);
        RecordFactoryControl(id);
        return clicked;
    }

    private int DrawFactoryPartsAllocation(string id, int percent, double availableManDays)
    {
        GUILayout.Label($"Parts share: {percent}% · Whole machines share: {100 - percent}%");
        var next = Mathf.Clamp(Mathf.RoundToInt(WrightTycoonSlider.Layout(percent, 1f, 100f)), 1, 100);
        RecordFactoryControl(id);
        DrawWrappedFactoryStatus($"Parts reserve: {FormatUiNumber(availableManDays * next / 100d, "0")} MD/month. When either category reaches its target, unused capacity transfers to the other. Only work needed for the remaining targets is used. Changes apply to the next unprocessed month.");
        return next;
    }

    private int DrawFactoryAutomationChoice(string id, int tier, bool confirmed)
    {
        var labels = new[] { "Hand", "Jigs", "Mechanized", "Automated", "Sophisticated" };
        tier = Mathf.Clamp(tier, 0, labels.Length - 1);
        if (confirmed)
        {
            DrawModernInfoRow("Installed Automation", labels[tier]);
            RecordFactoryControl(id + ".current");
            return tier;
        }
        GUILayout.Label("Automation");
        GUILayout.BeginHorizontal();
        var selected = DrawPlannerTabs(id, tier, labels);
        GUILayout.EndHorizontal();
        return selected;
    }

    private bool PlannerPrimaryButton(string id, string label, params GUILayoutOption[] options)
    {
        var clicked = GUILayout.Button(label, ModernStyle(WrightTycoonUiTheme.PrimaryButtonStyleName), options);
        RecordFactoryControl(id);
        return clicked;
    }

    private bool PlannerDangerButton(string id, string label, params GUILayoutOption[] options)
    {
        var clicked = DrawModernDangerButton(label, options);
        RecordFactoryControl(id);
        return clicked;
    }

    private int DrawPlannerTabs(string id, int selected, string[] labels, float fixedWidth = 0)
    {
        var next = Mathf.Clamp(selected, 0, labels.Length - 1);
        for (var index = 0; index < labels.Length; index++)
        {
            var chosen = fixedWidth > 0
                ? GUILayout.Toggle(index == selected, labels[index], GUI.skin.button, GUILayout.Width(fixedWidth), GUILayout.Height(30f))
                : GUILayout.Toggle(index == selected, labels[index], GUI.skin.button, GUILayout.ExpandWidth(true), GUILayout.Height(32f));
            RecordFactoryControl(id + ":" + index);
            if (chosen && index != selected) next = index;
        }
        return next;
    }

    private int DrawFactoryChoice(string id, string label, int selected, string[] choices)
    {
        if (choices.Length == 0) { GUILayout.Label(label + ": no available options"); return 0; }
        selected = Mathf.Clamp(selected, 0, choices.Length - 1);
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(140f));
        if (PlannerButton(id + ".previous", "◀", GUILayout.Width(34f), GUILayout.Height(29f))) selected = (selected + choices.Length - 1) % choices.Length;
        if (PlannerButton(id + ".open", choices[selected] + "  ▾", GUILayout.Height(29f)))
            _factoryChoiceOpen = _factoryChoiceOpen == id ? string.Empty : id;
        if (PlannerButton(id + ".next", "▶", GUILayout.Width(34f), GUILayout.Height(29f))) selected = (selected + 1) % choices.Length;
        GUILayout.EndHorizontal();
        if (_factoryChoiceOpen == id)
        {
            _factoryChoiceScroll = GUILayout.BeginScrollView(_factoryChoiceScroll, GUILayout.Height(Mathf.Min(180f, choices.Length * 32f + 12f)));
            for (var index = 0; index < choices.Length; index++)
                if (PlannerButton(id + ".option:" + index, choices[index], GUILayout.Height(28f)))
                { selected = index; _factoryChoiceOpen = string.Empty; }
            GUILayout.EndScrollView();
        }
        return selected;
    }
}
