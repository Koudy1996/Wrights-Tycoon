using System;

namespace WrightTycoon.Core.Accounting;

public enum LoanRepaymentMode
{
    Amortized = 0,
    InterestOnly = 1,
}

public enum LoanPrepaymentStrategy
{
    ShortenTerm = 0,
    ReducePayment = 1,
}

public enum CompanyCreditRating
{
    E = 0,
    D = 1,
    C = 2,
    B = 3,
    A = 4,
}

public static class LoanBalance
{
    public const double MinimumPrincipal = 5_000_000d;
    public const double PrincipalIncrement = 1_000_000d;
    public const double BaseUnsecuredCredit = 150_000_000d;
    public const double MaximumCreditLimit = 2_000_000_000d;
    public const double OriginationFeeShare = 0.005d;
    public const int FactoryConstructionTermMonths = 120;
    public const double FactoryConstructionRateDiscount = 0.02d;
    public const double EarlyPrepaymentFeeShare = 0.01d;
    public const int EarlyPrepaymentFeeMonths = 12;
    public const double MinimumPrepayment = 1_000_000d;

    public const double EmergencyMaximumProceeds = 75_000_000d;
    public const double EmergencyAnnualInterestRate = 0.30d;
    public const double EmergencyCapitalizedFeeShare = 0.05d;
    public const int EmergencyTermMonths = 24;
    public const int EmergencyInterestOnlyMonths = 3;
    public const int EmergencyRecoveryPenaltyMonths = 24;

    public static readonly int[] AllowedTermsMonths = { 24, 36, 60, 120 };

    public static bool IsAllowedTerm(int termMonths) => Array.IndexOf(AllowedTermsMonths, termMonths) >= 0;

    public static double GetBaseAnnualInterestRate(CompanyCreditRating rating) => rating switch
    {
        CompanyCreditRating.A => 0.06d,
        CompanyCreditRating.B => 0.09d,
        CompanyCreditRating.C => 0.13d,
        CompanyCreditRating.D => 0.18d,
        _ => throw new InvalidOperationException("Rating E is not eligible for standard financing."),
    };

    public static double GetTermRateAdjustment(int termMonths) => termMonths switch
    {
        24 => -0.005d,
        36 or 60 => 0d,
        120 => 0.015d,
        _ => throw new ArgumentOutOfRangeException(nameof(termMonths)),
    };

    public static double GetCreditLimitMultiplier(CompanyCreditRating rating) => rating switch
    {
        CompanyCreditRating.A => 1d,
        CompanyCreditRating.B => 0.80d,
        CompanyCreditRating.C => 0.55d,
        CompanyCreditRating.D => 0.25d,
        _ => 0d,
    };

    public static CompanyCreditRating ResolveRating(double score)
    {
        if (score >= 85d) return CompanyCreditRating.A;
        if (score >= 70d) return CompanyCreditRating.B;
        if (score >= 55d) return CompanyCreditRating.C;
        if (score >= 40d) return CompanyCreditRating.D;
        return CompanyCreditRating.E;
    }

    public static double CalculateAmortizedPayment(double principal, double annualInterestRate, int termMonths)
    {
        if (principal <= 0d) throw new ArgumentOutOfRangeException(nameof(principal));
        if (annualInterestRate < 0d || double.IsNaN(annualInterestRate) || double.IsInfinity(annualInterestRate))
            throw new ArgumentOutOfRangeException(nameof(annualInterestRate));
        if (termMonths <= 0) throw new ArgumentOutOfRangeException(nameof(termMonths));
        var monthlyRate = annualInterestRate / 12d;
        if (monthlyRate <= 0d) return principal / termMonths;
        return principal * monthlyRate / (1d - System.Math.Pow(1d + monthlyRate, -termMonths));
    }

    public static double RoundMoney(double value) => System.Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
