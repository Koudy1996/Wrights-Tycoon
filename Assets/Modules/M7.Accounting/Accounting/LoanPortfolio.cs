using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Presentation;
using Math = System.Math;

namespace WrightTycoon.Core.Accounting;

public sealed record LoanFinancialContext(
    double Cash,
    double InventoryValue,
    double FactoryResaleValue,
    double EquipmentScrapValue,
    IReadOnlyList<MonthlyAccountingSnapshot> AccountingHistory,
    int CurrentMonthIndex);

public sealed record LoanCreditAssessment(
    double HealthScore,
    CompanyCreditRating Rating,
    double LiquidityScore,
    double DebtServiceCoverageScore,
    double LeverageScore,
    double ProfitConsistencyScore,
    double PaymentHistoryScore,
    double CreditLimit,
    double OutstandingPrincipal,
    double AvailableCredit,
    double AnnualInterestRate,
    bool StandardFinancingAvailable,
    string AvailabilityMessage);

public sealed record LoanOffer(
    double Principal,
    int TermMonths,
    LoanRepaymentMode RepaymentMode,
    double AnnualInterestRate,
    double OriginationFee,
    double ScheduledMonthlyPayment,
    double BalloonPayment,
    double EstimatedTotalInterest);

public sealed record LoanPaymentInstruction(
    string LoanId,
    double Interest,
    double Principal,
    bool ClosedAfterPayment,
    bool IsEmergency)
{
    public double TotalPayment => Interest + Principal;
}

public sealed record LoanPrepaymentQuote(
    string LoanId,
    double PrincipalPayment,
    double Fee,
    double TotalCashRequired,
    bool PaysOffLoan);

public sealed record EmergencyLoanOffer(
    double CashProceeds,
    double CapitalizedFee,
    double Principal,
    double AnnualInterestRate,
    int TermMonths,
    int InterestOnlyMonths,
    double InterestOnlyMonthlyPayment,
    double LaterAmortizedPayment);

public sealed class LoanState
{
    internal LoanState(
        string loanId,
        double originalPrincipal,
        double outstandingPrincipal,
        double annualInterestRate,
        int originalTermMonths,
        int remainingMonths,
        int monthsSinceOrigination,
        int nextDueMonthIndex,
        int lastProcessedMonthIndex,
        double scheduledPayment,
        LoanRepaymentMode repaymentMode,
        bool isEmergency,
        int emergencyInterestOnlyMonthsRemaining,
        double totalInterestPaid,
        double totalPrincipalPaid,
        bool isClosed)
    {
        LoanId = loanId;
        OriginalPrincipal = originalPrincipal;
        OutstandingPrincipal = outstandingPrincipal;
        AnnualInterestRate = annualInterestRate;
        OriginalTermMonths = originalTermMonths;
        RemainingMonths = remainingMonths;
        MonthsSinceOrigination = monthsSinceOrigination;
        NextDueMonthIndex = nextDueMonthIndex;
        LastProcessedMonthIndex = lastProcessedMonthIndex;
        ScheduledPayment = scheduledPayment;
        RepaymentMode = repaymentMode;
        IsEmergency = isEmergency;
        EmergencyInterestOnlyMonthsRemaining = emergencyInterestOnlyMonthsRemaining;
        TotalInterestPaid = totalInterestPaid;
        TotalPrincipalPaid = totalPrincipalPaid;
        IsClosed = isClosed;
    }

    public string LoanId { get; }
    public double OriginalPrincipal { get; }
    public double OutstandingPrincipal { get; internal set; }
    public double AnnualInterestRate { get; }
    public int OriginalTermMonths { get; }
    public int RemainingMonths { get; internal set; }
    public int MonthsSinceOrigination { get; internal set; }
    public int NextDueMonthIndex { get; internal set; }
    public int LastProcessedMonthIndex { get; internal set; }
    public double ScheduledPayment { get; internal set; }
    public LoanRepaymentMode RepaymentMode { get; }
    public bool IsEmergency { get; }
    public int EmergencyInterestOnlyMonthsRemaining { get; internal set; }
    public double TotalInterestPaid { get; internal set; }
    public double TotalPrincipalPaid { get; internal set; }
    public bool IsClosed { get; internal set; }
    public double MonthlyInterest => LoanBalance.RoundMoney(OutstandingPrincipal * AnnualInterestRate / 12d);
}

