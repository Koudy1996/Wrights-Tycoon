using System.Collections.Generic;

namespace WrightTycoon.Core.Contracts;

public interface IContractGenerator
{
    IReadOnlyList<Contract> GenerateMonthlyContracts(int monthIndex);
}
