using System;
using UnityEngine;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Game.GovernmentContracts;

// Example integration bridge between Unity runtime and pure M9 services.
public sealed class GovernmentContractRuntimeBridgeExample : MonoBehaviour
{
    [SerializeField] private int _randomSeed = 900601;
    [SerializeField] private GovernmentContractNotebookWindow? _notebookWindow;

    private GovernmentContractBoard? _board;

    public GovernmentContractBoard Board
    {
        get
        {
            if (_board is null)
            {
                throw new InvalidOperationException("Government contract board is not initialized.");
            }

            return _board;
        }
    }

    private void Awake()
    {
        var random = new SystemGovernmentContractRandomProvider(_randomSeed);
        var generator = new GovernmentContractGenerator(random);
        _board = new GovernmentContractBoard(generator, random);
    }

    public GovernmentContractMonthlyUpdate ProcessMonth(SimulationDate currentDate)
    {
        var update = Board.ProcessMonth(currentDate);
        if (update.EvaluationResult is not null)
        {
            _notebookWindow?.NotifyContractEvaluated();
        }

        return update;
    }

    public GovernmentContractAcceptanceResult TryAccept(
        SimulationDate currentDate,
        GovernmentContractSubmissionCandidate candidate)
    {
        var result = Board.TryAcceptActiveContract(currentDate, candidate);
        if (result.IsAccepted)
        {
            _notebookWindow?.NotifyContractAccepted();
        }

        return result;
    }

    public GovernmentDeliveryProgressReport ProcessDelivery(
        SimulationDate currentDate,
        IGovernmentContractInventoryGateway inventory)
    {
        return Board.ProcessDeliveryFromInventory(currentDate, inventory);
    }
}
