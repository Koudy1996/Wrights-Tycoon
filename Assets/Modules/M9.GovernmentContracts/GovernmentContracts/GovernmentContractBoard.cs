using System;
using System.Collections.Generic;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Presentation;
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
    private readonly List<GovernmentDeliveryContract> _activeDeliveryContracts = new();
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
        _pricingService = pricingService ?? new GovernmentContractPricingService();
        _complianceEvaluator = complianceEvaluator ?? new GovernmentContractComplianceEvaluator(_pricingService);
    }

    public GovernmentContract? ActiveContract { get; private set; }

    public GovernmentDeliveryContract? ActiveDeliveryContract =>
        _activeDeliveryContracts.Count == 0
            ? null
            : _activeDeliveryContracts[0];

    public IReadOnlyList<GovernmentDeliveryContract> ActiveDeliveryContracts => _activeDeliveryContracts;

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
                _activeDeliveryContracts.Add(createdDeliveryContract);
                events.Add($"Delivery contract created ({createdDeliveryContract.DeliveryId}) for {createdDeliveryContract.TotalUnitsRequired} units.");
            }
            else
            {
                events.Add($"Government contract failed evaluation ({evaluatedContract.ContractId}).");
            }
        }

        for (var index = _activeDeliveryContracts.Count - 1; index >= 0; index--)
        {
            var deliveryContract = _activeDeliveryContracts[index];
            if (deliveryContract.Status != GovernmentDeliveryContractStatus.Active
                || !IsOnOrAfter(currentDate, deliveryContract.EndsOn)
                || deliveryContract.RemainingUnits <= 0)
            {
                continue;
            }

            deliveryContract.MarkEnded(currentDate);
            _closedDeliveryContracts.Add(deliveryContract);
            events.Add($"Delivery contract ended without full completion ({deliveryContract.DeliveryId}).");
            _activeDeliveryContracts.RemoveAt(index);
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

        var eligibleDevelopmentPrice = submission.DevelopmentPriceBreakdown.TotalPrice > 0d
            ? submission.DevelopmentPriceBreakdown.TotalPrice
            : submission.Metrics.DevelopmentCost;
        var upfrontFunding = eligibleDevelopmentPrice * GovernmentContractBalance.UpfrontFundingShare;
        ActiveContract.Accept(currentDate, submission, upfrontFunding);
        ActiveContract.LockUnitPrice(_pricingService.CalculateQuote(ActiveContract.Requirements, 1).UnitPrice,
            submission.Metrics.CreateContractServiceOffer(ActiveContract.Requirements));

        return new GovernmentContractAcceptanceResult(
            true,
            $"Government contract accepted. Upfront funding granted: {FinancialValueFormatter.FormatNumber(upfrontFunding, "0.00")}.",
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

        var deliveryContract = ActiveDeliveryContract;
        if (deliveryContract is null || deliveryContract.Status != GovernmentDeliveryContractStatus.Active)
        {
            return new GovernmentDeliveryProgressReport(false, 0, 0, 0, false, false, 0d, "No active government delivery contract.");
        }

        var availableUnits = inventory.GetAvailableUnits(deliveryContract.AircraftDesignId);
        var unitsToDeliver = System.Math.Min(availableUnits, deliveryContract.RemainingUnits);
        if (unitsToDeliver <= 0)
        {
            return new GovernmentDeliveryProgressReport(
                true,
                0,
                deliveryContract.DeliveredUnits,
                deliveryContract.RemainingUnits,
                false,
                false,
                0d,
                "No aircraft available in warehouse for automatic government delivery.");
        }

        return DeliverFromInventory(currentDate, deliveryContract.DeliveryId, inventory, unitsToDeliver);
    }

    public bool TryGetActiveDeliveryContract(string deliveryId, out GovernmentDeliveryContract? contract)
    {
        contract = null;
        if (string.IsNullOrWhiteSpace(deliveryId))
        {
            return false;
        }

        for (var index = 0; index < _activeDeliveryContracts.Count; index++)
        {
            var candidate = _activeDeliveryContracts[index];
            if (!string.Equals(candidate.DeliveryId, deliveryId, StringComparison.Ordinal))
            {
                continue;
            }

            contract = candidate;
            return true;
        }

        return false;
    }

    public GovernmentDeliveryProgressReport DeliverFromInventory(
        SimulationDate currentDate,
        string deliveryId,
        IGovernmentContractInventoryGateway inventory,
        int requestedUnits)
    {
        if (inventory is null)
        {
            throw new ArgumentNullException(nameof(inventory));
        }

        if (string.IsNullOrWhiteSpace(deliveryId))
        {
            return new GovernmentDeliveryProgressReport(false, 0, 0, 0, false, false, 0d, "Delivery id is required.");
        }

        if (!TryGetActiveDeliveryContract(deliveryId, out var activeDelivery) || activeDelivery is null)
        {
            return new GovernmentDeliveryProgressReport(false, 0, 0, 0, false, false, 0d, $"Delivery contract '{deliveryId}' was not found.");
        }

        if (activeDelivery.Status != GovernmentDeliveryContractStatus.Active)
        {
            return new GovernmentDeliveryProgressReport(false, 0, 0, 0, false, false, 0d, $"Delivery contract '{deliveryId}' is not active.");
        }

        if (IsOnOrAfter(currentDate, activeDelivery.EndsOn) && activeDelivery.RemainingUnits > 0)
        {
            activeDelivery.MarkEnded(currentDate);
            _closedDeliveryContracts.Add(activeDelivery);
            _activeDeliveryContracts.Remove(activeDelivery);
            return new GovernmentDeliveryProgressReport(false, 0, 0, 0, false, true, 0d, $"Delivery contract '{deliveryId}' ended with incomplete delivery.");
        }

        if (requestedUnits <= 0)
        {
            return new GovernmentDeliveryProgressReport(
                true,
                0,
                activeDelivery.DeliveredUnits,
                activeDelivery.RemainingUnits,
                false,
                false,
                0d,
                "Requested delivery units must be greater than zero.");
        }

        var availableUnits = inventory.GetAvailableUnits(activeDelivery.AircraftDesignId);
        if (availableUnits <= 0)
        {
            return new GovernmentDeliveryProgressReport(
                true,
                0,
                activeDelivery.DeliveredUnits,
                activeDelivery.RemainingUnits,
                false,
                false,
                0d,
                "No aircraft available in warehouse for delivery.");
        }

        var unitsToConsume = System.Math.Min(requestedUnits, System.Math.Min(availableUnits, activeDelivery.RemainingUnits));
        if (unitsToConsume <= 0)
        {
            return new GovernmentDeliveryProgressReport(
                true,
                0,
                activeDelivery.DeliveredUnits,
                activeDelivery.RemainingUnits,
                false,
                false,
                0d,
                "Delivery contract is already satisfied.");
        }

        if (!inventory.TryConsumeUnits(activeDelivery.AircraftDesignId, unitsToConsume, out var consumeMessage))
        {
            return new GovernmentDeliveryProgressReport(
                true,
                0,
                activeDelivery.DeliveredUnits,
                activeDelivery.RemainingUnits,
                false,
                false,
                0d,
                $"Warehouse consumption failed: {consumeMessage}");
        }

        var deliveredNow = activeDelivery.RegisterDeliveredUnits(unitsToConsume, currentDate);
        var completionBonusGranted = 0d;
        var completedThisMonth = false;
        if (activeDelivery.Status == GovernmentDeliveryContractStatus.Completed)
        {
            completedThisMonth = true;
            if (!activeDelivery.CompletionBonusGranted)
            {
                completionBonusGranted = RoundCurrency(
                    activeDelivery.TotalContractValue * GovernmentContractBalance.DeliveryCompletionBonusShare);
                activeDelivery.MarkCompletionBonusGranted();
            }

            _closedDeliveryContracts.Add(activeDelivery);
            _activeDeliveryContracts.Remove(activeDelivery);
        }

        var hasActiveAfterDelivery = activeDelivery.Status == GovernmentDeliveryContractStatus.Active;
        var totalDelivered = hasActiveAfterDelivery ? activeDelivery.DeliveredUnits : 0;
        var remainingUnits = hasActiveAfterDelivery ? activeDelivery.RemainingUnits : 0;

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

    public GovernmentContractBoardSnapshot CreateSnapshot()
    {
        var snapshot = new GovernmentContractBoardSnapshot
        {
            ContractSequence = _contractSequence,
            ActiveContract = ActiveContract is null ? null : CreateSnapshot(ActiveContract),
        };

        for (var index = 0; index < _expiredContracts.Count; index++)
        {
            snapshot.ExpiredContracts.Add(CreateSnapshot(_expiredContracts[index]));
        }

        for (var index = 0; index < _evaluatedContracts.Count; index++)
        {
            snapshot.EvaluatedContracts.Add(CreateSnapshot(_evaluatedContracts[index]));
        }

        for (var index = 0; index < _activeDeliveryContracts.Count; index++)
        {
            snapshot.ActiveDeliveryContracts.Add(CreateSnapshot(_activeDeliveryContracts[index]));
        }

        for (var index = 0; index < _closedDeliveryContracts.Count; index++)
        {
            snapshot.ClosedDeliveryContracts.Add(CreateSnapshot(_closedDeliveryContracts[index]));
        }

        return snapshot;
    }

    public void RestoreSnapshot(GovernmentContractBoardSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        _contractSequence = System.Math.Max(snapshot.ContractSequence, 0);
        ActiveContract = snapshot.ActiveContract is null
            ? null
            : RestoreContract(snapshot.ActiveContract);

        _expiredContracts.Clear();
        if (snapshot.ExpiredContracts is not null)
        {
            for (var index = 0; index < snapshot.ExpiredContracts.Count; index++)
            {
                _expiredContracts.Add(RestoreContract(snapshot.ExpiredContracts[index]));
            }
        }

        _evaluatedContracts.Clear();
        if (snapshot.EvaluatedContracts is not null)
        {
            for (var index = 0; index < snapshot.EvaluatedContracts.Count; index++)
            {
                _evaluatedContracts.Add(RestoreContract(snapshot.EvaluatedContracts[index]));
            }
        }

        _activeDeliveryContracts.Clear();
        if (snapshot.ActiveDeliveryContracts is not null)
        {
            for (var index = 0; index < snapshot.ActiveDeliveryContracts.Count; index++)
            {
                _activeDeliveryContracts.Add(RestoreDeliveryContract(snapshot.ActiveDeliveryContracts[index]));
            }
        }

        _closedDeliveryContracts.Clear();
        if (snapshot.ClosedDeliveryContracts is not null)
        {
            for (var index = 0; index < snapshot.ClosedDeliveryContracts.Count; index++)
            {
                _closedDeliveryContracts.Add(RestoreDeliveryContract(snapshot.ClosedDeliveryContracts[index]));
            }
        }
    }

    private GovernmentContractEvaluationResult EvaluateAcceptedContract(
        GovernmentContract contract,
        SimulationDate currentDate)
    {
        var acceptedSubmission = contract.AcceptedSubmission
            ?? throw new InvalidOperationException("Accepted contract does not contain selected submission.");

        var compliance = _complianceEvaluator.Evaluate(contract.Requirements, acceptedSubmission, contract.LockedUnitPrice, contract.LockedLifecycleCostQuote,
            serviceIntervalAlreadyAccepted: true);
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

        var unitPrice = evaluatedContract.LockedUnitPrice ?? quote.UnitPrice;
        return new GovernmentDeliveryContract(
            deliveryId,
            evaluatedContract.ContractId,
            acceptedSubmission.DesignId,
            currentDate,
            endsOn,
            totalUnits,
            unitPrice,
            unitPrice * totalUnits,
            evaluatedContract.Requirements,
            acceptedSubmission.Metrics,
            evaluatedContract.LockedServiceOffer,
            evaluatedContract.LockedLifecycleCostQuote);
    }

    private static GovernmentContractSnapshot CreateSnapshot(GovernmentContract contract)
    {
        var snapshot = new GovernmentContractSnapshot
        {
            ContractId = contract.ContractId,
            GeneratedOnYear = contract.GeneratedOn.Year,
            GeneratedOnMonth = contract.GeneratedOn.Month,
            Status = contract.Status,
            Requirements = CreateSnapshot(contract.Requirements),
            UpfrontFundingGranted = contract.UpfrontFundingGranted,
            LockedUnitPrice = contract.LockedUnitPrice,
            LockedServiceOffer = contract.LockedServiceOffer,
            LockedLifecycleCostQuote = contract.LockedLifecycleCostQuote,
        };

        if (contract.AcceptedOn.HasValue)
        {
            snapshot.AcceptedOnYear = contract.AcceptedOn.Value.Year;
            snapshot.AcceptedOnMonth = contract.AcceptedOn.Value.Month;
        }

        if (contract.AcceptedSubmission is not null)
        {
            snapshot.AcceptedSubmission = CreateSnapshot(contract.AcceptedSubmission);
        }

        if (contract.EvaluationResult is not null)
        {
            snapshot.EvaluationResult = CreateSnapshot(contract.EvaluationResult);
        }

        return snapshot;
    }

    private static GovernmentContractRequirementsSnapshot CreateSnapshot(GovernmentContractRequirements requirements)
    {
        return new GovernmentContractRequirementsSnapshot
        {
            EconomicSnapshot = requirements.EconomicSnapshot,
            RequiredAircraftType = requirements.RequiredAircraftType,
            LifecycleModelVersion = requirements.LifecycleModelVersion,
            MinimumExpectedLifeHours = requirements.MinimumExpectedLifeHours,
            MaximumServiceIntervalHours = requirements.MaximumServiceIntervalHours,
            MinimumSupportMonths = requirements.MinimumSupportMonths,
            UtilizationHoursPerMonth = requirements.UtilizationHoursPerMonth,
            AverageFlightHours = requirements.AverageFlightHours,
            OperatingProfileId = requirements.OperatingProfileId,
            ProcurementPreference = requirements.ProcurementPreference,
            MaximumOperatingCostPerUnit = requirements.MaximumOperatingCostPerUnit,
            SpeedAltitudeBand = requirements.SpeedAltitudeBand,
            MinimumMaximumSpeedMetersPerSecond = requirements.MinimumMaximumSpeedMetersPerSecond,
            MinimumRangeKilometers = requirements.MinimumRangeKilometers,
            MinimumPayloadKilograms = requirements.MinimumPayloadKilograms,
            MinimumReliability = requirements.MinimumReliability,
            MinimumManeuverability = requirements.MinimumManeuverability,
            MinimumCrewSize = requirements.MinimumCrewSize,
            MaximumRequiredRunwayLengthMeters = requirements.MaximumRequiredRunwayLengthMeters,
        };
    }

    private static GovernmentContractSubmissionCandidateSnapshot CreateSnapshot(GovernmentContractSubmissionCandidate submission)
    {
        return new GovernmentContractSubmissionCandidateSnapshot
        {
            DesignId = submission.DesignId,
            DisplayName = submission.DisplayName,
            LifecycleStage = submission.LifecycleStage,
            Metrics = CreateSnapshot(submission.Metrics),
            DevelopmentPriceBreakdown = CreateSnapshot(submission.DevelopmentPriceBreakdown),
        };
    }

    private static GovernmentContractAircraftMetricsSnapshot CreateSnapshot(GovernmentContractAircraftMetrics metrics)
    {
        var snapshot = new GovernmentContractAircraftMetricsSnapshot
        {
            AircraftType = metrics.AircraftType,
            ExpectedLifeHours = metrics.ExpectedLifeHours,
            ServiceIntervalHours = metrics.ServiceIntervalHours,
            GuaranteedSupportMonths = metrics.GuaranteedSupportMonths,
            FuelBurnKilogramsPerHour = metrics.FuelBurnKilogramsPerHour,
            UnitProductionCost = metrics.UnitProductionCost,
            FuelPricePerKilogram = metrics.FuelPricePerKilogram,
            ServicePricePerEvent = metrics.ServicePricePerEvent,
            MaintenanceModelVersion = metrics.MaintenanceModelVersion,
            CombinedServiceOffer = metrics.CombinedServiceOffer,
            UsesDynamicCategoryQualification = true,
            MaximumSpeedLowAltitudeMetersPerSecond = metrics.MaximumSpeedLowAltitudeMetersPerSecond,
            MaximumSpeedMediumAltitudeMetersPerSecond = metrics.MaximumSpeedMediumAltitudeMetersPerSecond,
            MaximumSpeedHighAltitudeMetersPerSecond = metrics.MaximumSpeedHighAltitudeMetersPerSecond,
            RangeKilometers = metrics.RangeKilometers,
            PayloadKilograms = metrics.PayloadKilograms,
            Reliability = metrics.Reliability,
            Maneuverability = metrics.Maneuverability,
            CrewSize = metrics.CrewSize,
            RequiredRunwayLengthMeters = metrics.RequiredRunwayLengthMeters,
            DevelopmentCost = metrics.DevelopmentCost,
        };

        if (metrics.QualifiedAircraftTypes is not null)
        {
            for (var index = 0; index < metrics.QualifiedAircraftTypes.Count; index++)
            {
                snapshot.QualifiedAircraftTypes.Add(metrics.QualifiedAircraftTypes[index]);
            }
        }

        return snapshot;
    }

    private static GovernmentContractDevelopmentPriceBreakdownSnapshot CreateSnapshot(
        GovernmentContractDevelopmentPriceBreakdown breakdown)
    {
        return new GovernmentContractDevelopmentPriceBreakdownSnapshot
        {
            EnginePrice = breakdown.EnginePrice,
            FamilyPrice = breakdown.FamilyPrice,
            TypePrice = breakdown.TypePrice,
        };
    }

    private static GovernmentContractEvaluationResultSnapshot CreateSnapshot(GovernmentContractEvaluationResult evaluationResult)
    {
        var snapshot = new GovernmentContractEvaluationResultSnapshot
        {
            EvaluatedOnYear = evaluationResult.EvaluatedOn.Year,
            EvaluatedOnMonth = evaluationResult.EvaluatedOn.Month,
            IsSuccess = evaluationResult.IsSuccess,
            FinalProbability = evaluationResult.FinalProbability,
            RandomRoll = evaluationResult.RandomRoll,
        };

        for (var index = 0; index < evaluationResult.ParameterBonuses.Count; index++)
        {
            var bonus = evaluationResult.ParameterBonuses[index];
            snapshot.ParameterBonuses.Add(new GovernmentContractParameterBonusSnapshot
            {
                Parameter = bonus.Parameter,
                RequiredValue = bonus.RequiredValue,
                ActualValue = bonus.ActualValue,
                ExcessRatio = bonus.ExcessRatio,
                BonusValue = bonus.BonusValue,
            });
        }

        return snapshot;
    }

    private static GovernmentDeliveryContractSnapshot CreateSnapshot(GovernmentDeliveryContract deliveryContract)
    {
        var snapshot = new GovernmentDeliveryContractSnapshot
        {
            DeliveryId = deliveryContract.DeliveryId,
            GovernmentContractId = deliveryContract.GovernmentContractId,
            AircraftDesignId = deliveryContract.AircraftDesignId,
            StartedOnYear = deliveryContract.StartedOn.Year,
            StartedOnMonth = deliveryContract.StartedOn.Month,
            EndsOnYear = deliveryContract.EndsOn.Year,
            EndsOnMonth = deliveryContract.EndsOn.Month,
            TotalUnitsRequired = deliveryContract.TotalUnitsRequired,
            DeliveredUnits = deliveryContract.DeliveredUnits,
            UnitPrice = deliveryContract.UnitPrice,
            ServiceOffer = deliveryContract.ServiceOffer,
            LifecycleCostQuote = deliveryContract.LifecycleCostQuote,
            LifecycleRequirements = deliveryContract.LifecycleRequirements is null ? null : CreateSnapshot(deliveryContract.LifecycleRequirements),
            LifecycleAircraftMetrics = deliveryContract.LifecycleAircraftMetrics.HasValue ? CreateSnapshot(deliveryContract.LifecycleAircraftMetrics.Value) : null,
            TotalContractValue = deliveryContract.TotalContractValue,
            Status = deliveryContract.Status,
            CompletionBonusGranted = deliveryContract.CompletionBonusGranted,
        };

        if (deliveryContract.CompletedOn.HasValue)
        {
            snapshot.CompletedOnYear = deliveryContract.CompletedOn.Value.Year;
            snapshot.CompletedOnMonth = deliveryContract.CompletedOn.Value.Month;
        }

        return snapshot;
    }

    private static GovernmentContract RestoreContract(GovernmentContractSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var generatedOn = CreateDate(snapshot.GeneratedOnYear, snapshot.GeneratedOnMonth);
        var requirements = RestoreRequirements(snapshot.Requirements);
        var contract = new GovernmentContract(snapshot.ContractId, generatedOn, requirements);

        if (snapshot.Status == GovernmentContractStatus.Expired)
        {
            contract.Expire();
            return contract;
        }

        if (snapshot.Status is GovernmentContractStatus.Accepted or GovernmentContractStatus.Evaluated)
        {
            if (!snapshot.AcceptedOnYear.HasValue || !snapshot.AcceptedOnMonth.HasValue || snapshot.AcceptedSubmission is null)
            {
                throw new InvalidOperationException($"Snapshot for contract '{snapshot.ContractId}' is missing acceptance data.");
            }

            var acceptedOn = CreateDate(snapshot.AcceptedOnYear.Value, snapshot.AcceptedOnMonth.Value);
            var acceptedSubmission = RestoreSubmission(snapshot.AcceptedSubmission);
            contract.Accept(acceptedOn, acceptedSubmission, snapshot.UpfrontFundingGranted);
            if (snapshot.LockedUnitPrice.HasValue) contract.LockUnitPrice(snapshot.LockedUnitPrice.Value, snapshot.LockedServiceOffer, snapshot.LockedLifecycleCostQuote);
        }

        if (snapshot.Status == GovernmentContractStatus.Evaluated)
        {
            if (snapshot.EvaluationResult is null)
            {
                throw new InvalidOperationException($"Snapshot for contract '{snapshot.ContractId}' is missing evaluation result.");
            }

            var evaluationResult = RestoreEvaluationResult(snapshot.EvaluationResult);
            contract.SetEvaluationResult(evaluationResult);
        }

        return contract;
    }

    private static GovernmentContractRequirements RestoreRequirements(GovernmentContractRequirementsSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        return new GovernmentContractRequirements
        {
            RequiredAircraftType = snapshot.RequiredAircraftType,
            EconomicSnapshot = snapshot.EconomicSnapshot,
            LifecycleModelVersion = snapshot.LifecycleModelVersion,
            MinimumExpectedLifeHours = snapshot.MinimumExpectedLifeHours,
            MaximumServiceIntervalHours = snapshot.MaximumServiceIntervalHours,
            MinimumSupportMonths = snapshot.MinimumSupportMonths,
            UtilizationHoursPerMonth = snapshot.UtilizationHoursPerMonth,
            AverageFlightHours = snapshot.AverageFlightHours,
            OperatingProfileId = snapshot.OperatingProfileId,
            ProcurementPreference = snapshot.ProcurementPreference,
            MaximumOperatingCostPerUnit = snapshot.MaximumOperatingCostPerUnit,
            SpeedAltitudeBand = snapshot.SpeedAltitudeBand,
            MinimumMaximumSpeedMetersPerSecond = snapshot.MinimumMaximumSpeedMetersPerSecond,
            MinimumRangeKilometers = snapshot.MinimumRangeKilometers,
            MinimumPayloadKilograms = snapshot.MinimumPayloadKilograms,
            MinimumReliability = snapshot.MinimumReliability,
            MinimumManeuverability = snapshot.MinimumManeuverability,
            MinimumCrewSize = snapshot.MinimumCrewSize,
            MaximumRequiredRunwayLengthMeters = snapshot.MaximumRequiredRunwayLengthMeters,
        };
    }

    private static GovernmentContractSubmissionCandidate RestoreSubmission(GovernmentContractSubmissionCandidateSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var metrics = RestoreMetrics(snapshot.Metrics);
        var breakdown = RestoreBreakdown(snapshot.DevelopmentPriceBreakdown);
        return new GovernmentContractSubmissionCandidate(
            snapshot.DesignId,
            snapshot.DisplayName,
            snapshot.LifecycleStage,
            metrics,
            breakdown);
    }

    private static GovernmentContractAircraftMetrics RestoreMetrics(GovernmentContractAircraftMetricsSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        IReadOnlyList<AircraftCategory>? qualifiedTypes = snapshot.UsesDynamicCategoryQualification
            ? (IReadOnlyList<AircraftCategory>?)snapshot.QualifiedAircraftTypes ?? Array.Empty<AircraftCategory>()
            : null;

        return new GovernmentContractAircraftMetrics(
            snapshot.AircraftType,
            snapshot.MaximumSpeedLowAltitudeMetersPerSecond,
            snapshot.MaximumSpeedMediumAltitudeMetersPerSecond,
            snapshot.MaximumSpeedHighAltitudeMetersPerSecond,
            snapshot.RangeKilometers,
            snapshot.PayloadKilograms,
            snapshot.Reliability,
            snapshot.Maneuverability,
            snapshot.CrewSize,
            snapshot.RequiredRunwayLengthMeters,
            snapshot.DevelopmentCost,
            qualifiedTypes,
            snapshot.ExpectedLifeHours,
            snapshot.ServiceIntervalHours,
            snapshot.GuaranteedSupportMonths,
            snapshot.FuelBurnKilogramsPerHour,
            snapshot.UnitProductionCost,
            snapshot.FuelPricePerKilogram,
            snapshot.ServicePricePerEvent,
            snapshot.MaintenanceModelVersion,
            snapshot.CombinedServiceOffer);
    }

    private static GovernmentContractDevelopmentPriceBreakdown RestoreBreakdown(
        GovernmentContractDevelopmentPriceBreakdownSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return GovernmentContractDevelopmentPriceBreakdown.FromTotal(0d);
        }

        return new GovernmentContractDevelopmentPriceBreakdown(
            snapshot.EnginePrice,
            snapshot.FamilyPrice,
            snapshot.TypePrice);
    }

    private static GovernmentContractEvaluationResult RestoreEvaluationResult(GovernmentContractEvaluationResultSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var bonuses = new List<GovernmentContractParameterBonus>();
        if (snapshot.ParameterBonuses is not null)
        {
            for (var index = 0; index < snapshot.ParameterBonuses.Count; index++)
            {
                var bonus = snapshot.ParameterBonuses[index];
                if (bonus is null)
                {
                    continue;
                }

                bonuses.Add(new GovernmentContractParameterBonus(
                    bonus.Parameter,
                    bonus.RequiredValue,
                    bonus.ActualValue,
                    bonus.ExcessRatio,
                    bonus.BonusValue));
            }
        }

        var evaluatedOn = CreateDate(snapshot.EvaluatedOnYear, snapshot.EvaluatedOnMonth);
        return new GovernmentContractEvaluationResult(
            evaluatedOn,
            snapshot.IsSuccess,
            snapshot.FinalProbability,
            snapshot.RandomRoll,
            bonuses);
    }

    private static GovernmentDeliveryContract RestoreDeliveryContract(GovernmentDeliveryContractSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var startedOn = CreateDate(snapshot.StartedOnYear, snapshot.StartedOnMonth);
        var endsOn = CreateDate(snapshot.EndsOnYear, snapshot.EndsOnMonth);
        var restored = new GovernmentDeliveryContract(
            snapshot.DeliveryId,
            snapshot.GovernmentContractId,
            snapshot.AircraftDesignId,
            startedOn,
            endsOn,
            snapshot.TotalUnitsRequired,
            snapshot.UnitPrice,
            snapshot.TotalContractValue,
            snapshot.LifecycleRequirements is null ? null : RestoreRequirements(snapshot.LifecycleRequirements),
            snapshot.LifecycleAircraftMetrics is null ? (GovernmentContractAircraftMetrics?)null : RestoreMetrics(snapshot.LifecycleAircraftMetrics),
            snapshot.ServiceOffer,
            snapshot.LifecycleCostQuote);

        var deliveredUnits = snapshot.DeliveredUnits;
        if (deliveredUnits > 0)
        {
            var effectiveDate = snapshot.CompletedOnYear.HasValue && snapshot.CompletedOnMonth.HasValue
                ? CreateDate(snapshot.CompletedOnYear.Value, snapshot.CompletedOnMonth.Value)
                : startedOn;
            restored.RegisterDeliveredUnits(deliveredUnits, effectiveDate);
        }

        if (snapshot.Status == GovernmentDeliveryContractStatus.Ended && restored.Status == GovernmentDeliveryContractStatus.Active)
        {
            var endedOn = snapshot.CompletedOnYear.HasValue && snapshot.CompletedOnMonth.HasValue
                ? CreateDate(snapshot.CompletedOnYear.Value, snapshot.CompletedOnMonth.Value)
                : endsOn;
            restored.MarkEnded(endedOn);
        }
        else if (snapshot.Status == GovernmentDeliveryContractStatus.Completed && restored.Status != GovernmentDeliveryContractStatus.Completed)
        {
            var completedOn = snapshot.CompletedOnYear.HasValue && snapshot.CompletedOnMonth.HasValue
                ? CreateDate(snapshot.CompletedOnYear.Value, snapshot.CompletedOnMonth.Value)
                : endsOn;
            restored.RegisterDeliveredUnits(restored.RemainingUnits, completedOn);
        }

        if (snapshot.CompletionBonusGranted && !restored.CompletionBonusGranted)
        {
            restored.MarkCompletionBonusGranted();
        }

        return restored;
    }

    private static SimulationDate CreateDate(int year, int month)
    {
        var normalizedYear = year <= 0 ? 1 : year;
        var normalizedMonth = month < 1
            ? 1
            : month > 12
                ? 12
                : month;
        return new SimulationDate(normalizedYear, normalizedMonth);
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
