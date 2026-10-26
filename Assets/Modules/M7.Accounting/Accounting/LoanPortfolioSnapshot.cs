using System.Collections.Generic;

namespace WrightTycoon.Core.Accounting;

public sealed class LoanPortfolioSnapshot
{
    public int NextLoanSequence { get; set; } = 1;
    public bool EmergencyLoanUsed { get; set; }
    public int EmergencyPaidOffMonthIndex { get; set; } = -1;
    public List<LoanStateSnapshot> Loans { get; set; } = new();
}

public sealed class LoanStateSnapshot
{
    public string LoanId { get; set; } = string.Empty;
    public double OriginalPrincipal { get; set; }
    public double OutstandingPrincipal { get; set; }
    public double AnnualInterestRate { get; set; }
    public int OriginalTermMonths { get; set; }
    public int RemainingMonths { get; set; }
    public int MonthsSinceOrigination { get; set; }
    public int NextDueMonthIndex { get; set; }
    public int OriginationMonthIndex { get; set; } = -1;
    public int LastProcessedMonthIndex { get; set; } = -1;
    public double ScheduledPayment { get; set; }
    public int RepaymentMode { get; set; }
    public bool IsEmergency { get; set; }
    public int EmergencyInterestOnlyMonthsRemaining { get; set; }
    public double TotalInterestPaid { get; set; }
    public double TotalPrincipalPaid { get; set; }
    public bool IsClosed { get; set; }
}
