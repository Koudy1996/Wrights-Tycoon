namespace WrightTycoon.Core.GovernmentContracts;

public sealed record GovernmentDeliveryProgressReport(
    bool HasActiveDeliveryContract,
    int DeliveredUnitsThisMonth,
    int TotalDeliveredUnits,
    int RemainingUnits,
    bool CompletedThisMonth,
    bool EndedThisMonth,
    double CompletionBonusGranted,
    string Message);
