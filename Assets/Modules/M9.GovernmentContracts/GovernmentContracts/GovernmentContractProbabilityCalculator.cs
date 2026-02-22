using System;
using System.Collections.Generic;
using WrightTycoon.Core.Balance;

namespace WrightTycoon.Core.GovernmentContracts;

public static class GovernmentContractProbabilityCalculator
{
    public static GovernmentContractParameterBonus CreateHigherIsBetterBonus(
        GovernmentContractParameter parameter,
        double requiredValue,
        double actualValue)
    {
        if (requiredValue <= 0d)
        {
            return new GovernmentContractParameterBonus(parameter, requiredValue, actualValue, 0d, 0d);
        }

        var excessRatio = (actualValue - requiredValue) / requiredValue;
        var bonus = CalculateBonusByExcessRatio(excessRatio);
        return new GovernmentContractParameterBonus(parameter, requiredValue, actualValue, excessRatio, bonus);
    }

    public static GovernmentContractParameterBonus CreateLowerIsBetterBonus(
        GovernmentContractParameter parameter,
        double requiredMaximumValue,
        double actualValue)
    {
        if (requiredMaximumValue <= 0d)
        {
            return new GovernmentContractParameterBonus(parameter, requiredMaximumValue, actualValue, 0d, 0d);
        }

        var excessRatio = (requiredMaximumValue - actualValue) / requiredMaximumValue;
        var bonus = CalculateBonusByExcessRatio(excessRatio);
        return new GovernmentContractParameterBonus(parameter, requiredMaximumValue, actualValue, excessRatio, bonus);
    }

    public static double CalculateBonusByExcessRatio(double excessRatio)
    {
        if (excessRatio <= 0d)
        {
            return 0d;
        }

        var normalized = excessRatio / GovernmentContractBalance.FullBonusExcessRatio;
        if (normalized > 1d)
        {
            normalized = 1d;
        }

        return GovernmentContractBalance.MaxBonusPerParameter * normalized;
    }

    public static double CalculateFinalProbability(
        IEnumerable<GovernmentContractParameterBonus> parameterBonuses,
        double baseProbability = GovernmentContractBalance.BaseEvaluationProbability)
    {
        if (parameterBonuses is null)
        {
            throw new ArgumentNullException(nameof(parameterBonuses));
        }

        var total = baseProbability;
        foreach (var parameterBonus in parameterBonuses)
        {
            total += parameterBonus.BonusValue;
        }

        return Clamp01(total);
    }

    public static bool ResolveSuccess(double finalProbability, double randomRoll) => randomRoll <= finalProbability;

    private static double Clamp01(double value)
    {
        if (value < 0d)
        {
            return 0d;
        }

        return value > 1d ? 1d : value;
    }
}
