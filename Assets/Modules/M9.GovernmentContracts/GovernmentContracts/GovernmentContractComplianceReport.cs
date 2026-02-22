using System;
using System.Collections.Generic;
using System.Linq;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractComplianceReport
{
    private readonly IReadOnlyList<string> _unmetRequirements;
    private readonly IReadOnlyList<GovernmentContractParameterBonus> _parameterBonuses;

    public GovernmentContractComplianceReport(
        bool isLifecycleStageAllowed,
        IEnumerable<string> unmetRequirements,
        IEnumerable<GovernmentContractParameterBonus> parameterBonuses)
    {
        if (unmetRequirements is null)
        {
            throw new ArgumentNullException(nameof(unmetRequirements));
        }

        if (parameterBonuses is null)
        {
            throw new ArgumentNullException(nameof(parameterBonuses));
        }

        IsLifecycleStageAllowed = isLifecycleStageAllowed;
        _unmetRequirements = unmetRequirements
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        _parameterBonuses = parameterBonuses.ToArray();
    }

    public bool IsLifecycleStageAllowed { get; }

    public bool MeetsAllRequirements => _unmetRequirements.Count == 0;

    public bool IsSelectableInUi => IsLifecycleStageAllowed && MeetsAllRequirements;

    public IReadOnlyList<string> UnmetRequirements => _unmetRequirements;

    public IReadOnlyList<GovernmentContractParameterBonus> ParameterBonuses => _parameterBonuses;
}
