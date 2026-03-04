using System;

namespace WrightTycoon.Core.Simulation;

public readonly record struct SimulationDate
{
    public static SimulationDate WrightTycoonStart => new(1950, 1);

    public SimulationDate(int year, int month)
    {
        if (year < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be greater than or equal to 1.");
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be in range 1..12.");
        }

        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public SimulationDate NextMonth() => AddMonths(1);

    public SimulationDate AddMonths(int months)
    {
        var baseMonthIndex = checked(((Year - 1) * 12) + (Month - 1));
        var targetMonthIndex = checked(baseMonthIndex + months);

        if (targetMonthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(months), "Resulting date is before year 1.");
        }

        var targetYear = (targetMonthIndex / 12) + 1;
        var targetMonth = (targetMonthIndex % 12) + 1;
        return new SimulationDate(targetYear, targetMonth);
    }

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