public sealed class LoanPortfolio
{
    private readonly List<LoanState> _loans = new();
    private int _nextLoanSequence = 1;

    public IReadOnlyList<LoanState> Loans => _loans.AsReadOnly();
    public IReadOnlyList<LoanState> ActiveLoans => _loans.Where(loan => !loan.IsClosed).ToArray();
    public bool EmergencyLoanUsed { get; private set; }
    public int EmergencyPaidOffMonthIndex { get; private set; } = -1;
    public double OutstandingPrincipal => _loans.Where(loan => !loan.IsClosed).Sum(loan => loan.OutstandingPrincipal);
    public bool HasActiveEmergencyLoan => _loans.Any(loan => !loan.IsClosed && loan.IsEmergency);

    public LoanCreditAssessment CalculateCreditAssessment(LoanFinancialContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        var history = context.AccountingHistory ?? Array.Empty<MonthlyAccountingSnapshot>();
        var recent = history.Count <= 12 ? history.ToArray() : history.Skip(history.Count - 12).ToArray();
        var outstanding = OutstandingPrincipal;
        var averageOperatingCosts = recent.Length == 0 ? 0d : recent.Average(month => month.Costs.TotalCosts);
        var averageOperatingProfit = recent.Length == 0 ? 0d : recent.Average(month => month.OperatingProfitLoss);
        var unencumberedCash = System.Math.Max(context.Cash - outstanding, 0d);
        var eligibleAssets = unencumberedCash
            + System.Math.Max(context.InventoryValue, 0d) * 0.50d
            + System.Math.Max(context.FactoryResaleValue, 0d) * 0.80d
            + System.Math.Max(context.EquipmentScrapValue, 0d);

        var liquidityScore = averageOperatingCosts <= 0.01d
            ? 25d
            : 25d * Clamp01(unencumberedCash / (12d * averageOperatingCosts));
        var nextDebtService = GetNextScheduledDebtService();
        var debtServiceCoverageScore = nextDebtService <= 0.01d
            ? 30d
            : 30d * Clamp01(System.Math.Max(averageOperatingProfit, 0d) / nextDebtService / 2d);
        var leverageScore = outstanding <= 0.01d
            ? 25d
            : 25d * Clamp01(1d - outstanding / System.Math.Max(eligibleAssets, 1d));
        var profitConsistencyScore = recent.Length < 6
            ? 5d
            : 10d * recent.Count(month => month.OperatingProfitLoss > 0d) / recent.Length;
        var paymentHistoryScore = 10d;
        if (HasActiveEmergencyLoan) paymentHistoryScore -= 5d;
        else if (EmergencyPaidOffMonthIndex >= 0
            && context.CurrentMonthIndex - EmergencyPaidOffMonthIndex < LoanBalance.EmergencyRecoveryPenaltyMonths)
            paymentHistoryScore -= 3d;
        paymentHistoryScore = System.Math.Max(paymentHistoryScore, 0d);

        var healthScore = liquidityScore + debtServiceCoverageScore + leverageScore
            + profitConsistencyScore + paymentHistoryScore;
        if (HasActiveEmergencyLoan) healthScore = System.Math.Min(healthScore, 54d);
        var rating = LoanBalance.ResolveRating(healthScore);
        var baseCapacity = LoanBalance.BaseUnsecuredCredit
            + 0.25d * unencumberedCash
            + 0.50d * System.Math.Max(context.InventoryValue, 0d)
            + 0.80d * System.Math.Max(context.FactoryResaleValue, 0d)
            + System.Math.Max(context.EquipmentScrapValue, 0d)
            + 18d * System.Math.Max(averageOperatingProfit, 0d);
        var creditLimit = System.Math.Min(
            LoanBalance.MaximumCreditLimit,
            System.Math.Max(0d, baseCapacity * LoanBalance.GetCreditLimitMultiplier(rating)));
        var availableCredit = System.Math.Max(0d, creditLimit - outstanding);
        var activeStandardLoans = _loans.Count(loan => !loan.IsClosed && !loan.IsEmergency);
        var standardAvailable = rating != CompanyCreditRating.E
            && !HasActiveEmergencyLoan
            && activeStandardLoans < LoanBalance.MaximumStandardActiveLoans
            && availableCredit >= LoanBalance.MinimumPrincipal;
        var message = standardAvailable
            ? "Standard financing is available."
            : HasActiveEmergencyLoan
                ? "Standard financing is unavailable while emergency financing is active."
                : activeStandardLoans >= LoanBalance.MaximumStandardActiveLoans
                    ? $"Maximum of {LoanBalance.MaximumStandardActiveLoans} active standard loans reached."
                    : rating == CompanyCreditRating.E
                        ? "Company rating E is not eligible for standard financing."
                        : "Available credit is below the minimum loan amount.";
        var annualRate = rating == CompanyCreditRating.E ? 0d : LoanBalance.GetBaseAnnualInterestRate(rating);
        return new LoanCreditAssessment(
            healthScore,
            rating,
            liquidityScore,
            debtServiceCoverageScore,
            leverageScore,
            profitConsistencyScore,
            paymentHistoryScore,
            creditLimit,
            outstanding,
            availableCredit,
            annualRate,
            standardAvailable,
            message);
    }

