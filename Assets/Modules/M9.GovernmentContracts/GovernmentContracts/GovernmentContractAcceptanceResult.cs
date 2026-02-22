namespace WrightTycoon.Core.GovernmentContracts;

public sealed record GovernmentContractAcceptanceResult(
    bool IsAccepted,
    string Message,
    double UpfrontFundingGranted,
    GovernmentContractComplianceReport? Compliance);
