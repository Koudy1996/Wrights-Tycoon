using WrightTycoon.Core.Balance;

namespace WrightTycoon.Core.GovernmentContracts;

public enum GovernmentContractSpeedAltitudeBand
{
    Low = 0,
    Medium = 1,
    High = 2,
}

public static class GovernmentContractSpeedAltitudeBandExtensions
{
    public static double ToAltitudeMeters(this GovernmentContractSpeedAltitudeBand band)
    {
        return band switch
        {
            GovernmentContractSpeedAltitudeBand.Low => GovernmentContractBalance.SpeedAltitudeLowMeters,
            GovernmentContractSpeedAltitudeBand.Medium => GovernmentContractBalance.SpeedAltitudeMediumMeters,
            GovernmentContractSpeedAltitudeBand.High => GovernmentContractBalance.SpeedAltitudeHighMeters,
            _ => GovernmentContractBalance.SpeedAltitudeMediumMeters,
        };
    }
}