    public bool TryBuildStandardOffer(
        double principal,
        int termMonths,
        LoanRepaymentMode repaymentMode,
        LoanCreditAssessment assessment,
        out LoanOffer? offer,
        out string message)
    {
        offer = null;
        if (assessment is null) throw new ArgumentNullException(nameof(assessment));
        if (!assessment.StandardFinancingAvailable)
        {
            message = assessment.AvailabilityMessage;
            return false;
        }
        if (double.IsNaN(principal) || double.IsInfinity(principal))
        {
            message = "Loan amount must be a finite number.";
            return false;
        }
        if (principal < LoanBalance.MinimumPrincipal || principal > assessment.AvailableCredit + 0.005d)
        {
            message = $"Loan amount must be between {FinancialValueFormatter.FormatNumber(LoanBalance.MinimumPrincipal)} and {FinancialValueFormatter.FormatNumber(assessment.AvailableCredit)}.";
            return false;
        }
        if (System.Math.Abs(principal / LoanBalance.PrincipalIncrement - System.Math.Round(principal / LoanBalance.PrincipalIncrement)) > 0.000001d)
        {
            message = $"Loan amount must use increments of {FinancialValueFormatter.FormatNumber(LoanBalance.PrincipalIncrement)}.";
            return false;
        }
        if (!LoanBalance.IsAllowedTerm(termMonths))
        {
            message = "Unsupported loan term.";
            return false;
        }
        if (!Enum.IsDefined(typeof(LoanRepaymentMode), repaymentMode))
        {
            message = "Unsupported repayment mode.";
            return false;
        }

        var annualRate = System.Math.Max(0d, assessment.AnnualInterestRate + LoanBalance.GetTermRateAdjustment(termMonths));
        var monthlyInterest = LoanBalance.RoundMoney(principal * annualRate / 12d);
        var scheduledPayment = repaymentMode == LoanRepaymentMode.Amortized
            ? LoanBalance.RoundMoney(LoanBalance.CalculateAmortizedPayment(principal, annualRate, termMonths))
            : monthlyInterest;
        var balloon = repaymentMode == LoanRepaymentMode.InterestOnly ? principal : 0d;
        var totalInterest = repaymentMode == LoanRepaymentMode.InterestOnly
            ? monthlyInterest * termMonths
            : System.Math.Max(0d, scheduledPayment * termMonths - principal);
        offer = new LoanOffer(
            principal,
            termMonths,
            repaymentMode,
            annualRate,
            LoanBalance.RoundMoney(principal * LoanBalance.OriginationFeeShare),
            scheduledPayment,
            balloon,
            LoanBalance.RoundMoney(totalInterest));
        message = "Loan offer prepared.";
        return true;
    }

