using System;
using System.Collections.Generic;

namespace WrightTycoon.Core.Contracts;

public sealed class ContractBoard
{
    private readonly IContractGenerator _generator;
    private readonly IContractEvaluator _evaluator;
    private readonly List<Contract> _openContracts = new();
    private readonly List<Contract> _deliveredContracts = new();
    private readonly List<Contract> _canceledContracts = new();

    public ContractBoard(IContractGenerator generator, IContractEvaluator evaluator)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    }

    public int CurrentMonthIndex { get; private set; }

    public IReadOnlyList<Contract> OpenContracts => _openContracts;

    public IReadOnlyList<Contract> DeliveredContracts => _deliveredContracts;

    public IReadOnlyList<Contract> CanceledContracts => _canceledContracts;

    public IReadOnlyList<Contract> GenerateContractsForNextMonth() => AdvanceMonthAndGenerateContracts();

    public IReadOnlyList<Contract> AdvanceMonthAndGenerateContracts()
    {
        CurrentMonthIndex++;
        AgeAndCancelExpiredContracts(CurrentMonthIndex);

        var generated = _generator.GenerateMonthlyContracts(CurrentMonthIndex);
        foreach (var contract in generated)
        {
            if (contract is null)
            {
                throw new InvalidOperationException("Generator produced a null contract.");
            }

            contract.Validate();
        }

        _openContracts.AddRange(generated);
        return generated;
    }

    public int InjectGeneratedContractsForCurrentMonth(IReadOnlyList<Contract> contracts)
    {
        if (contracts is null)
        {
            throw new ArgumentNullException(nameof(contracts));
        }

        var injectedCount = 0;
        for (var index = 0; index < contracts.Count; index++)
        {
            var contract = contracts[index];
            if (contract is null)
            {
                throw new InvalidOperationException("Injected contract cannot be null.");
            }

            contract.Validate();
            if (contract.CreatedMonthIndex != CurrentMonthIndex)
            {
                throw new InvalidOperationException(
                    $"Injected contract '{contract.Id}' has creation month {contract.CreatedMonthIndex}, expected {CurrentMonthIndex}.");
            }

            if (contract.Status != ContractStatus.Open)
            {
                throw new InvalidOperationException($"Injected contract '{contract.Id}' must be open.");
            }

            if (ContainsContractId(contract.Id))
            {
                throw new InvalidOperationException($"Injected contract id '{contract.Id}' already exists.");
            }

            _openContracts.Add(contract);
            injectedCount++;
        }

        return injectedCount;
    }

    public ContractEvaluationReport Evaluate(Contract contract, WrightTycoon.Core.Aircraft.Aircraft aircraft) => _evaluator.Evaluate(contract, aircraft);

    public ContractDeliveryResult TryDeliver(string contractId, WrightTycoon.Core.Aircraft.Aircraft aircraft)
    {
        if (string.IsNullOrWhiteSpace(contractId))
        {
            throw new ArgumentException("Contract id cannot be empty.", nameof(contractId));
        }

        if (aircraft is null)
        {
            throw new ArgumentNullException(nameof(aircraft));
        }

        var contract = FindOpenContractById(contractId);
        if (contract is null)
        {
            return new ContractDeliveryResult(
                contractId,
                false,
                new ContractEvaluationReport(Array.Empty<string>()),
                $"Contract '{contractId}' was not found in open contracts.");
        }

        var evaluation = _evaluator.Evaluate(contract, aircraft);
        if (!evaluation.CanDeliver)
        {
            return new ContractDeliveryResult(
                contract.Id,
                false,
                evaluation,
                $"Contract '{contract.Id}' is not deliverable with current aircraft.");
        }

        contract.MarkDelivered(CurrentMonthIndex);
        _openContracts.Remove(contract);
        _deliveredContracts.Add(contract);

        return new ContractDeliveryResult(
            contract.Id,
            true,
            evaluation,
            $"Delivered {contract.QuantityUnits} units to {contract.CompanyName}.");
    }

    public ContractBoardSnapshot CreateSnapshot()
    {
        var snapshot = new ContractBoardSnapshot
        {
            CurrentMonthIndex = CurrentMonthIndex,
        };

        for (var index = 0; index < _openContracts.Count; index++)
        {
            snapshot.OpenContracts.Add(CreateSnapshot(_openContracts[index]));
        }

        for (var index = 0; index < _deliveredContracts.Count; index++)
        {
            snapshot.DeliveredContracts.Add(CreateSnapshot(_deliveredContracts[index]));
        }

        for (var index = 0; index < _canceledContracts.Count; index++)
        {
            snapshot.CanceledContracts.Add(CreateSnapshot(_canceledContracts[index]));
        }

        return snapshot;
    }

    public void RestoreSnapshot(ContractBoardSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        if (snapshot.CurrentMonthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot.CurrentMonthIndex), "Current month index must be >= 0.");
        }

        _openContracts.Clear();
        _deliveredContracts.Clear();
        _canceledContracts.Clear();

        RestoreContracts(snapshot.OpenContracts, _openContracts, ContractStatus.Open);
        RestoreContracts(snapshot.DeliveredContracts, _deliveredContracts, ContractStatus.Delivered);
        RestoreContracts(snapshot.CanceledContracts, _canceledContracts, ContractStatus.Canceled);

        CurrentMonthIndex = snapshot.CurrentMonthIndex;
    }

    private static void RestoreContracts(
        IReadOnlyList<ContractSnapshot>? snapshots,
        List<Contract> target,
        ContractStatus expectedStatus)
    {
        if (snapshots is null)
        {
            return;
        }

        for (var index = 0; index < snapshots.Count; index++)
        {
            var restored = RestoreContract(snapshots[index], expectedStatus);
            target.Add(restored);
        }
    }

    private static ContractSnapshot CreateSnapshot(Contract contract)
    {
        return new ContractSnapshot
        {
            Id = contract.Id,
            CompanyName = contract.CompanyName,
            QuantityUnits = contract.QuantityUnits,
            DeadlineMonths = contract.DeadlineMonths,
            CreatedMonthIndex = contract.CreatedMonthIndex,
            UnitPrice = contract.UnitPrice,
            EstimatedMinimumUnitCost = contract.EstimatedMinimumUnitCost,
            AppliedMarkup = contract.AppliedMarkup,
            Status = contract.Status,
            RemainingMonths = contract.RemainingMonths,
            DeliveredMonthIndex = contract.DeliveredMonthIndex,
            CanceledMonthIndex = contract.CanceledMonthIndex,
            CancellationReason = contract.CancellationReason,
            Requirements = CreateSnapshot(contract.Requirements),
        };
    }

    private static ContractRequirementsSnapshot CreateSnapshot(ContractRequirements requirements)
    {
        return new ContractRequirementsSnapshot
        {
            RequiredCategory = requirements.RequiredCategory,
            MinimumReliability = requirements.MinimumReliability,
            MaximumTotalWeightKilograms = requirements.MaximumTotalWeightKilograms,
            MinimumRangeKilometers = requirements.MinimumRangeKilometers,
            MinimumPayloadKilograms = requirements.MinimumPayloadKilograms,
            MinimumCrew = requirements.MinimumCrew,
            MinimumSpeedAltitudeMeters = requirements.MinimumSpeed?.AltitudeMeters,
            MinimumSpeedMetersPerSecond = requirements.MinimumSpeed?.MinimumSpeedMetersPerSecond,
            MaximumFuelConsumptionKilogramsPerHour = requirements.MaximumFuelConsumptionKilogramsPerHour,
        };
    }

    private static Contract RestoreContract(ContractSnapshot snapshot, ContractStatus expectedStatus)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var requirements = RestoreRequirements(snapshot.Requirements);
        var contract = new Contract(
            snapshot.Id,
            snapshot.CompanyName,
            snapshot.QuantityUnits,
            snapshot.DeadlineMonths,
            requirements,
            snapshot.CreatedMonthIndex,
            snapshot.UnitPrice,
            snapshot.EstimatedMinimumUnitCost,
            snapshot.AppliedMarkup);

        var targetAdvanceSteps = snapshot.DeadlineMonths - snapshot.RemainingMonths;
        if (targetAdvanceSteps < 0)
        {
            targetAdvanceSteps = 0;
        }

        var monthCursor = snapshot.CreatedMonthIndex;
        for (var step = 0; step < targetAdvanceSteps && contract.Status == ContractStatus.Open; step++)
        {
            monthCursor++;
            contract.AdvanceMonth(monthCursor);
        }

        if (expectedStatus == ContractStatus.Delivered && contract.Status == ContractStatus.Open)
        {
            var deliveredMonth = snapshot.DeliveredMonthIndex ?? monthCursor;
            contract.MarkDelivered(deliveredMonth);
        }
        else if (expectedStatus == ContractStatus.Canceled && contract.Status == ContractStatus.Open)
        {
            var canceledMonth = snapshot.CanceledMonthIndex ?? monthCursor;
            var reason = string.IsNullOrWhiteSpace(snapshot.CancellationReason)
                ? "Canceled after load restore."
                : snapshot.CancellationReason;
            contract.MarkCanceled(canceledMonth, reason);
        }

        return contract;
    }

    private static ContractRequirements RestoreRequirements(ContractRequirementsSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        ContractSpeedRequirement? minimumSpeed = null;
        if (snapshot.MinimumSpeedAltitudeMeters.HasValue && snapshot.MinimumSpeedMetersPerSecond.HasValue)
        {
            minimumSpeed = new ContractSpeedRequirement(
                snapshot.MinimumSpeedAltitudeMeters.Value,
                snapshot.MinimumSpeedMetersPerSecond.Value);
        }

        var requirements = new ContractRequirements
        {
            RequiredCategory = snapshot.RequiredCategory,
            MinimumReliability = snapshot.MinimumReliability,
            MaximumTotalWeightKilograms = snapshot.MaximumTotalWeightKilograms,
            MinimumRangeKilometers = snapshot.MinimumRangeKilometers,
            MinimumSpeed = minimumSpeed,
            MinimumPayloadKilograms = snapshot.MinimumPayloadKilograms,
            MinimumCrew = snapshot.MinimumCrew,
            MaximumFuelConsumptionKilogramsPerHour = snapshot.MaximumFuelConsumptionKilogramsPerHour,
        };

        requirements.Validate();
        return requirements;
    }

    private Contract? FindOpenContractById(string contractId)
    {
        for (var index = 0; index < _openContracts.Count; index++)
        {
            var contract = _openContracts[index];
            if (string.Equals(contract.Id, contractId, StringComparison.Ordinal))
            {
                return contract;
            }
        }

        return null;
    }

    private bool ContainsContractId(string contractId)
    {
        for (var openIndex = 0; openIndex < _openContracts.Count; openIndex++)
        {
            if (string.Equals(_openContracts[openIndex].Id, contractId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        for (var deliveredIndex = 0; deliveredIndex < _deliveredContracts.Count; deliveredIndex++)
        {
            if (string.Equals(_deliveredContracts[deliveredIndex].Id, contractId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        for (var canceledIndex = 0; canceledIndex < _canceledContracts.Count; canceledIndex++)
        {
            if (string.Equals(_canceledContracts[canceledIndex].Id, contractId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void AgeAndCancelExpiredContracts(int monthIndex)
    {
        for (var index = _openContracts.Count - 1; index >= 0; index--)
        {
            var contract = _openContracts[index];
            if (!contract.AdvanceMonth(monthIndex))
            {
                continue;
            }

            _openContracts.RemoveAt(index);
            _canceledContracts.Add(contract);
        }
    }
}
