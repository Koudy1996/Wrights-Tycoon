using System;
using UnityEngine;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Research;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    // Quote months already include the selected modifier points and any facility effects.
    // The approval-only first-year bonus is applied here exactly once, as in ResearchBoard.
    private static int CalculateResearchApprovalMonths(int quotedMonths, int bookingMonthIndex)
    {
        var bonusMonths = bookingMonthIndex <= ResearchBalance.FirstYearApprovalSpeedBonusWindowMonths
            ? Math.Max(0, ResearchBalance.FirstYearApprovalSpeedBonusMonths)
            : 0;
        return Math.Max(1, quotedMonths - bonusMonths);
    }

    private void DrawResearchApprovalTimeRow(ResearchProject project)
    {
        var quotedMonths = project.ResearchTimeMonths;
        var points = GetResearchModifierPoints(project.SubjectType, project.SubjectId);
        if (TryBuildResearchQuote(project, points, null, out _, out var quote))
        {
            quotedMonths = quote.ResearchTimeMonths;
        }

        DrawResearchApprovalTimeRow(quotedMonths);
    }

    private void DrawResearchApprovalTimeRow(int quotedMonths, string label = "Engineering Time (months)")
    {
        var months = CalculateResearchApprovalMonths(quotedMonths, _elapsedMonths + 1);
        var standardLabel = GUI.skin.label;
        var baseFontSize = standardLabel.fontSize > 0
            ? standardLabel.fontSize
            : (standardLabel.font != null ? standardLabel.font.fontSize : GUI.skin.font != null ? GUI.skin.font.fontSize : 14);
        var resultStyle = new GUIStyle(standardLabel)
        {
            fontSize = Math.Max(12, baseFontSize) * 2,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
        };
        var labelStyle = new GUIStyle(standardLabel) { alignment = TextAnchor.MiddleLeft };
        var standardHeight = standardLabel.CalcHeight(new GUIContent("Engineering Time (months)"), 240f)
            + GUI.skin.box.padding.vertical;
        var resultHeight = CalculateResearchApprovalRowHeight(standardHeight);

        GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.MinHeight(resultHeight));
        GUILayout.Label(label, labelStyle, GUILayout.Width(240f), GUILayout.MinHeight(resultHeight - GUI.skin.box.padding.vertical));
        GUILayout.Label(FormatUiInteger(months), resultStyle, GUILayout.MinHeight(resultHeight - GUI.skin.box.padding.vertical));
        GUILayout.EndHorizontal();
    }

    private static float CalculateResearchApprovalRowHeight(float standardRowHeight) =>
        Math.Max(1f, standardRowHeight) * 2f;
}