    public LoanState AcceptStandardOffer(LoanOffer offer, int currentMonthIndex)
    {
        if (offer is null) throw new ArgumentNullException(nameof(offer));
        var loan = new LoanState(
            BuildLoanId(),
            offer.Principal,
            offer.Principal,
            offer.AnnualInterestRate,
            offer.TermMonths,
            offer.TermMonths,
            0,
            currentMonthIndex + 1,
            -1,
            offer.ScheduledMonthlyPayment,
            offer.RepaymentMode,
            false,
            0,
            0d,
            0d,
            false);
        _loans.Add(loan);
        return loan;
    }

    public IReadOnlyList<LoanPaymentInstruction> ProcessMonth(int monthIndex)
    {
        if (monthIndex < 0) throw new ArgumentOutOfRangeException(nameof(monthIndex));
        var instructions = new List<LoanPaymentInstruction>();
        foreach (var loan in _loans.Where(loan => !loan.IsClosed).OrderBy(loan => loan.LoanId, StringComparer.Ordinal))
        {
            if (monthIndex < loan.NextDueMonthIndex || loan.LastProcessedMonthIndex >= monthIndex) continue;
            var interest = loan.MonthlyInterest;
            var principal = 0d;
            if (loan.IsEmergency && loan.EmergencyInterestOnlyMonthsRemaining > 0)
            {
                loan.EmergencyInterestOnlyMonthsRemaining--;
            }
            else if (loan.RepaymentMode == LoanRepaymentMode.InterestOnly)
            {
                if (loan.RemainingMonths <= 1) principal = loan.OutstandingPrincipal;
            }
            else
            {
                principal = loan.RemainingMonths <= 1
                    ? loan.OutstandingPrincipal
                    : System.Math.Min(loan.OutstandingPrincipal, System.Math.Max(0d, loan.ScheduledPayment - interest));
            }

            principal = LoanBalance.RoundMoney(principal);
            loan.OutstandingPrincipal = LoanBalance.RoundMoney(System.Math.Max(0d, loan.OutstandingPrincipal - principal));
            loan.TotalInterestPaid = LoanBalance.RoundMoney(loan.TotalInterestPaid + interest);
            loan.TotalPrincipalPaid = LoanBalance.RoundMoney(loan.TotalPrincipalPaid + principal);
            loan.RemainingMonths = System.Math.Max(0, loan.RemainingMonths - 1);
            loan.MonthsSinceOrigination++;
            loan.LastProcessedMonthIndex = monthIndex;
            loan.NextDueMonthIndex = monthIndex + 1;
            if (loan.OutstandingPrincipal <= 0.005d || loan.RemainingMonths == 0)
            {
                loan.OutstandingPrincipal = 0d;
                loan.IsClosed = true;
                if (loan.IsEmergency) EmergencyPaidOffMonthIndex = monthIndex;
            }
            instructions.Add(new LoanPaymentInstruction(loan.LoanId, interest, principal, loan.IsClosed, loan.IsEmergency));
        }
        return instructions;
    }

    public double GetNextScheduledDebtService() =>
        _loans.Where(loan => !loan.IsClosed).Sum(CalculateNextPayment);

