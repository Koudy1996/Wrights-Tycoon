namespace WrightTycoon.Core.GovernmentContracts;

public readonly record struct GovernmentContractParameterBonus(
    GovernmentContractParameter Parameter,
    double RequiredValue,
    double ActualValue,
    double ExcessRatio,
    double BonusValue);
