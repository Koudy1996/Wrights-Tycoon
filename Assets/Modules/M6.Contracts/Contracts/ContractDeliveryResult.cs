namespace WrightTycoon.Core.Contracts;

public sealed record ContractDeliveryResult(
    string ContractId,
    bool IsSuccess,
    ContractEvaluationReport Evaluation,
    string Message);
