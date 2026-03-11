using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using WrightTycoon.Core.Balance;

namespace WrightTycoon.Core.Research;

public sealed class ResearchBoard
{
    private readonly Dictionary<string, ResearchProject> _projects = new(StringComparer.Ordinal);
    private readonly Dictionary<int, double> _bookedResearchCostByMonth = new();

    public IReadOnlyList<ResearchProject> GetProjectsByStatus(ResearchStatus status)
    {
        return _projects.Values
            .Where(project => project.Status == status)
            .OrderBy(project => project.SubjectType)
            .ThenBy(project => project.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<ResearchProject> GetAllProjects()
    {
        return _projects.Values
            .OrderBy(project => project.SubjectType)
            .ThenBy(project => project.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ResearchProject UpsertProposal(
        ResearchSubjectType subjectType,
        string subjectId,
        string displayName,
        ResearchQuote quote)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            throw new ArgumentException("Subject id cannot be empty.", nameof(subjectId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        }

        quote.Validate();

        var normalizedSubjectId = NormalizeSubjectId(subjectId);
        var researchId = BuildResearchId(subjectType, normalizedSubjectId);

        if (_projects.TryGetValue(researchId, out var existing))
        {
            existing.UpdateProposal(displayName, quote);
            return existing;
        }

        var project = new ResearchProject(researchId, subjectType, normalizedSubjectId, displayName, quote);
        _projects.Add(researchId, project);
        return project;
    }

    public bool TryApproveProposal(
        ResearchSubjectType subjectType,
        string subjectId,
        int expenseMonthIndex,
        out ResearchProject? approvedProject,
        out string message)
    {
        if (expenseMonthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expenseMonthIndex), "Expense month index must be >= 0.");
        }

        var normalizedSubjectId = NormalizeSubjectId(subjectId);
        var researchId = BuildResearchId(subjectType, normalizedSubjectId);
        if (!_projects.TryGetValue(researchId, out var project))
        {
            approvedProject = null;
            message = $"Research proposal '{researchId}' was not found.";
            return false;
        }

        var speedBonusApplied = false;
        if (expenseMonthIndex <= ResearchBalance.FirstYearApprovalSpeedBonusWindowMonths)
        {
            speedBonusApplied = project.TryApplyApprovalSpeedBonus(ResearchBalance.FirstYearApprovalSpeedBonusMonths);
        }

        if (!project.TryApprove(expenseMonthIndex, out var bookedExpense))
        {
            approvedProject = project;
            message = $"Research '{project.DisplayName}' is already {project.Status}.";
            return false;
        }

        BookResearchExpense(expenseMonthIndex, bookedExpense);
        approvedProject = project;
        var speedBonusSuffix = speedBonusApplied ? " First-year speed bonus applied." : string.Empty;
        message =
            $"Approved '{project.DisplayName}'. Cost: {FormatUiNumber(bookedExpense, "0.00")}, duration: {FormatUiInteger(project.ResearchTimeMonths)} months.{speedBonusSuffix}";
        return true;
    }

    public int AdvanceMonth(int monthIndex)
    {
        if (monthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthIndex), "Month index must be >= 0.");
        }

        var completedCount = 0;
        foreach (var project in _projects.Values)
        {
            if (project.AdvanceMonth(monthIndex))
            {
                completedCount++;
            }
        }

        return completedCount;
    }

    public bool IsCompleted(ResearchSubjectType subjectType, string subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return false;
        }

        var researchId = BuildResearchId(subjectType, NormalizeSubjectId(subjectId));
        return _projects.TryGetValue(researchId, out var project) && project.IsCompleted;
    }

    public double GetBookedResearchExpenseForMonth(int monthIndex)
    {
        if (monthIndex < 0)
        {
            return 0d;
        }

        return _bookedResearchCostByMonth.TryGetValue(monthIndex, out var value)
            ? value
            : 0d;
    }

    public ResearchBoardSnapshot CreateSnapshot()
    {
        var snapshot = new ResearchBoardSnapshot();

        foreach (var project in _projects.Values
                     .OrderBy(project => project.SubjectType)
                     .ThenBy(project => project.ResearchId, StringComparer.Ordinal))
        {
            snapshot.Projects.Add(project.CreateSnapshot());
        }

        foreach (var entry in _bookedResearchCostByMonth.OrderBy(entry => entry.Key))
        {
            snapshot.BookedExpensesByMonth.Add(new ResearchExpenseSnapshot
            {
                MonthIndex = entry.Key,
                Expense = entry.Value,
            });
        }

        return snapshot;
    }

    public void RestoreSnapshot(ResearchBoardSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        _projects.Clear();
        _bookedResearchCostByMonth.Clear();

        if (snapshot.Projects is not null)
        {
            for (var index = 0; index < snapshot.Projects.Count; index++)
            {
                var restoredProject = ResearchProject.RestoreSnapshot(snapshot.Projects[index]);
                _projects[restoredProject.ResearchId] = restoredProject;
            }
        }

        if (snapshot.BookedExpensesByMonth is not null)
        {
            for (var index = 0; index < snapshot.BookedExpensesByMonth.Count; index++)
            {
                var item = snapshot.BookedExpensesByMonth[index];
                if (item.MonthIndex < 0)
                {
                    continue;
                }

                if (item.Expense <= 0d)
                {
                    continue;
                }

                _bookedResearchCostByMonth[item.MonthIndex] = item.Expense;
            }
        }
    }

    public static string BuildResearchId(ResearchSubjectType subjectType, string subjectId)
    {
        var normalizedSubjectId = NormalizeSubjectId(subjectId);
        var typePrefix = subjectType switch
        {
            ResearchSubjectType.Engine => "engine",
            ResearchSubjectType.AircraftFamily => "family",
            ResearchSubjectType.AircraftType => "type",
            _ => "unknown",
        };

        return $"{typePrefix}:{normalizedSubjectId}";
    }


    private static readonly CultureInfo UiNumberCulture = CreateUiNumberCulture();

    private static CultureInfo CreateUiNumberCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = " ";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSizes = new[] { 3 };
        return culture;
    }

    private static string FormatUiNumber(double value, string format) =>
        value.ToString($"#,##{format}", UiNumberCulture);

    private static string FormatUiInteger(int value) =>
        value.ToString("#,##0", UiNumberCulture);
    private static string NormalizeSubjectId(string subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return "unknown";
        }

        return subjectId.Trim().ToLowerInvariant();
    }

    private void BookResearchExpense(int monthIndex, double expense)
    {
        if (expense <= 0d)
        {
            return;
        }

        if (_bookedResearchCostByMonth.TryGetValue(monthIndex, out var existing))
        {
            _bookedResearchCostByMonth[monthIndex] = existing + expense;
            return;
        }

        _bookedResearchCostByMonth[monthIndex] = expense;
    }
}
