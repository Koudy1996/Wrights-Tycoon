namespace WrightTycoon.Core.Contracts;

public interface IContractEvaluator
{
    ContractEvaluationReport Evaluate(Contract contract, WrightTycoon.Core.Aircraft.Aircraft aircraft);
}