    public bool TryBuildPrepaymentQuote(
        string loanId,
        double requestedPrincipal,
        out LoanPrepaymentQuote? quote,
        out string message)
    {
        quote = null;
        var loan = FindActiveLoan(loanId);
        if (loan is null)
        {
            message = "Active loan was not found.";
            return false;
        }
        if (double.IsNaN(requestedPrincipal) || double.IsInfinity(requestedPrincipal))
        {
            message = "Repayment must be a finite number.";
            return false;
        }
        if (requestedPrincipal < LoanBalance.MinimumPrepayment && requestedPrincipal < loan.OutstandingPrincipal - 0.005d)
        {
            message = $"Partial repayment must be at least {FinancialValueFormatter.FormatNumber(LoanBalance.MinimumPrepayment)}.";
            return false;
        }
        var principal = LoanBalance.RoundMoney(System.Math.Min(System.Math.Max(requestedPrincipal, 0d), loan.OutstandingPrincipal));
        if (principal <= 0d)
        {
            message = "Repayment must be greater than zero.";
            return false;
        }
        var fee = loan.MonthsSinceOrigination < LoanBalance.EarlyPrepaymentFeeMonths
            ? LoanBalance.RoundMoney(principal * LoanBalance.EarlyPrepaymentFeeShare)
            : 0d;
        quote = new LoanPrepaymentQuote(
            loan.LoanId,
            principal,
            fee,
            LoanBalance.RoundMoney(principal + fee),
            principal >= loan.OutstandingPrincipal - 0.005d);
        message = "Repayment quote prepared.";
        return true;
    }

    public bool ApplyPrepayment(
        LoanPrepaymentQuote quote,
        LoanPrepaymentStrategy strategy,
        int currentMonthIndex,
        out string message)
    {
        if (quote is null) throw new ArgumentNullException(nameof(quote));
        var loan = FindActiveLoan(quote.LoanId);
        if (loan is null)
        {
            message = "Active loan was not found.";
            return false;
        }
        loan.OutstandingPrincipal = LoanBalance.RoundMoney(System.Math.Max(0d, loan.OutstandingPrincipal - quote.PrincipalPayment));
        loan.TotalPrincipalPaid = LoanBalance.RoundMoney(loan.TotalPrincipalPaid + quote.PrincipalPayment);
        if (loan.OutstandingPrincipal <= 0.005d)
        {
            loan.OutstandingPrincipal = 0d;
            loan.RemainingMonths = 0;
            loan.IsClosed = true;
            if (loan.IsEmergency) EmergencyPaidOffMonthIndex = currentMonthIndex;
            message = $"Loan {loan.LoanId} paid off.";
            return true;
        }

        if (loan.RepaymentMode == LoanRepaymentMode.Amortized && loan.EmergencyInterestOnlyMonthsRemaining == 0)
        {
            if (strategy == LoanPrepaymentStrategy.ReducePayment)
            {
                loan.ScheduledPayment = LoanBalance.RoundMoney(LoanBalance.CalculateAmortizedPayment(
                    loan.OutstandingPrincipal,
                    loan.AnnualInterestRate,
                    System.Math.Max(loan.RemainingMonths, 1)));
            }
            else
            {
                loan.RemainingMonths = CalculateRemainingMonthsForPayment(
                    loan.OutstandingPrincipal,
                    loan.AnnualInterestRate,
                    loan.ScheduledPayment,
                    loan.RemainingMonths);
            }
        }
        message = $"Loan {loan.LoanId} principal reduced to {FinancialValueFormatter.FormatNumber(loan.OutstandingPrincipal)}.";
        return true;
    }

