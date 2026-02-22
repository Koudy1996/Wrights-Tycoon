using System;
using System.Collections.Generic;
using System.Linq;

namespace WrightTycoon.Core.Contracts;

public sealed class ContractEvaluationReport
{
    private readonly IReadOnlyList<string> _unmetRequirements;

    public ContractEvaluationReport(IEnumerable<string> unmetRequirements)
    {
        if (unmetRequirements is null)
        {
            throw new ArgumentNullException(nameof(unmetRequirements));
        }

        _unmetRequirements = unmetRequirements
            .Where(requirement => !string.IsNullOrWhiteSpace(requirement))
            .ToArray();
    }

    public IReadOnlyList<string> UnmetRequirements => _unmetRequirements;

    public bool CanDeliver => _unmetRequirements.Count == 0;
}
