using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.GovernmentContracts;

public interface IGovernmentContractGenerator
{
    GovernmentContract Generate(SimulationDate generatedOn, int contractSequence);
}
