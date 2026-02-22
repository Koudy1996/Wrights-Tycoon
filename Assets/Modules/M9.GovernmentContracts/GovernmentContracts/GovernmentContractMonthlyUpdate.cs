using System.Collections.Generic;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed record GovernmentContractMonthlyUpdate(
    GovernmentContract? GeneratedContract,
    GovernmentContract? ExpiredContract,
    GovernmentContract? EvaluatedContract,
    GovernmentContractEvaluationResult? EvaluationResult,
    GovernmentDeliveryContract? CreatedDeliveryContract,
    IReadOnlyList<string> Events);
