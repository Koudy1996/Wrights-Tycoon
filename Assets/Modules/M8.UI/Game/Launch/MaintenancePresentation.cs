using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Game.Launch;

/// <summary>Aggregates frozen airframe and engine maintenance terms for the fleet UI.</summary>
public static class MaintenancePresentation
{
    /// <summary>Aggregates frozen delivery terms, including guarantees after support has been disabled.</summary>
    public static MaintenanceFleetSummary Summarize(IEnumerable<FleetCohortSnapshot> cohorts,
        IEnumerable<string> designIds, int currentMonth)
    {
        if (cohorts is null) throw new ArgumentNullException(nameof(cohorts));
        if (designIds is null) throw new ArgumentNullException(nameof(designIds));
        var selected = new HashSet<string>(designIds, StringComparer.Ordinal);
        var matching = cohorts.Where(cohort => cohort.ActiveCount > 0 && selected.Contains(cohort.DesignId)).ToArray();
        var guaranteed = matching.Where(cohort => cohort.SupportEndMonth >= currentMonth).ToArray();
        return new MaintenanceFleetSummary(
            matching.Sum(cohort => cohort.ActiveCount),
            matching.Where(cohort => cohort.Grounded).Sum(cohort => cohort.ActiveCount),
            matching.Where(cohort => !cohort.Grounded).Sum(cohort => checked(cohort.ActiveCount * cohort.EngineCount)),
            matching.Where(cohort => cohort.Grounded).Sum(cohort => checked(cohort.ActiveCount * cohort.EngineCount)),
            matching.Select(cohort => cohort.Offer.ServiceIntervalHours).Distinct().OrderBy(value => value).ToArray(),
            matching.Select(cohort => cohort.Offer.ServicePricePerEvent).Distinct().OrderBy(value => value).ToArray(),
            guaranteed.Length == 0 ? -1 : guaranteed.Max(cohort => cohort.SupportEndMonth),
            guaranteed.Length == 0 ? -1 : guaranteed.Min(cohort => cohort.SupportEndMonth),
            guaranteed.Sum(cohort => cohort.ActiveCount),
            guaranteed.Select(cohort => cohort.ContractId).Distinct(StringComparer.Ordinal).Count());
    }


}

public sealed class MaintenanceFleetSummary
{
    public MaintenanceFleetSummary(int activeAircraft, int groundedAircraft, int flyingEngines, int groundedEngines,
        int[] serviceIntervalsHours, double[] servicePrices, int latestSupportEndMonth, int nextSupportEndMonth,
        int guaranteedAircraft, int guaranteedContracts)
    {
        ActiveAircraft = activeAircraft;
        GroundedAircraft = groundedAircraft;
        FlyingEngines = flyingEngines;
        GroundedEngines = groundedEngines;
        ServiceIntervalsHours = serviceIntervalsHours;
        ServicePrices = servicePrices;
        LatestSupportEndMonth = latestSupportEndMonth;
        NextSupportEndMonth = nextSupportEndMonth;
        GuaranteedAircraft = guaranteedAircraft;
        GuaranteedContracts = guaranteedContracts;
    }

    public int ActiveAircraft { get; }
    public int GroundedAircraft { get; }
    public int FlyingEngines { get; }
    public int GroundedEngines { get; }
    public int[] ServiceIntervalsHours { get; }
    public double[] ServicePrices { get; }
    public int LatestSupportEndMonth { get; }
    public int NextSupportEndMonth { get; }
    public int GuaranteedAircraft { get; }
    public int GuaranteedContracts { get; }
}
