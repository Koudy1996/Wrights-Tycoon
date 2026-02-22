using System.Collections.Generic;

namespace WrightTycoon.Core.Research;

public sealed class ResearchBoardSnapshot
{
    public List<ResearchProjectSnapshot> Projects { get; set; } = new();

    public List<ResearchExpenseSnapshot> BookedExpensesByMonth { get; set; } = new();
}

public sealed class ResearchProjectSnapshot
{
    public string ResearchId { get; set; } = string.Empty;

    public ResearchSubjectType SubjectType { get; set; }

    public string SubjectId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public double ResearchCost { get; set; }

    public int ResearchTimeMonths { get; set; }

    public double ComplexityNormalized { get; set; }

    public ResearchStatus Status { get; set; }

    public int ProgressMonths { get; set; }

    public int? StartedMonthIndex { get; set; }

    public int? CompletedMonthIndex { get; set; }
}

public sealed class ResearchExpenseSnapshot
{
    public int MonthIndex { get; set; }

    public double Expense { get; set; }
}