    public bool TryBuildEmergencyOffer(double cashDeficit, double nextMonthlyFixedObligations, out EmergencyLoanOffer? offer, out string message)
    {
        offer = null;
        if (double.IsNaN(cashDeficit) || double.IsInfinity(cashDeficit)
            || double.IsNaN(nextMonthlyFixedObligations) || double.IsInfinity(nextMonthlyFixedObligations))
        {
            message = "Emergency financing inputs must be finite.";
            return false;
        }
        if (EmergencyLoanUsed || HasActiveEmergencyLoan)
        {
            message = "Emergency financing has already been used in this run.";
            return false;
        }
        if (cashDeficit <= 0d)
        {
            message = "Emergency financing is only available while cash is below zero.";
            return false;
        }
        var requiredProceeds = System.Math.Ceiling(
            (cashDeficit + 3d * System.Math.Max(nextMonthlyFixedObligations, 0d)) /
            LoanBalance.PrincipalIncrement) * LoanBalance.PrincipalIncrement;
        requiredProceeds = System.Math.Max(requiredProceeds, LoanBalance.MinimumPrincipal);
        if (requiredProceeds > LoanBalance.EmergencyMaximumProceeds)
        {
            message = $"Required emergency financing exceeds the {FinancialValueFormatter.FormatNumber(LoanBalance.EmergencyMaximumProceeds)} limit.";
            return false;
        }
        var capitalizedFee = LoanBalance.RoundMoney(requiredProceeds * LoanBalance.EmergencyCapitalizedFeeShare);
        var principal = LoanBalance.RoundMoney(requiredProceeds + capitalizedFee);
        var laterMonths = LoanBalance.EmergencyTermMonths - LoanBalance.EmergencyInterestOnlyMonths;
        offer = new EmergencyLoanOffer(
            requiredProceeds,
            capitalizedFee,
            principal,
            LoanBalance.EmergencyAnnualInterestRate,
            LoanBalance.EmergencyTermMonths,
            LoanBalance.EmergencyInterestOnlyMonths,
            LoanBalance.RoundMoney(principal * LoanBalance.EmergencyAnnualInterestRate / 12d),
            LoanBalance.RoundMoney(LoanBalance.CalculateAmortizedPayment(
                principal,
                LoanBalance.EmergencyAnnualInterestRate,
                laterMonths)));
        message = "Emergency financing is available.";
        return true;
    }

    public LoanState AcceptEmergencyOffer(EmergencyLoanOffer offer, int currentMonthIndex)
    {
        if (offer is null) throw new ArgumentNullException(nameof(offer));
        if (EmergencyLoanUsed || HasActiveEmergencyLoan) throw new InvalidOperationException("Emergency financing is no longer available.");
        var loan = new LoanState(
            BuildLoanId(),
            offer.Principal,
            offer.Principal,
            offer.AnnualInterestRate,
            offer.TermMonths,
            offer.TermMonths,
            0,
            currentMonthIndex + 1,
            -1,
            offer.LaterAmortizedPayment,
            LoanRepaymentMode.Amortized,
            true,
            offer.InterestOnlyMonths,
            0d,
            0d,
            false);
        _loans.Add(loan);
        EmergencyLoanUsed = true;
        return loan;
    }

    public LoanPortfolioSnapshot CreateSnapshot()
    {
        var snapshot = new LoanPortfolioSnapshot
        {
            NextLoanSequence = _nextLoanSequence,
            EmergencyLoanUsed = EmergencyLoanUsed,
            EmergencyPaidOffMonthIndex = EmergencyPaidOffMonthIndex,
        };
        foreach (var loan in _loans)
        {
            snapshot.Loans.Add(new LoanStateSnapshot
            {
                LoanId = loan.LoanId,
                OriginalPrincipal = loan.OriginalPrincipal,
                OutstandingPrincipal = loan.OutstandingPrincipal,
                AnnualInterestRate = loan.AnnualInterestRate,
                OriginalTermMonths = loan.OriginalTermMonths,
                RemainingMonths = loan.RemainingMonths,
                MonthsSinceOrigination = loan.MonthsSinceOrigination,
                NextDueMonthIndex = loan.NextDueMonthIndex,
                LastProcessedMonthIndex = loan.LastProcessedMonthIndex,
                ScheduledPayment = loan.ScheduledPayment,
                RepaymentMode = (int)loan.RepaymentMode,
                IsEmergency = loan.IsEmergency,
                EmergencyInterestOnlyMonthsRemaining = loan.EmergencyInterestOnlyMonthsRemaining,
                TotalInterestPaid = loan.TotalInterestPaid,
                TotalPrincipalPaid = loan.TotalPrincipalPaid,
                IsClosed = loan.IsClosed,
            });
        }
        return snapshot;
    }

