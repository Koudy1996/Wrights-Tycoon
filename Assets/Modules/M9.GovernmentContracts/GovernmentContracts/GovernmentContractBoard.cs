using System;
using System.Collections.Generic;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractBoard
{
    private readonly IGovernmentContractGenerator _generator;
    private readonly IGovernmentContractRandomProvider _random;
    private readonly GovernmentContractComplianceEvaluator _complianceEvaluator;
    private readonly IGovernmentContractPricingService _pricingService;
    private readonly List<GovernmentContract> _expiredContracts = new();
    private readonly List<GovernmentContract> _evaluatedContracts = new();
    private readonly List<GovernmentDeliveryContract> _closedDeliveryContracts = new();
    private int _contractSequence;

    public GovernmentContractBoard(
        IGovernmentContractGenerator generator,
        IGovernmentContractRandomProvider random,
        GovernmentContractComplianceEvaluator? complianceEvaluator = null,
        IGovernmentContractPricingService? pricingService = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _complianceEvaluator = complianceEvaluator ?? new GovernmentContractComplianceEvaluator();
        _pricingService = pricingService ?? new GovernmentContractPricingService();
    }

    public GovernmentContract? ActiveContract { get; private set; }

    public GovernmentDeliveryContract? ActiveDeliveryContract { get; private set; }

    public IReadOnlyList<GovernmentContract> ExpiredContracts => _expiredContracts;

    public IReadOnlyList<GovernmentContract> EvaluatedContracts => _evaluatedContracts;

    public IReadOnlyList<GovernmentDeliveryContract> ClosedDeliveryContracts => _closedDeliveryContracts;

    public GovernmentContractMonthlyUpdate ProcessMonth(SimulationDate currentDate)
    {
        var events = new List<string>();
        GovernmentContract? expiredContract = null;
        GovernmentContract? evaluatedContract = null;
        GovernmentContract? generatedContract = null;
        GovernmentContractEvaluationResult? evaluationResult = null;
        GovernmentDeliveryContract? createdDeliveryContract = null;

        if (ActiveContract is not null
            && ActiveContract.Status == GovernmentContractStatus.Available
            && IsOnOrAfter(currentDate, ActiveContract.AvailabilityExpiresOn))
        {
            ActiveContract.Expire();
            expiredContract = ActiveContract;
            _expiredContracts.Add(ActiveContract);
            ActiveContract = null;
            events.Add($"Government contract expired ({expiredContract.ContractId}).");
        }

        if (ActiveContract is not null
            && ActiveContract.Status == GovernmentContractStatus.Accepted
            && ActiveContract.EvaluationDueOn.HasValue
            && IsOnOrAfter(currentDate, ActiveContract.EvaluationDueOn.Value))
        {
            evaluationResult = EvaluateAcceptedContract(ActiveContract, currentDate);
            ActiveContract.SetEvaluationResult(evaluationResult);
            evaluatedContract = ActiveContract;
            _evaluatedContracts.Add(ActiveContract);
            ActiveContract = null;
            events.Add($"Government contract evaluated ({evaluatedContract.ContractId}) with {evaluationResult.FinalProbability:P1} success probability.");

            if (evaluationResult.IsSuccess)
            {
                createdDeliveryContract = CreateDeliveryContract(evaluatedContract, evaluationResult, currentDate);
                ActiveDeliveryContract = createdDeliveryContract;
                events.Add($"Delivery contract created ({createdDeliveryContract.DeliveryId}) for {createdDeliveryContract.TotalUnitsRequired} units.");
            }
        }

        if (ActiveDeliveryContract is not null
            && ActiveDeliveryContract.Status == GovernmentDeliveryContractStatus.Active
            && IsOnOrAfter(currentDate, ActiveDeliveryContract.EndsOn)
            && ActiveDeliveryContract.RemainingUnits > 0)
        {
            ActiveDeliveryContract.MarkEnded(currentDate);
            _closedDeliveryContracts.Add(ActiveDeliveryContract);
            events.Add($"Delivery contract ended without full completion ({ActiveDeliveryContract.DeliveryId}).");
            ActiveDeliveryContract = null;
        }

        if (currentDate.Month == 1 && ActiveContract is null)
        {
            generatedContract = _generator.Generate(currentDate, _contractSequence);
            _contractSequence++;
            ActiveContract = generatedContract;
            events.Add($"New government contract generated ({generatedContract.ContractId}).");
        }

        return new GovernmentContractMonthlyUpdate(
            generatedContract,
            expiredContract,
            evaluatedContract,
            evaluationResult,
            createdDeliveryContract,
            events);
    }

    public IReadOnlyList<GovernmentContractSubmissionAssessment> GetSubmissionAssessments(
        IEnumerable<GovernmentContractSubmissionCandidate> submissions)
    {
        if (submissions is null)
        {
            throw new ArgumentNullException(nameof(submissions));
        }

        if (ActiveContract is null || ActiveContract.Status != GovernmentContractStatus.Available)
        {
            return Array.Empty<GovernmentContractSubmissionAssessment>();
        }

        var assessments = new List<GovernmentContractSubmissionAssessment>();
        foreach (var submission in submissions)
        {
            if (submission is null)
            {
                continue;
            }

            var compliance = _complianceEvaluator.Evaluate(ActiveContract.Requirements, submission);
            assessments.Add(new GovernmentContractSubmissionAssessment(submission, compliance));
        }

        return assessments;
    }

    public GovernmentContractAcceptanceResult TryAcceptActiveContract(
        SimulationDate currentDate,
        GovernmentContractSubmissionCandidate submission)
    {
        if (submission is null)
        {
            throw new ArgumentNullException(nameof(submission));
        }

        if (ActiveContract is null)
        {
            return new GovernmentContractAcceptanceResult(
                false,
                "No active government contract is available.",
                0d,
                null);
        }

        if (ActiveContract.Status != GovernmentContractStatus.Available)
        {
            return new GovernmentContractAcceptanceResult(
                false,
                $"Government contract '{ActiveContract.ContractId}' is not available for acceptance.",
                0d,
                null);
        }

        if (IsOnOrAfter(currentDate, ActiveContract.AvailabilityExpiresOn))
        {
            ActiveContract.Expire();
            _expiredContracts.Add(ActiveContract);
            ActiveContract = null;
            return new GovernmentContractAcceptanceResult(
                false,
                "Government contract has already expired.",
                0d,
                null);
        }

        var compliance = _complianceEvaluator.Evaluate(ActiveContract.Requirements, submission);
        if (!compliance.IsSelectableInUi)
        {
            return new GovernmentContractAcceptanceResult(
                false,
                "Selected aircraft does not meet government contract submission rules.",
                0d,
                compliance);
        }

        var upfrontFunding = submission.Metrics.DevelopmentCost * GovernmentContractBalance.UpfrontFundingShare;
        ActiveContract.Accept(currentDate, submission, upfrontFunding);

        return new GovernmentContractAcceptanceResult(
            true,
            $"Government contract accepted. Upfront funding granted: {upfrontFunding:0.00}.",
            upfrontFunding,
            compliance);
    }

    public GovernmentDeliveryProgressReport ProcessDeliveryFromInventory(
        SimulationDate currentDate,
        IGovernmentContractInventoryGateway inventory)
    {
        if (inventory is null)
        {
            throw new ArgumentNullException(nameof(inventory));
        }

        if (ActiveDeliveryContract is null || ActiveDeliveryContract.Status != GovernmentDeliveryContractStatus.Active)
        {
            return new GovernmentDeliveryProgressReport(false, 0, 0, 0, false, false, 0d, "No active government delivery contract.");
        }

        if (IsOnOrAfter(currentDate, ActiveDeliveryContract.EndsOn) && ActiveDeliveryContract.RemainingUnits > 0)
        {
            ActiveDeliveryContract.MarkEnded(currentDate);
            _closedDeliveryContracts.Add(ActiveDeliveryContract);
            var endedContractId = ActiveDeliveryContract.DeliveryId;
            ActiveDeliveryContract = null;
            return new GovernmentDeliveryProgressReport(false, 0, 0, 0, false, true, 0d, $"Delivery contract '{endedContractId}' ended with incomplete delivery.");
        }

        var designId = ActiveDeliveryContract.AircraftDesignId;
        var availableUnits = inventory.GetAvailableUnits(designId);
        if (availableUnits <= 0)
        {
            return new GovernmentDeliveryProgressReport(
                true,
                0,
                ActiveDeliveryContract.DeliveredUnits,
                ActiveDeliveryContract.RemainingUnits,
                false,
                false,
                0d,
                "No aircraft available in warehouse for automatic government delivery.");
        }

        var unitsToConsume = System.Math.Min(availableUnits, ActiveDeliveryContract.RemainingUnits);
        if (unitsToConsume <= 0)
        {
            return new GovernmentDeliveryProgressReport(
                true,
                0,
                ActiveDeliveryContract.DeliveredUnits,
                ActiveDeliveryContract.RemainingUnits,
                false,
                false,
                0d,
                "Delivery contract is already satisfied.");
        }

        if (!inventory.TryConsumeUnits(designId, unitsToConsume, out var consumeMessage))
        {
            return new GovernmentDeliveryProgressReport(
                true,
                0,
                ActiveDeliveryContract.DeliveredUnits,
                ActiveDeliveryContract.RemainingUnits,
                false,
                false,
                0d,
                $"Warehouse consumption failed: {consumeMessage}");
        }

        var deliveredNow = ActiveDeliveryContract.RegisterDeliveredUnits(unitsToConsume, currentDate);
        var completionBonusGranted = 0d;
        var completedThisMonth = false;
        if (ActiveDeliveryContract.Status == GovernmentDeliveryContractStatus.Completed)
        {
            completedThisMonth = true;
            if (!ActiveDeliveryContract.CompletionBonusGranted)
            {
                completionBonusGranted = RoundCurrency(
                    ActiveDeliveryContract.TotalContractValue * GovernmentContractBalance.DeliveryCompletionBonusShare);
                ActiveDeliveryContract.MarkCompletionBonusGranted();
            }

            _closedDeliveryContracts.Add(ActiveDeliveryContract);
            ActiveDeliveryContract = null;
        }

        var hasActiveAfterDelivery = ActiveDeliveryContract is not null && ActiveDeliveryContract.Status == GovernmentDeliveryContractStatus.Active;
        var totalDelivered = hasActiveAfterDelivery
            ? ActiveDeliveryContract!.DeliveredUnits
            : 0;
        var remainingUnits = hasActiveAfterDelivery
            ? ActiveDeliveryContract!.RemainingUnits
            : 0;

        return new GovernmentDeliveryProgressReport(
            hasActiveAfterDelivery,
            deliveredNow,
            totalDelivered,
            remainingUnits,
            completedThisMonth,
            false,
            completionBonusGranted,
            consumeMessage);
    }

    private GovernmentContractEvaluationResult EvaluateAcceptedContract(
        GovernmentContract contract,
        SimulationDate currentDate)
    {
        var acceptedSubmission = contract.AcceptedSubmission
            ?? throw new InvalidOperationException("Accepted contract does not contain selected submission.");

        var compliance = _complianceEvaluator.Evaluate(contract.Requirements, acceptedSubmission);
        if (!compliance.IsSelectableInUi)
        {
            return new GovernmentContractEvaluationResult(
                currentDate,
                isSuccess: false,
                finalProbability: GovernmentContractBalance.BaseEvaluationProbability,
                randomRoll: 1d,
                parameterBonuses: Array.Empty<GovernmentContractParameterBonus>());
        }

        var finalProbability = GovernmentContractProbabilityCalculator.CalculateFinalProbability(compliance.ParameterBonuses);
        var randomRoll = _random.NextDouble();
        var isSuccess = GovernmentContractProbabilityCalculator.ResolveSuccess(finalProbability, randomRoll);
        return new GovernmentContractEvaluationResult(
            currentDate,
            isSuccess,
            finalProbability,
            randomRoll,
            compliance.ParameterBonuses);
    }

    private GovernmentDeliveryContract CreateDeliveryContract(
        GovernmentContract evaluatedContract,
        GovernmentContractEvaluationResult evaluationResult,
        SimulationDate currentDate)
    {
        var acceptedSubmission = evaluatedContract.AcceptedSubmission
            ?? throw new InvalidOperationException("Accepted submission is required for delivery contract creation.");

        var baseUnits = _random.NextIntInclusive(
            GovernmentContractBalance.DeliveryBaseUnitsMin,
            GovernmentContractBalance.DeliveryBaseUnitsMax);
        var bonusUnits = (int)System.Math.Round(
            GovernmentContractBalance.DeliveryBonusUnitsScale
            * evaluationResult.BonusProbability
            * GovernmentContractBalance.DeliveryBonusUnitsMultiplier,
            MidpointRounding.AwayFromZero);
        if (bonusUnits < 0)
        {
            bonusUnits = 0;
        }

        var totalUnits = baseUnits + bonusUnits;
        var quote = _pricingService.CalculateQuote(evaluatedContract.Requirements, totalUnits);
        var deliveryId = $"{evaluatedContract.ContractId}.delivery";
        var endsOn = currentDate.AddMonths(GovernmentContractBalance.DeliveryContractDurationYears * 12);

        return new GovernmentDeliveryContract(
            deliveryId,
            evaluatedContract.ContractId,
            acceptedSubmission.DesignId,
            currentDate,
            endsOn,
            totalUnits,
            quote.UnitPrice,
            quote.TotalContractValue);
    }

    private static bool IsOnOrAfter(SimulationDate left, SimulationDate right)
    {
        var leftIndex = ((left.Year - 1) * 12) + (left.Month - 1);
        var rightIndex = ((right.Year - 1) * 12) + (right.Month - 1);
        return leftIndex >= rightIndex;
    }

    private static double RoundCurrency(double value) =>
        System.Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
