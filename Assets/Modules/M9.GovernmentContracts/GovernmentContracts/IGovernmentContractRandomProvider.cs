namespace WrightTycoon.Core.GovernmentContracts;

public interface IGovernmentContractRandomProvider
{
    int NextIntInclusive(int minInclusive, int maxInclusive);

    double NextDouble();

    double NextDouble(double minInclusive, double maxExclusive);
}
