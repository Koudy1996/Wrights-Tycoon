using System;

namespace WrightTycoon.Core.Research;

public sealed class ResearchProject
{
    public ResearchProject(
        string researchId,
        ResearchSubjectType subjectType,
        string subjectId,
        string displayName,
        ResearchQuote quote)
    {
        if (string.IsNullOrWhiteSpace(researchId))
        {
            throw new ArgumentException("Research id cannot be empty.", nameof(researchId));
        }

        if (string.IsNullOrWhiteSpace(subjectId))
        {
            throw new ArgumentException("Subject id cannot be empty.", nameof(subjectId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        }

        quote.Validate();

        ResearchId = researchId.Trim();
        SubjectType = subjectType;
        SubjectId = subjectId.Trim();
        DisplayName = displayName.Trim();
        ResearchCost = quote.ResearchCost;
        ResearchTimeMonths = quote.ResearchTimeMonths;
        ComplexityNormalized = quote.ComplexityNormalized;
    }

    public string ResearchId { get; }

    public ResearchSubjectType SubjectType { get; }

    public string SubjectId { get; }

    public string DisplayName { get; private set; }

    public double ResearchCost { get; private set; }

    public int ResearchTimeMonths { get; private set; }

    public double ComplexityNormalized { get; private set; }

    public ResearchStatus Status { get; private set; } = ResearchStatus.Proposed;

    public int ProgressMonths { get; private set; }

    public int RemainingMonths => Status == ResearchStatus.Completed
        ? 0
        : System.Math.Max(ResearchTimeMonths - ProgressMonths, 0);

    public int? StartedMonthIndex { get; private set; }

    public int? CompletedMonthIndex { get; private set; }

    public bool IsCompleted => Status == ResearchStatus.Completed;

    public void UpdateProposal(string displayName, ResearchQuote quote)
    {
        if (Status != ResearchStatus.Proposed)
        {
            // Started/completed research keeps already-approved budget and schedule.
            return;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        }

        quote.Validate();
        DisplayName = displayName.Trim();
        ResearchCost = quote.ResearchCost;
        ResearchTimeMonths = quote.ResearchTimeMonths;
        ComplexityNormalized = quote.ComplexityNormalized;
    }

    public bool TryApprove(int expenseMonthIndex, out double bookedResearchExpense)
    {
        if (Status != ResearchStatus.Proposed)
        {
            bookedResearchExpense = 0d;
            return false;
        }

        if (expenseMonthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expenseMonthIndex), "Expense month index must be >= 0.");
        }

        Status = ResearchStatus.Ongoing;
        ProgressMonths = 0;
        StartedMonthIndex = expenseMonthIndex;
        CompletedMonthIndex = null;
        bookedResearchExpense = ResearchCost;
        return true;
    }

    public bool AdvanceMonth(int monthIndex)
    {
        if (Status != ResearchStatus.Ongoing)
        {
            return false;
        }

        if (monthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthIndex), "Month index must be >= 0.");
        }

        ProgressMonths = System.Math.Min(ResearchTimeMonths, ProgressMonths + 1);
        if (ProgressMonths < ResearchTimeMonths)
        {
            return false;
        }

        Status = ResearchStatus.Completed;
        CompletedMonthIndex = monthIndex;
        return true;
    }

    public ResearchProjectSnapshot CreateSnapshot()
    {
        return new ResearchProjectSnapshot
        {
            ResearchId = ResearchId,
            SubjectType = SubjectType,
            SubjectId = SubjectId,
            DisplayName = DisplayName,
            ResearchCost = ResearchCost,
            ResearchTimeMonths = ResearchTimeMonths,
            ComplexityNormalized = ComplexityNormalized,
            Status = Status,
            ProgressMonths = ProgressMonths,
            StartedMonthIndex = StartedMonthIndex,
            CompletedMonthIndex = CompletedMonthIndex,
        };
    }

    public static ResearchProject RestoreSnapshot(ResearchProjectSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var quote = new ResearchQuote(
            snapshot.ResearchCost,
            snapshot.ResearchTimeMonths,
            snapshot.ComplexityNormalized);
        var project = new ResearchProject(
            snapshot.ResearchId,
            snapshot.SubjectType,
            snapshot.SubjectId,
            snapshot.DisplayName,
            quote);

        project.Status = snapshot.Status;
        project.ProgressMonths = System.Math.Max(snapshot.ProgressMonths, 0);
        project.StartedMonthIndex = snapshot.StartedMonthIndex;
        project.CompletedMonthIndex = snapshot.CompletedMonthIndex;

        return project;
    }
}