    public void RestoreSnapshot(LoanPortfolioSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        _loans.Clear();
        _nextLoanSequence = System.Math.Max(snapshot.NextLoanSequence, 1);
        EmergencyLoanUsed = snapshot.EmergencyLoanUsed;
        EmergencyPaidOffMonthIndex = snapshot.EmergencyPaidOffMonthIndex;
        if (snapshot.Loans is null) return;
        foreach (var item in snapshot.Loans)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.LoanId) || item.OriginalPrincipal <= 0d) continue;
            var mode = Enum.IsDefined(typeof(LoanRepaymentMode), item.RepaymentMode)
                ? (LoanRepaymentMode)item.RepaymentMode
                : LoanRepaymentMode.Amortized;
            _loans.Add(new LoanState(
                item.LoanId.Trim(),
                item.OriginalPrincipal,
                System.Math.Max(item.OutstandingPrincipal, 0d),
                System.Math.Max(item.AnnualInterestRate, 0d),
                System.Math.Max(item.OriginalTermMonths, 1),
                System.Math.Max(item.RemainingMonths, 0),
                System.Math.Max(item.MonthsSinceOrigination, 0),
                System.Math.Max(item.NextDueMonthIndex, 0),
                item.LastProcessedMonthIndex,
                System.Math.Max(item.ScheduledPayment, 0d),
                mode,
                item.IsEmergency,
                System.Math.Max(item.EmergencyInterestOnlyMonthsRemaining, 0),
                System.Math.Max(item.TotalInterestPaid, 0d),
                System.Math.Max(item.TotalPrincipalPaid, 0d),
                item.IsClosed || item.OutstandingPrincipal <= 0.005d));
        }
    }

    private string BuildLoanId() => $"loan.{_nextLoanSequence++:D4}";

    private LoanState? FindActiveLoan(string loanId)
    {
        if (string.IsNullOrWhiteSpace(loanId)) return null;
        return _loans.FirstOrDefault(loan => !loan.IsClosed && string.Equals(loan.LoanId, loanId.Trim(), StringComparison.Ordinal));
    }

    private static double CalculateNextPayment(LoanState loan)
    {
        var interest = loan.MonthlyInterest;
        if (loan.IsEmergency && loan.EmergencyInterestOnlyMonthsRemaining > 0) return interest;
        if (loan.RepaymentMode == LoanRepaymentMode.InterestOnly)
            return loan.RemainingMonths <= 1 ? interest + loan.OutstandingPrincipal : interest;
        return loan.RemainingMonths <= 1 ? interest + loan.OutstandingPrincipal : System.Math.Min(loan.ScheduledPayment, interest + loan.OutstandingPrincipal);
    }

    private static int CalculateRemainingMonthsForPayment(
        double principal,
        double annualInterestRate,
        double scheduledPayment,
        int currentRemainingMonths)
    {
        var balance = principal;
        var monthlyRate = annualInterestRate / 12d;
        for (var month = 1; month <= System.Math.Max(currentRemainingMonths, 1); month++)
        {
            var interest = balance * monthlyRate;
            var principalPayment = scheduledPayment - interest;
            if (principalPayment <= 0d) return currentRemainingMonths;
            balance -= principalPayment;
            if (balance <= 0.005d) return month;
        }
        return currentRemainingMonths;
    }

    private static double Clamp01(double value) => value <= 0d ? 0d : value >= 1d ? 1d : value;
}
