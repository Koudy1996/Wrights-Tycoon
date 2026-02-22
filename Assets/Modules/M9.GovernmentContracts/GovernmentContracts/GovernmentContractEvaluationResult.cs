using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractEvaluationResult
{
    private readonly IReadOnlyList<GovernmentContractParameterBonus> _parameterBonuses;

    public GovernmentContractEvaluationResult(
        SimulationDate evaluatedOn,
        bool isSuccess,
        double finalProbability,
        double randomRoll,
        IEnumerable<GovernmentContractParameterBonus> parameterBonuses)
    {
        if (parameterBonuses is null)
        {
            throw new ArgumentNullException(nameof(parameterBonuses));
        }

        EvaluatedOn = evaluatedOn;
        IsSuccess = isSuccess;
        FinalProbability = finalProbability;
        RandomRoll = randomRoll;
        _parameterBonuses = parameterBonuses.ToArray();
    }

    public SimulationDate EvaluatedOn { get; }

    public bool IsSuccess { get; }

    public double FinalProbability { get; }

    public double BonusProbability => FinalProbability - 0.5d;

    public double RandomRoll { get; }

    public IReadOnlyList<GovernmentContractParameterBonus> ParameterBonuses => _parameterBonuses;
}
