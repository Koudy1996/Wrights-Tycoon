using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Game.Save;
using WrightTycoon.Game.UI;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private int _modernAirframeDesignerStage;
    private int _selectedIntroCampaignIndex = -1;
    private Vector2 _introMenuScroll;

    private static GUIStyle ModernStyle(string name) => WrightTycoonUiTheme.GetStyle(name);

    private void DrawModernIntroScreen(Vector2 virtualScreenSize)
    {
        DrawIntroBackground(virtualScreenSize);

        var panelWidth = Mathf.Min(700f, virtualScreenSize.x - 32f);
        var panelHeight = Mathf.Min(680f, virtualScreenSize.y - 32f);
        var panelY = Mathf.Clamp(
            ((virtualScreenSize.y - panelHeight) * 0.5f) + 52f,
            16f,
            virtualScreenSize.y - panelHeight - 16f);
        var panelRect = new Rect(
            (virtualScreenSize.x - panelWidth) * 0.5f,
            panelY,
            panelWidth,
            panelHeight);

        GameSaveSlotEntry? continueEntry = null;
        var hasContinueSave = _savePersistence is not null
            && _savePersistence.TryGetContinueEntry(out continueEntry)
            && continueEntry is not null;

        DrawIntroLogo(virtualScreenSize, panelRect);
        GUILayout.BeginArea(panelRect, ModernStyle(WrightTycoonUiTheme.PageHeaderStyleName));
        _introMenuScroll = GUILayout.BeginScrollView(_introMenuScroll);

        GUILayout.Label("MAIN MENU", ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
        GUILayout.Label("Wright Tycoon", ModernStyle(WrightTycoonUiTheme.PageTitleStyleName));
        GUILayout.Label(
            "Continue your company or begin a new aviation era.",
            ModernStyle(WrightTycoonUiTheme.PageSubtitleStyleName));

        GUILayout.Space(8f);
        DrawIntroContinueCard(hasContinueSave ? continueEntry : null);

        DrawModernSectionHeader("New Game", "Choose a campaign before naming your company.");
        var campaignCardWidth = Mathf.Max(220f, (panelWidth - 58f) * 0.5f);
        GUILayout.BeginHorizontal();
        DrawIntroCampaignCard(
            campaignIndex: 0,
            title: "Historical Campaign",
            badge: "1946 START",
            description: "Begin after the war. Technologies, aircraft markets and historical events unlock over time.",
            width: campaignCardWidth);
        GUILayout.Space(6f);
        DrawIntroCampaignCard(
            campaignIndex: 1,
            title: "Modern Campaign",
            badge: "2020 START",
            description: "Begin in the modern era with the contemporary technology timeline already available.",
            width: campaignCardWidth);
        GUILayout.EndHorizontal();

        if (_selectedIntroCampaignIndex >= 0)
        {
            DrawIntroNewCompanyCard();
        }

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (DrawModernSecondaryButton("Options", GUILayout.Height(34f)))
        {
            OpenOptionsMenu();
        }

        if (DrawModernSecondaryButton("Exit", GUILayout.Height(34f)))
        {
            Application.Quit();
        }

        GUILayout.EndHorizontal();

        if (!_initialized && !string.IsNullOrWhiteSpace(_initError))
        {
            GUILayout.Space(6f);
            DrawError("Initialization failed. Use Retry Initialization in gameplay view or fix project data paths.");
        }

        GUILayout.Space(8f);
        DrawBuildManifestSummary();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawIntroContinueCard(GameSaveSlotEntry? continueEntry)
    {
        BeginModernCard(continueEntry is not null);
        if (continueEntry is null)
        {
            GUILayout.Label("No saved company", ModernStyle(WrightTycoonUiTheme.CardHeaderStyleName));
            GUILayout.Label(
                "Start a new campaign below. Your most recently played company will appear here.",
                ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
        }
        else
        {
            var saveName = string.IsNullOrWhiteSpace(continueEntry.SlotName)
                ? continueEntry.CompanyName
                : continueEntry.SlotName;
            var saveType = string.Equals(continueEntry.Type, "autosave", StringComparison.OrdinalIgnoreCase)
                ? "AUTOSAVE"
                : "MANUAL SAVE";

            GUILayout.BeginHorizontal();
            GUILayout.Label(saveName, ModernStyle(WrightTycoonUiTheme.CardHeaderStyleName));
            GUILayout.FlexibleSpace();
            DrawModernBadge("LAST PLAYED", ModernUiTone.Success);
            GUILayout.EndHorizontal();
            GUILayout.Label(
                $"{continueEntry.CompanyName}  •  {saveType}  •  Saved {continueEntry.CreatedAtIso}",
                ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
        }

        GUILayout.Space(5f);
        GUILayout.BeginHorizontal();
        var previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && continueEntry is not null;
        if (DrawModernPrimaryButton("Continue", GUILayout.Height(36f)))
        {
            ContinueLastPlayedSave();
        }

        GUI.enabled = previousEnabled;
        if (DrawModernSecondaryButton("Load another save", GUILayout.Height(36f)))
        {
            _runtimeScreen = RuntimeScreen.LoadMenu;
        }

        GUILayout.EndHorizontal();
        EndModernCard();
    }

    private void DrawIntroCampaignCard(
        int campaignIndex,
        string title,
        string badge,
        string description,
        float width)
    {
        var selected = _selectedIntroCampaignIndex == campaignIndex;
        GUILayout.BeginVertical(
            ModernStyle(selected
                ? WrightTycoonUiTheme.SelectedCardStyleName
                : WrightTycoonUiTheme.CardStyleName),
            GUILayout.Width(width),
            GUILayout.MinHeight(142f));

        GUILayout.BeginHorizontal();
        GUILayout.Label(title, ModernStyle(WrightTycoonUiTheme.CardHeaderStyleName));
        GUILayout.FlexibleSpace();
        DrawModernBadge(badge, selected ? ModernUiTone.Accent : ModernUiTone.Neutral);
        GUILayout.EndHorizontal();
        GUILayout.Label(description, ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
        GUILayout.FlexibleSpace();

        var buttonLabel = selected ? "Selected" : "Choose campaign";
        if ((selected
                ? DrawModernPrimaryButton(buttonLabel, GUILayout.Height(32f))
                : DrawModernSecondaryButton(buttonLabel, GUILayout.Height(32f))))
        {
            _selectedIntroCampaignIndex = campaignIndex;
        }

        GUILayout.EndVertical();
    }

    private void DrawIntroNewCompanyCard()
    {
        var isHistoricalCampaign = _selectedIntroCampaignIndex == 0;
        BeginModernCard(selected: true);
        GUILayout.Label(
            isHistoricalCampaign ? "Start Historical Campaign" : "Start Modern Campaign",
            ModernStyle(WrightTycoonUiTheme.CardHeaderStyleName));
        _playerCompanyName = DrawText("Company Name", _playerCompanyName);

        var previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && !string.IsNullOrWhiteSpace(_playerCompanyName);
        if (DrawModernPrimaryButton(
                isHistoricalCampaign ? "Start in 1946" : "Start in 2020",
                GUILayout.Height(38f)))
        {
            if (isHistoricalCampaign)
            {
                StartNewGameFromMenu();
            }
            else
            {
                StartNewGameFromMenu(
                    new SimulationDate(ModernCampaignStartYear, 1),
                    "Modern 2020 campaign started.");
            }
        }

        GUI.enabled = previousEnabled;
        EndModernCard();
    }

    private static void DrawModernPageHeader(
        string title,
        string subtitle,
        string? badge = null,
        ModernUiTone badgeTone = ModernUiTone.Accent)
    {
        GUILayout.BeginVertical(ModernStyle(WrightTycoonUiTheme.PageHeaderStyleName));
        GUILayout.BeginHorizontal();
        GUILayout.Label(title, ModernStyle(WrightTycoonUiTheme.PageTitleStyleName));
        if (!string.IsNullOrWhiteSpace(badge))
        {
            GUILayout.FlexibleSpace();
            DrawModernBadge(badge, badgeTone);
        }

        GUILayout.EndHorizontal();
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            GUILayout.Label(subtitle, ModernStyle(WrightTycoonUiTheme.PageSubtitleStyleName));
        }

        GUILayout.EndVertical();
        GUILayout.Space(6f);
    }

    private static void DrawModernSectionHeader(string title, string? subtitle = null)
    {
        GUILayout.Space(5f);
        GUILayout.Label(title, ModernStyle(WrightTycoonUiTheme.SectionHeaderStyleName));
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            GUILayout.Label(subtitle, ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
        }
    }

    private static void BeginModernCard(bool selected = false)
    {
        GUILayout.BeginVertical(ModernStyle(
            selected
                ? WrightTycoonUiTheme.SelectedCardStyleName
                : WrightTycoonUiTheme.CardStyleName));
    }

    private static void EndModernCard()
    {
        GUILayout.EndVertical();
    }

    private static void DrawModernBadge(string text, ModernUiTone tone)
    {
        GUILayout.Label(text, ModernStyle(ResolveModernBadgeStyleName(tone)), GUILayout.ExpandWidth(false));
    }

    private static string ResolveModernBadgeStyleName(ModernUiTone tone) => tone switch
    {
        ModernUiTone.Success => WrightTycoonUiTheme.SuccessBadgeStyleName,
        ModernUiTone.Warning => WrightTycoonUiTheme.WarningBadgeStyleName,
        ModernUiTone.Danger => WrightTycoonUiTheme.DangerBadgeStyleName,
        ModernUiTone.Neutral => WrightTycoonUiTheme.NeutralBadgeStyleName,
        _ => WrightTycoonUiTheme.AccentBadgeStyleName,
    };

    private static bool DrawModernPrimaryButton(string label, params GUILayoutOption[] options)
    {
        return GUILayout.Button(label, ModernStyle(WrightTycoonUiTheme.PrimaryButtonStyleName), options);
    }

    private static bool DrawModernSecondaryButton(string label, params GUILayoutOption[] options)
    {
        return GUILayout.Button(label, ModernStyle(WrightTycoonUiTheme.SecondaryButtonStyleName), options);
    }

    private static bool DrawModernDangerButton(string label, params GUILayoutOption[] options)
    {
        return GUILayout.Button(label, ModernStyle(WrightTycoonUiTheme.DangerButtonStyleName), options);
    }

    private static void DrawModernKpi(
        string label,
        string value,
        string? delta = null,
        ModernUiTone deltaTone = ModernUiTone.Neutral)
    {
        GUILayout.BeginVertical(
            ModernStyle(WrightTycoonUiTheme.KpiCardStyleName),
            GUILayout.MinWidth(150f),
            GUILayout.ExpandWidth(true));
        GUILayout.Label(label, ModernStyle(WrightTycoonUiTheme.KpiLabelStyleName));
        GUILayout.Label(value, ModernStyle(WrightTycoonUiTheme.KpiValueStyleName));
        if (!string.IsNullOrWhiteSpace(delta))
        {
            DrawModernBadge(delta, deltaTone);
        }

        GUILayout.EndVertical();
    }

    private static void DrawModernKpiRow(params ModernKpiItem[] items)
    {
        if (items is null || items.Length == 0)
        {
            return;
        }

        const float gap = 6f;
        var hasDelta = items.Any(item => !string.IsNullOrWhiteSpace(item.Delta));
        var rowHeight = hasDelta ? 110f : 88f;
        var rowRect = GUILayoutUtility.GetRect(1f, rowHeight, GUILayout.ExpandWidth(true));
        var availableCardWidth = rowRect.width - (gap * (items.Length - 1));
        var cardWidth = Mathf.Max(1f, availableCardWidth / items.Length);
        var cardStyle = ModernStyle(WrightTycoonUiTheme.DesignerKpiCardStyleName);

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var cardRect = new Rect(
                rowRect.x + ((cardWidth + gap) * index),
                rowRect.y,
                cardWidth,
                rowHeight);
            GUI.Box(cardRect, GUIContent.none, cardStyle);

            const float horizontalPadding = 9f;
            var contentWidth = Mathf.Max(1f, cardRect.width - (horizontalPadding * 2f));
            var contentX = cardRect.x + horizontalPadding;
            var textX = contentX;
            var textWidth = contentWidth;
            if (item.Icon != ModernKpiIcon.None)
            {
                const float iconSize = 26f;
                const float iconTextGap = 8f;
                DrawModernKpiIcon(
                    new Rect(contentX, cardRect.y + 16f, iconSize, iconSize),
                    item.Icon,
                    ResolveModernKpiIconColor(item.Icon));
                textX += iconSize + iconTextGap;
                textWidth = Mathf.Max(1f, contentWidth - iconSize - iconTextGap);
            }

            var labelStyle = CreateFittedSingleLineKpiStyle(
                WrightTycoonUiTheme.DesignerKpiLabelStyleName,
                item.Label,
                textWidth,
                8);
            var valueStyle = CreateFittedSingleLineKpiStyle(
                WrightTycoonUiTheme.DesignerKpiValueStyleName,
                item.Value,
                textWidth,
                12);

            GUI.Label(new Rect(textX, cardRect.y + 10f, textWidth, 18f), item.Label, labelStyle);
            GUI.Label(new Rect(textX, cardRect.y + 32f, textWidth, 34f), item.Value, valueStyle);

            if (!string.IsNullOrWhiteSpace(item.Delta))
            {
                var badgeStyle = new GUIStyle(ModernStyle(ResolveModernBadgeStyleName(item.DeltaTone)))
                {
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                };
                GUI.Label(
                    new Rect(textX, cardRect.y + 76f, textWidth, 24f),
                    item.Delta,
                    badgeStyle);
            }
        }
    }

    private static Color ResolveModernKpiIconColor(ModernKpiIcon icon) => icon switch
    {
        ModernKpiIcon.Power => new Color(0.48f, 0.70f, 0.98f, 1f),
        ModernKpiIcon.Reliability => new Color(0.43f, 0.81f, 0.53f, 1f),
        ModernKpiIcon.Weight => new Color(0.91f, 0.71f, 0.34f, 1f),
        ModernKpiIcon.PartsCost => new Color(0.68f, 0.57f, 0.82f, 1f),
        ModernKpiIcon.CompleteCost => new Color(0.79f, 0.63f, 0.88f, 1f),
        ModernKpiIcon.SpecificFuel => new Color(0.38f, 0.76f, 0.82f, 1f),
        ModernKpiIcon.Speed => new Color(0.48f, 0.70f, 0.98f, 1f),
        ModernKpiIcon.Range => new Color(0.35f, 0.78f, 0.70f, 1f),
        ModernKpiIcon.Payload => new Color(0.91f, 0.71f, 0.34f, 1f),
        ModernKpiIcon.Maneuverability => new Color(0.91f, 0.48f, 0.38f, 1f),
        _ => WrightTycoonUiTheme.TextMuted,
    };

    private static void DrawModernKpiIcon(Rect rect, ModernKpiIcon icon, Color color)
    {
        Vector2 Point(float x, float y) => new(rect.x + (rect.width * x), rect.y + (rect.height * y));

        switch (icon)
        {
            case ModernKpiIcon.Power:
                DrawModernIconPolyline(color, 2.2f,
                    Point(0.58f, 0.03f), Point(0.22f, 0.52f), Point(0.48f, 0.52f),
                    Point(0.36f, 0.97f), Point(0.79f, 0.39f), Point(0.53f, 0.39f),
                    Point(0.58f, 0.03f));
                break;
            case ModernKpiIcon.Reliability:
                DrawModernIconPolyline(color, 2.1f,
                    Point(0.50f, 0.05f), Point(0.85f, 0.20f), Point(0.82f, 0.58f),
                    Point(0.68f, 0.81f), Point(0.50f, 0.95f), Point(0.32f, 0.81f),
                    Point(0.18f, 0.58f), Point(0.15f, 0.20f), Point(0.50f, 0.05f));
                DrawModernIconPolyline(color, 2f,
                    Point(0.31f, 0.50f), Point(0.45f, 0.64f), Point(0.70f, 0.36f));
                break;
            case ModernKpiIcon.Weight:
                DrawModernIconArc(rect, color, 2f, 0.50f, 0.30f, 0.17f, 0.18f, 180f, 360f, 8);
                DrawModernIconPolyline(color, 2.1f,
                    Point(0.34f, 0.30f), Point(0.22f, 0.86f), Point(0.78f, 0.86f),
                    Point(0.66f, 0.30f), Point(0.34f, 0.30f));
                break;
            case ModernKpiIcon.PartsCost:
                DrawModernIconCircle(rect, color, 2f, 0.50f, 0.50f, 0.23f, 12);
                DrawModernIconCircle(rect, color, 2f, 0.50f, 0.50f, 0.09f, 10);
                for (var index = 0; index < 8; index++)
                {
                    var angle = index * Mathf.PI * 0.25f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var center = Point(0.50f, 0.50f);
                    DrawLine(center + (direction * rect.width * 0.27f), center + (direction * rect.width * 0.42f), color, 2.4f);
                }
                break;
            case ModernKpiIcon.CompleteCost:
                DrawModernIconArc(rect, color, 2f, 0.50f, 0.23f, 0.31f, 0.13f, 0f, 360f, 16);
                DrawModernIconArc(rect, color, 2f, 0.50f, 0.45f, 0.31f, 0.13f, 0f, 180f, 8);
                DrawModernIconArc(rect, color, 2f, 0.50f, 0.67f, 0.31f, 0.13f, 0f, 180f, 8);
                DrawLine(Point(0.19f, 0.23f), Point(0.19f, 0.68f), color, 2f);
                DrawLine(Point(0.81f, 0.23f), Point(0.81f, 0.68f), color, 2f);
                break;
            case ModernKpiIcon.SpecificFuel:
                DrawModernIconPolyline(color, 2.1f,
                    Point(0.50f, 0.04f), Point(0.26f, 0.39f), Point(0.20f, 0.58f),
                    Point(0.25f, 0.77f), Point(0.36f, 0.90f), Point(0.50f, 0.95f),
                    Point(0.64f, 0.90f), Point(0.75f, 0.77f), Point(0.80f, 0.58f),
                    Point(0.74f, 0.39f), Point(0.50f, 0.04f));
                DrawLine(Point(0.58f, 0.75f), Point(0.68f, 0.62f), color, 1.7f);
                break;
            case ModernKpiIcon.Speed:
                DrawModernIconArc(rect, color, 2.1f, 0.50f, 0.62f, 0.39f, 0.39f, 180f, 360f, 12);
                DrawLine(Point(0.16f, 0.78f), Point(0.84f, 0.78f), color, 2.1f);
                DrawLine(Point(0.50f, 0.62f), Point(0.73f, 0.38f), color, 2.2f);
                DrawModernIconCircle(rect, color, 2f, 0.50f, 0.62f, 0.05f, 8);
                break;
            case ModernKpiIcon.Range:
                DrawModernIconCircle(rect, color, 2.1f, 0.50f, 0.38f, 0.30f, 14);
                DrawModernIconCircle(rect, color, 1.8f, 0.50f, 0.38f, 0.09f, 10);
                DrawModernIconPolyline(color, 2.1f,
                    Point(0.25f, 0.55f), Point(0.50f, 0.94f), Point(0.75f, 0.55f));
                break;
            case ModernKpiIcon.Payload:
                DrawModernIconPolyline(color, 2.1f,
                    Point(0.14f, 0.30f), Point(0.50f, 0.12f), Point(0.86f, 0.30f),
                    Point(0.86f, 0.79f), Point(0.50f, 0.95f), Point(0.14f, 0.79f),
                    Point(0.14f, 0.30f), Point(0.50f, 0.48f), Point(0.86f, 0.30f));
                DrawLine(Point(0.50f, 0.48f), Point(0.50f, 0.95f), color, 2.1f);
                DrawLine(Point(0.32f, 0.21f), Point(0.68f, 0.39f), color, 1.8f);
                break;
            case ModernKpiIcon.Maneuverability:
                DrawModernIconPolyline(color, 2.1f,
                    Point(0.08f, 0.58f), Point(0.36f, 0.47f), Point(0.48f, 0.10f),
                    Point(0.58f, 0.07f), Point(0.59f, 0.43f), Point(0.87f, 0.54f),
                    Point(0.88f, 0.64f), Point(0.58f, 0.59f), Point(0.45f, 0.90f),
                    Point(0.36f, 0.88f), Point(0.39f, 0.57f), Point(0.08f, 0.66f),
                    Point(0.08f, 0.58f));
                break;
        }
    }

    private static void DrawModernIconPolyline(Color color, float thickness, params Vector2[] points)
    {
        for (var index = 1; index < points.Length; index++)
        {
            DrawLine(points[index - 1], points[index], color, thickness);
        }
    }

    private static void DrawModernIconCircle(
        Rect rect,
        Color color,
        float thickness,
        float centerX,
        float centerY,
        float radius,
        int segments)
    {
        DrawModernIconArc(rect, color, thickness, centerX, centerY, radius, radius, 0f, 360f, segments);
    }

    private static void DrawModernIconArc(
        Rect rect,
        Color color,
        float thickness,
        float centerX,
        float centerY,
        float radiusX,
        float radiusY,
        float startDegrees,
        float endDegrees,
        int segments)
    {
        Vector2 PointAt(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(
                rect.x + (rect.width * (centerX + (Mathf.Cos(radians) * radiusX))),
                rect.y + (rect.height * (centerY + (Mathf.Sin(radians) * radiusY))));
        }

        var previous = PointAt(startDegrees);
        for (var index = 1; index <= segments; index++)
        {
            var current = PointAt(Mathf.Lerp(startDegrees, endDegrees, index / (float)segments));
            DrawLine(previous, current, color, thickness);
            previous = current;
        }
    }

    private static void DrawModernKpiGrid(int columns, params ModernKpiItem[] items)
    {
        if (items is null || items.Length == 0)
        {
            return;
        }

        var safeColumns = Mathf.Clamp(columns, 1, items.Length);
        for (var startIndex = 0; startIndex < items.Length; startIndex += safeColumns)
        {
            if (startIndex > 0)
            {
                GUILayout.Space(6f);
            }

            DrawModernKpiRow(items.Skip(startIndex).Take(safeColumns).ToArray());
        }
    }

    private static GUIStyle CreateFittedSingleLineKpiStyle(
        string styleName,
        string text,
        float availableWidth,
        int minimumFontSize)
    {
        var style = new GUIStyle(ModernStyle(styleName))
        {
            clipping = TextClipping.Clip,
            wordWrap = false,
        };
        var content = new GUIContent(text ?? string.Empty);
        while (style.fontSize > minimumFontSize && style.CalcSize(content).x > availableWidth)
        {
            style.fontSize--;
        }

        return style;
    }

    private static void DrawModernInfoRow(string label, string value, float labelWidth = 210f)
    {
        GUILayout.BeginHorizontal(ModernStyle(WrightTycoonUiTheme.CardStyleName));
        GUILayout.Label(label, ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName), GUILayout.Width(labelWidth));
        GUILayout.Label(value);
        GUILayout.EndHorizontal();
    }

    private static void DrawModernEngineKpis(
        Engine currentEngine,
        Engine referenceEngine,
        bool hasReference)
    {
        var current = currentEngine.Performance;
        var reference = referenceEngine.Performance;
        var currentOutput = ResolveEngineOutputAtSeaLevel(currentEngine);
        var referenceOutput = ResolveEngineOutputAtSeaLevel(referenceEngine);
        var outputUnit = current.OutputKind == EngineOutputKind.PowerKilowatts ? "kW" : "N";
        var currentIsJet = IsJetFamily(currentEngine.Definition.EngineType);
        var referenceIsJet = IsJetFamily(referenceEngine.Definition.EngineType);
        var currentSpecificFuelConsumption = ResolveEngineSpecificFuelConsumption(currentEngine);
        var referenceSpecificFuelConsumption = ResolveEngineSpecificFuelConsumption(referenceEngine);
        var specificFuelUnit = currentIsJet ? "L/N" : "L/kWh";
        var outputTone = ModernUiTone.Neutral;
        var reliabilityTone = ModernUiTone.Neutral;
        var weightTone = ModernUiTone.Neutral;
        var costTone = ModernUiTone.Neutral;
        var completeCostTone = ModernUiTone.Neutral;
        var specificFuelTone = ModernUiTone.Neutral;
        var outputDelta = hasReference
            ? BuildModernDelta(currentOutput, referenceOutput, false, out outputTone)
            : null;
        var reliabilityDelta = hasReference
            ? BuildModernDelta(current.Reliability, reference.Reliability, false, out reliabilityTone)
            : null;
        var weightDelta = hasReference
            ? BuildModernDelta(current.Weight.Kilograms, reference.Weight.Kilograms, true, out weightTone)
            : null;
        var costDelta = hasReference
            ? BuildModernDelta(current.Cost, reference.Cost, true, out costTone)
            : null;
        var completeCostDelta = hasReference
            ? BuildModernDelta(current.CompleteProductionCost, reference.CompleteProductionCost, true, out completeCostTone)
            : null;
        var specificFuelDelta = hasReference && currentIsJet == referenceIsJet
            ? BuildModernDelta(currentSpecificFuelConsumption, referenceSpecificFuelConsumption, true, out specificFuelTone)
            : null;

        DrawModernKpiGrid(
            3,
            new ModernKpiItem(
                current.OutputKind == EngineOutputKind.PowerKilowatts ? "POWER @ 0m" : "THRUST @ 0m",
                $"{FormatUiNumber(currentOutput, "0")} {outputUnit}",
                outputDelta,
                outputTone,
                ModernKpiIcon.Power),
            new ModernKpiItem(
                "RELIABILITY",
                $"{FormatUiNumber(current.Reliability * 100d, "0.0")}%",
                reliabilityDelta,
                reliabilityTone,
                ModernKpiIcon.Reliability),
            new ModernKpiItem(
                "WEIGHT",
                $"{FormatUiNumber(current.Weight.Kilograms, "0")} kg",
                weightDelta,
                weightTone,
                ModernKpiIcon.Weight),
            new ModernKpiItem(
                "PARTS COST",
                FormatCurrency(current.Cost),
                costDelta,
                costTone,
                ModernKpiIcon.PartsCost),
            new ModernKpiItem(
                "COMPLETE COST",
                FormatCurrency(current.CompleteProductionCost),
                completeCostDelta,
                completeCostTone,
                ModernKpiIcon.CompleteCost),
            new ModernKpiItem(
                "SPECIFIC FUEL",
                $"{FormatUiNumber(currentSpecificFuelConsumption, "0.0000")} {specificFuelUnit}",
                specificFuelDelta,
                specificFuelTone,
                ModernKpiIcon.SpecificFuel));
    }

    private static double ResolveEngineSpecificFuelConsumption(Engine engine)
    {
        if (TryGetEngineAltitudePerformanceValues(engine, 0d, out var output, out var fuelBurnKilogramsPerHour))
        {
            return CalculateFuelPerOutputMetric(
                output,
                fuelBurnKilogramsPerHour,
                IsJetFamily(engine.Definition.EngineType));
        }

        var points = engine.Performance.AltitudePerformance;
        if (points.Count == 0)
        {
            return 0d;
        }

        return CalculateFuelPerOutputMetric(
            points[0].Output,
            points[0].FuelConsumptionKilogramsPerHour,
            IsJetFamily(engine.Definition.EngineType));
    }

    private static string BuildModernDelta(
        double current,
        double reference,
        bool lowerIsBetter,
        out ModernUiTone tone)
    {
        if (Math.Abs(reference) <= 1e-9)
        {
            tone = ModernUiTone.Neutral;
            return "no baseline";
        }

        var deltaPercent = ((current - reference) / Math.Abs(reference)) * 100d;
        var favorable = lowerIsBetter ? deltaPercent <= 0d : deltaPercent >= 0d;
        tone = Math.Abs(deltaPercent) < 0.05d
            ? ModernUiTone.Neutral
            : favorable
                ? ModernUiTone.Success
                : ModernUiTone.Danger;
        return $"{(deltaPercent >= 0d ? "+" : string.Empty)}{FormatUiNumber(deltaPercent, "0.0")}%";
    }

    private static void DrawModernEnginePowerChart(
        IReadOnlyList<EngineAltitudePerformance> currentPoints,
        IReadOnlyList<EngineAltitudePerformance>? referencePoints,
        string currentLabel,
        string referenceLabel)
    {
        if (currentPoints.Count == 0)
        {
            return;
        }

        DrawModernSectionHeader("Power by Altitude", "A live curve keeps the outcome next to the parameters that produce it.");
        var chartRect = GUILayoutUtility.GetRect(520f, 250f, GUILayout.ExpandWidth(true));
        DrawRect(chartRect, WrightTycoonUiTheme.Panel);
        var plotRect = new Rect(chartRect.x + 54f, chartRect.y + 34f, chartRect.width - 74f, chartRect.height - 70f);
        DrawRect(plotRect, new Color(0.035f, 0.07f, 0.105f, 1f));

        var allPoints = referencePoints is null
            ? currentPoints.ToArray()
            : currentPoints.Concat(referencePoints).ToArray();
        var maximumAltitude = Math.Max(allPoints.Max(point => point.AltitudeMeters), 1d);
        var maximumOutput = Math.Max(allPoints.Max(point => point.Output), 1d);

        for (var gridIndex = 0; gridIndex <= 4; gridIndex++)
        {
            var ratio = gridIndex / 4f;
            var y = plotRect.yMax - (plotRect.height * ratio);
            DrawLine(
                new Vector2(plotRect.x, y),
                new Vector2(plotRect.xMax, y),
                new Color(0.19f, 0.30f, 0.40f, 0.55f),
                1f);
            GUI.Label(
                new Rect(chartRect.x, y - 10f, 50f, 20f),
                FormatUiNumber(maximumOutput * ratio, "0"),
                ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
        }

        DrawModernChartLine(currentPoints, plotRect, maximumAltitude, maximumOutput, WrightTycoonUiTheme.Accent, 3f);
        if (referencePoints is not null && referencePoints.Count > 0)
        {
            DrawModernChartLine(referencePoints, plotRect, maximumAltitude, maximumOutput, WrightTycoonUiTheme.TextMuted, 2f);
        }

        GUI.Label(
            new Rect(plotRect.x, chartRect.yMax - 30f, plotRect.width, 24f),
            $"Altitude 0 — {FormatUiNumber(maximumAltitude, "0")} m",
            ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
        GUI.Label(
            new Rect(plotRect.x, chartRect.y + 6f, plotRect.width, 24f),
            referencePoints is null
                ? currentLabel
                : $"{currentLabel}  •  baseline: {referenceLabel}",
            ModernStyle(WrightTycoonUiTheme.MutedLabelStyleName));
    }

    private static void DrawModernChartLine(
        IReadOnlyList<EngineAltitudePerformance> points,
        Rect plotRect,
        double maximumAltitude,
        double maximumOutput,
        Color color,
        float thickness)
    {
        if (points.Count < 2)
        {
            return;
        }

        var ordered = points.OrderBy(point => point.AltitudeMeters).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            Vector2 Map(EngineAltitudePerformance point) => new(
                plotRect.x + ((float)(point.AltitudeMeters / maximumAltitude) * plotRect.width),
                plotRect.yMax - ((float)(point.Output / maximumOutput) * plotRect.height));
            DrawLine(Map(ordered[index - 1]), Map(ordered[index]), color, thickness);
        }
    }

    private static void DrawModernWorkflowSteps(
        string[] labels,
        int activeIndex,
        int completedThroughIndex,
        bool failed = false)
    {
        GUILayout.BeginHorizontal();
        for (var index = 0; index < labels.Length; index++)
        {
            var styleName = index <= completedThroughIndex
                ? WrightTycoonUiTheme.StepDoneStyleName
                : index == activeIndex
                    ? failed
                        ? WrightTycoonUiTheme.DangerBadgeStyleName
                        : WrightTycoonUiTheme.StepActiveStyleName
                    : WrightTycoonUiTheme.StepIdleStyleName;
            var prefix = index <= completedThroughIndex ? "✓" : FormatUiInteger(index + 1);
            GUILayout.Label(
                $"{prefix}  {labels[index]}",
                ModernStyle(styleName),
                GUILayout.ExpandWidth(true),
                GUILayout.Height(34f));
        }

        GUILayout.EndHorizontal();
    }

    private void DrawGovernmentWorkflowHeader()
    {
        if (_governmentContractBoard is null || _productionFactory is null)
        {
            return;
        }

        var activeContract = _governmentContractBoard.ActiveContract;
        var activeDelivery = _governmentContractBoard.ActiveDeliveryContract;
        var latestEvaluation = _governmentContractBoard.EvaluatedContracts.Count == 0
            ? null
            : _governmentContractBoard.EvaluatedContracts[^1];
        var latestClosedDelivery = _governmentContractBoard.ClosedDeliveryContracts.Count == 0
            ? null
            : _governmentContractBoard.ClosedDeliveryContracts[^1];

        var activeStep = 0;
        var completedThrough = -1;
        var status = "NO ACTIVE OFFER";
        var tone = ModernUiTone.Neutral;
        var failed = false;

        if (activeDelivery is not null)
        {
            activeStep = 3;
            completedThrough = 2;
            status = activeDelivery.RemainingUnits > 0 ? "DELIVERY ACTIVE" : "DELIVERY COMPLETE";
            tone = activeDelivery.RemainingUnits > 0 ? ModernUiTone.Accent : ModernUiTone.Success;
        }
        else if (activeContract?.Status == GovernmentContractStatus.Accepted)
        {
            activeStep = 2;
            completedThrough = 1;
            status = "AWAITING EVALUATION";
            tone = ModernUiTone.Warning;
        }
        else if (activeContract?.Status == GovernmentContractStatus.Available)
        {
            activeStep = 0;
            status = "OFFER AVAILABLE";
            tone = ModernUiTone.Accent;
        }
        else if (latestClosedDelivery is not null)
        {
            activeStep = 3;
            completedThrough = 3;
            status = "DELIVERY CLOSED";
            tone = ModernUiTone.Success;
        }
        else if (latestEvaluation?.EvaluationResult is not null)
        {
            activeStep = 2;
            completedThrough = 1;
            failed = !latestEvaluation.EvaluationResult.IsSuccess;
            status = latestEvaluation.EvaluationResult.IsSuccess ? "EVALUATION PASSED" : "EVALUATION FAILED";
            tone = failed ? ModernUiTone.Danger : ModernUiTone.Success;
        }

        DrawModernPageHeader(
            "Government Contracts",
            "Review the offer, submit an approved design, await evaluation, then fulfil the resulting delivery contract.",
            status,
            tone);
        DrawModernWorkflowSteps(
            new[] { "Offer", "Accepted", "Evaluation", "Delivery" },
            activeStep,
            completedThrough,
            failed);

        GUILayout.Space(6f);
        BeginModernCard();
        GUILayout.Label("NEXT ACTION", ModernStyle(WrightTycoonUiTheme.CardHeaderStyleName));
        GUILayout.Label(BuildGovernmentNextActionText(activeContract, activeDelivery));
        EndModernCard();
    }

    private string BuildGovernmentNextActionText(
        GovernmentContract? activeContract,
        GovernmentDeliveryContract? activeDelivery)
    {
        if (activeDelivery is not null && _productionFactory is not null)
        {
            var available = _productionFactory.GetWarehouseUnits(activeDelivery.AircraftDesignId);
            if (available <= 0)
            {
                return $"Build {FormatUiInteger(activeDelivery.RemainingUnits)} more aircraft for this delivery contract.";
            }

            var deliverable = System.Math.Min(available, activeDelivery.RemainingUnits);
            return $"Deliver {FormatUiInteger(deliverable)} ready aircraft from the warehouse.";
        }

        if (activeContract?.Status == GovernmentContractStatus.Available)
        {
            return $"Select an approved design and submit it before {FormatSimulationDate(activeContract.AvailabilityExpiresOn)}.";
        }

        if (activeContract?.Status == GovernmentContractStatus.Accepted)
        {
            return activeContract.EvaluationDueOn.HasValue
                ? $"The selected design will be evaluated automatically in {FormatSimulationDate(activeContract.EvaluationDueOn.Value)}."
                : "The selected design is waiting for government evaluation.";
        }

        return "No action is required. A new offer is generated in January when no offer is active.";
    }

    private static string HumanizeUiToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        var builder = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index > 0
                && char.IsUpper(character)
                && !char.IsUpper(value[index - 1])
                && value[index - 1] != ' ')
            {
                builder.Append(' ');
            }

            builder.Append(character == '_' ? ' ' : character);
        }

        return builder.ToString();
    }

    private static ModernUiTone ResolveModernStatusTone(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return ModernUiTone.Neutral;
        }

        if (status.Contains("approved", StringComparison.OrdinalIgnoreCase)
            || status.Contains("completed", StringComparison.OrdinalIgnoreCase)
            || status.Contains("ready", StringComparison.OrdinalIgnoreCase)
            || status.Contains("eligible", StringComparison.OrdinalIgnoreCase))
        {
            return ModernUiTone.Success;
        }

        if (status.Contains("progress", StringComparison.OrdinalIgnoreCase)
            || status.Contains("pending", StringComparison.OrdinalIgnoreCase)
            || status.Contains("waiting", StringComparison.OrdinalIgnoreCase))
        {
            return ModernUiTone.Warning;
        }

        if (status.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || status.Contains("ineligible", StringComparison.OrdinalIgnoreCase)
            || status.Contains("locked", StringComparison.OrdinalIgnoreCase))
        {
            return ModernUiTone.Danger;
        }

        return ModernUiTone.Accent;
    }

    private enum ModernUiTone
    {
        Accent = 0,
        Success = 1,
        Warning = 2,
        Danger = 3,
        Neutral = 4,
    }

    private enum ModernKpiIcon
    {
        None = 0,
        Power = 1,
        Reliability = 2,
        Weight = 3,
        PartsCost = 4,
        CompleteCost = 5,
        SpecificFuel = 6,
        Speed = 7,
        Range = 8,
        Payload = 9,
        Maneuverability = 10,
    }

    private readonly struct ModernKpiItem
    {
        public ModernKpiItem(
            string label,
            string value,
            string? delta = null,
            ModernUiTone deltaTone = ModernUiTone.Neutral,
            ModernKpiIcon icon = ModernKpiIcon.None)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            Delta = delta;
            DeltaTone = deltaTone;
            Icon = icon;
        }

        public string Label { get; }

        public string Value { get; }

        public string? Delta { get; }

        public ModernUiTone DeltaTone { get; }

        public ModernKpiIcon Icon { get; }
    }
}
