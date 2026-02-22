using System;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContract
{
    public GovernmentContract(
        string contractId,
        SimulationDate generatedOn,
        GovernmentContractRequirements requirements)
    {
        ContractId = string.IsNullOrWhiteSpace(contractId)
            ? throw new ArgumentException("Contract id cannot be empty.", nameof(contractId))
            : contractId.Trim();
        GeneratedOn = generatedOn;
        Requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        AvailabilityExpiresOn = generatedOn.AddMonths(WrightTycoon.Core.Balance.GovernmentContractBalance.ContractAvailabilityMonths);
        Status = GovernmentContractStatus.Available;

        Requirements.Validate();
    }

    public string ContractId { get; }

    public SimulationDate GeneratedOn { get; }

    public SimulationDate AvailabilityExpiresOn { get; }

    public GovernmentContractRequirements Requirements { get; }

    public GovernmentContractStatus Status { get; private set; }

    public SimulationDate? AcceptedOn { get; private set; }

    public SimulationDate? EvaluationDueOn { get; private set; }

    public GovernmentContractSubmissionCandidate? AcceptedSubmission { get; private set; }

    public double UpfrontFundingGranted { get; private set; }

    public GovernmentContractEvaluationResult? EvaluationResult { get; private set; }

    public void Accept(
        SimulationDate acceptedOn,
        GovernmentContractSubmissionCandidate submission,
        double upfrontFundingGranted)
    {
        if (Status != GovernmentContractStatus.Available)
        {
            throw new InvalidOperationException($"Contract '{ContractId}' is in status '{Status}' and cannot be accepted.");
        }

        if (IsOnOrAfter(acceptedOn, AvailabilityExpiresOn))
        {
            throw new InvalidOperationException($"Contract '{ContractId}' is no longer available.");
        }

        submission.Validate();
        Numeric.ThrowIfNotFinite(upfrontFundingGranted, nameof(upfrontFundingGranted));
        if (upfrontFundingGranted < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(upfrontFundingGranted), "Upfront funding must be >= 0.");
        }

        Status = GovernmentContractStatus.Accepted;
        AcceptedOn = acceptedOn;
        EvaluationDueOn = acceptedOn.AddMonths(WrightTycoon.Core.Balance.GovernmentContractBalance.ContractEvaluationDelayMonths);
        AcceptedSubmission = submission;
        UpfrontFundingGranted = upfrontFundingGranted;
    }

    public void Expire()
    {
        if (Status != GovernmentContractStatus.Available)
        {
            return;
        }

        Status = GovernmentContractStatus.Expired;
    }

    public void SetEvaluationResult(GovernmentContractEvaluationResult evaluationResult)
    {
        if (Status != GovernmentContractStatus.Accepted)
        {
            throw new InvalidOperationException($"Contract '{ContractId}' is in status '{Status}' and cannot be evaluated.");
        }

        EvaluationResult = evaluationResult ?? throw new ArgumentNullException(nameof(evaluationResult));
        Status = GovernmentContractStatus.Evaluated;
    }

    private static bool IsOnOrAfter(SimulationDate left, SimulationDate right)
    {
        var leftIndex = ((left.Year - 1) * 12) + (left.Month - 1);
        var rightIndex = ((right.Year - 1) * 12) + (right.Month - 1);
        return leftIndex >= rightIndex;
    }
}
