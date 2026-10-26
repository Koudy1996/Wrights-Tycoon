using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Fleet;

public readonly record struct SimulationMonthId(int Value)
{
    public static SimulationMonthId FromDate(SimulationDate date) => new(checked((date.Year - 1) * 12 + date.Month - 1));
}

public sealed record FleetCohortSnapshot(string CohortId, string DeliveryId, string DesignId, string DesignName,
    string ContractId, int OriginalCount, int ActiveCount, int AccidentLosses, int RetiredCount,
    int DeliveryMonth, int SupportEndMonth, AircraftLifecycleSpecification Specification, OperatingProfile Profile,
    ServiceOfferSnapshot Offer, double ContractLifeHours, double RetirementHours, int EngineCount,
    double FlightHours, double Wear, int CompletedServiceCycles, bool Grounded, bool SupportEnabled,
    int LastProcessedMonth, int SplitSequence)
{
    public int LifetimePolicyVersion { get; init; }
    public double EngineHoursSinceReplacement { get; init; }
    public int CompletedEngineServiceCycles { get; init; }
}

public sealed record FleetRegistrySnapshot(ulong AccidentRandomState, string[] RegisteredDeliveryIds,
    FleetCohortSnapshot[] Cohorts, int LastProcessedMonth);
public sealed record FleetServiceDemand(string DesignId, int AirframeSpareParts, bool Guaranteed)
{
    public string AirframePartsGroupId { get; init; } = string.Empty;
}
public sealed record SupportForecastPoint(int MonthIndex, int GuaranteedUnits, int ActiveUnits, int AirframeSpareParts);

public sealed record EngineFleetDemand(string EngineDesignId, string PartsGroupId, int EngineParts,
    int ReplacementEngines, bool Guaranteed);

/// <summary>Each result is fully serviced aircraft; all installed engines must be fulfilled together.</summary>
public interface IFleetEngineServiceProvider
{
    int CompleteAirframeServices(string groupId, int requestedAircraft, int partsPerAircraft);
    int CompleteEngineMaintenance(string engineDesignId, string groupId, int requestedAircraft,
        int engineCount, int partsPerEngine, bool replacement);
}

public interface IFleetMaintenanceCostProvider
{
    double LastConsumptionCost { get; }
}

public sealed record FleetMaintenanceResult(double Revenue, double ProductionCost)
{
    public double Profit => Revenue - ProductionCost;
}

// Value equality keeps deterministic monthly reports comparable across save/replay.
internal sealed class FleetMaintenanceLedger : Dictionary<string, FleetMaintenanceResult>
{
    public override bool Equals(object? obj) => obj is IReadOnlyDictionary<string, FleetMaintenanceResult> other
        && Count == other.Count && this.All(p => other.TryGetValue(p.Key, out var value) && value == p.Value);
    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var pair in this) hash ^= HashCode.Combine(pair.Key, pair.Value);
        return hash;
    }
}

public sealed record GuaranteeMaintenanceDemand(long AirframeParts, long EngineParts, long ReplacementEngines);

public sealed record FleetMonthReport(int MonthIndex, double AircraftHours, int RequestedServices, int CompletedServices,
    int OverdueServices, int GuaranteedShortfall, int TechnicalLosses, int OperationalLosses, int Retirements,
    int GroundedUnits, double Revenue)
{
    public int ActiveAircraftAtStart { get; init; }
    public int WarrantyAircraft { get; init; }
    public int WarrantyServicedAircraft { get; init; }
    public int WarrantyMissedAircraft { get; init; }
    public double ExpectedAccidentLosses { get; init; }
    public double ProductionCost { get; init; }
    public IReadOnlyDictionary<string, FleetMaintenanceResult> MaintenanceByDesign { get; init; } = new FleetMaintenanceLedger();
    public int AccidentLosses => TechnicalLosses + OperationalLosses;
}

/// <summary>Compact phase-preserving cohorts. Servicing is mandatory at each threshold; unpaid aircraft ground,
/// so they accrue neither fictitious exposure nor repeat invoices. New deliveries start flying next month.</summary>
public sealed class FleetRegistry
{
    private readonly List<FleetCohortSnapshot> cohorts = new();
    private readonly HashSet<string> deliveryIds = new(StringComparer.Ordinal);
    private ulong randomState;
    private int lastProcessedMonth = -1;
    public FleetRegistry(ulong seed = 0x575459434F4F4EUL) => randomState = seed == 0 ? 1UL : seed;
    public IReadOnlyList<FleetCohortSnapshot> Cohorts => cohorts.AsReadOnly();
    public int ActiveUnits => cohorts.Sum(c => c.ActiveCount);

    public bool RegisterDelivery(string deliveryId, string designId, string designName, string contractId, int count,
        SimulationDate deliveryDate, AircraftLifecycleSpecification specification, OperatingProfile profile,
        ServiceOfferSnapshot offer, double contractLifeHours, int engineCount = 1)
    {
        if (string.IsNullOrWhiteSpace(deliveryId) || string.IsNullOrWhiteSpace(designId)) throw new ArgumentException("Stable delivery and design IDs are required.");
        if (deliveryIds.Contains(deliveryId)) return false;
        if (count <= 0 || engineCount <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        specification.Validate(); profile.Validate(); offer.Validate();
        if (offer.EngineCount != engineCount)
            throw new ArgumentException("Frozen maintenance engine count must match installed engines.", nameof(engineCount));
        if (offer.ServiceIntervalHours != specification.ServiceIntervalHours) throw new ArgumentException("Freeze the specification for the agreed interval.");
        if (double.IsNaN(contractLifeHours) || contractLifeHours <= 0 || contractLifeHours > specification.ExpectedLifeHours) throw new ArgumentOutOfRangeException(nameof(contractLifeHours));
        var month = SimulationMonthId.FromDate(deliveryDate).Value;
        // Twenty immutable retirement bins; deterministic per-delivery rotation avoids a size/seed-dependent global stream.
        var bins = System.Math.Min(20, count);
        var rotation = (int)(StableHash(deliveryId) % (uint)bins);
        for (var i = 0; i < bins; i++)
        {
            var units = count / bins + (i < count % bins ? 1 : 0);
            var threshold = (contractLifeHours + specification.ExpectedLifeHours) * .5d
                * (.9d + .2d * (((i + rotation) % bins) + .5d) / bins);
            cohorts.Add(new FleetCohortSnapshot(deliveryId + "/" + i, deliveryId, designId, designName, contractId,
                units, units, 0, 0, month, checked(month + offer.GuaranteedSupportMonths - 1), specification,
                profile, offer, contractLifeHours, threshold, engineCount, 0, 0, 0, false, true, month, 0) { LifetimePolicyVersion = 1 });
        }
        deliveryIds.Add(deliveryId);
        return true;
    }

    public IReadOnlyList<FleetServiceDemand> GetServiceDemand(SimulationDate date)
    {
        var month = SimulationMonthId.FromDate(date).Value;
        var result = new List<FleetServiceDemand>();
        foreach (var c in cohorts.Where(c => c.ActiveCount > 0 && c.SupportEnabled && c.DeliveryMonth < month))
        {
            var end = System.Math.Min(c.RetirementHours - .000001d, c.FlightHours + c.Profile.UtilizationHoursPerMonth);
            var lastCycle = (int)System.Math.Floor(System.Math.Max(c.FlightHours, end) / c.Offer.ServiceIntervalHours);
            var cycles = System.Math.Max(0, lastCycle - c.CompletedServiceCycles);
            if (cycles > 0)
                result.Add(new FleetServiceDemand(c.DesignId, checked(cycles * c.ActiveCount * c.Offer.SparePartsPerService),
                    month <= c.SupportEndMonth) { AirframePartsGroupId = c.Offer.AirframePartsGroupId });
        }
        return result.GroupBy(d => new { d.DesignId, d.Guaranteed, d.AirframePartsGroupId }).Select(g =>
            new FleetServiceDemand(g.Key.DesignId, g.Sum(d => d.AirframeSpareParts), g.Key.Guaranteed)
                { AirframePartsGroupId = g.Key.AirframePartsGroupId }).ToArray();
    }

    public IReadOnlyList<EngineFleetDemand> GetEngineServiceDemand(SimulationDate date, string? designId = null)
    {
        var month = SimulationMonthId.FromDate(date).Value;
        var result = new List<EngineFleetDemand>();
        foreach (var c in cohorts.Where(c => c.ActiveCount > 0 && c.SupportEnabled && c.DeliveryMonth < month && (designId == null || c.DesignId == designId)))
        {
            var terms = c.Offer.EngineMaintenance!;
            var hours = System.Math.Max(0d, System.Math.Min(c.Profile.UtilizationHoursPerMonth, c.RetirementHours - c.FlightHours - 1e-6));
            var events = ForecastEngineEvents(c, hours);
            if (events.services + events.replacements == 0) continue;
            result.Add(new EngineFleetDemand(terms.EngineDesignId, terms.PartsGroupId,
                checked(events.services * c.ActiveCount * c.EngineCount * terms.PartsPerService),
                checked(events.replacements * c.ActiveCount * c.EngineCount), month <= c.SupportEndMonth));
        }
        return result.GroupBy(d => new { d.EngineDesignId, d.PartsGroupId, d.Guaranteed }).Select(g =>
            new EngineFleetDemand(g.Key.EngineDesignId, g.Key.PartsGroupId, g.Sum(d => d.EngineParts),
                g.Sum(d => d.ReplacementEngines), g.Key.Guaranteed)).ToArray();
    }

    public FleetMonthReport ProcessMonth(SimulationDate date, IFleetEngineServiceProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        var month = SimulationMonthId.FromDate(date).Value;
        if (month <= lastProcessedMonth) throw new InvalidOperationException("Fleet month already processed or out of order.");
        double hours = 0, revenue = 0, productionCost = 0, expectedLosses = 0;
        int requested = 0, completed = 0, overdue = 0, guaranteedShortfall = 0, technical = 0, operational = 0, retired = 0;
        var output = new List<FleetCohortSnapshot>();
        var maintenance = new FleetMaintenanceLedger();
        int activeAtStart = 0, warrantyAircraft = 0, warrantyServiced = 0, warrantyMissed = 0;
        foreach (var initial in cohorts)
        {
            if (initial.ActiveCount == 0 || initial.DeliveryMonth >= month) { output.Add(initial); continue; }
            var result = ProcessEngineCohort(initial, date, month, provider, output);
            hours += result.AircraftHours; requested += result.RequestedServices;
            completed += result.CompletedServices; overdue += result.OverdueServices;
            guaranteedShortfall += result.GuaranteedShortfall; technical += result.TechnicalLosses;
            operational += result.OperationalLosses; retired += result.Retirements;
            revenue += result.Revenue; productionCost += result.ProductionCost; expectedLosses += result.ExpectedAccidentLosses;
            activeAtStart += result.ActiveAircraftAtStart; warrantyAircraft += result.WarrantyAircraft; warrantyServiced += result.WarrantyServicedAircraft;
            warrantyMissed += result.WarrantyMissedAircraft;
            if (result.CompletedServices > 0)
            {
                maintenance.TryGetValue(initial.DesignId, out var previous);
                maintenance[initial.DesignId] = new FleetMaintenanceResult((previous?.Revenue ?? 0) + result.Revenue,
                    (previous?.ProductionCost ?? 0) + result.ProductionCost);
            }
        }
        cohorts.Clear(); cohorts.AddRange(output); lastProcessedMonth = month;
        return new FleetMonthReport(month, hours, requested, completed, overdue, guaranteedShortfall, technical,
            operational, retired, cohorts.Where(c => c.Grounded).Sum(c => c.ActiveCount), revenue)
        {
            ActiveAircraftAtStart = activeAtStart, WarrantyAircraft = warrantyAircraft, WarrantyServicedAircraft = warrantyServiced, WarrantyMissedAircraft = warrantyMissed,
            ExpectedAccidentLosses = expectedLosses, ProductionCost = productionCost, MaintenanceByDesign = maintenance,
        };
    }

    public bool IsSupported(string designId) => cohorts.Any(c => c.DesignId == designId && c.ActiveCount > 0 && c.SupportEnabled);
    public bool TryEndSupport(string designId, SimulationDate date, bool allowBreach, out int affectedUnits)
    {
        var month = SimulationMonthId.FromDate(date).Value;
        affectedUnits = cohorts.Where(c => c.DesignId == designId && c.SupportEnabled && c.SupportEndMonth >= month).Sum(c => c.ActiveCount);
        for (var i = 0; i < cohorts.Count; i++) if (cohorts[i].DesignId == designId) cohorts[i] = cohorts[i] with { SupportEnabled = false };
        return true;
    }
    public void ResumeSupport(string designId)
    { for (var i = 0; i < cohorts.Count; i++) if (cohorts[i].DesignId == designId) cohorts[i] = cohorts[i] with { SupportEnabled = true }; }
    public IReadOnlyList<SupportForecastPoint> GetSupportForecast(string designId, SimulationDate start, int months)
    {
        if (months < 0 || months > 2400) throw new ArgumentOutOfRangeException(nameof(months));
        var first = SimulationMonthId.FromDate(start).Value;
        return Enumerable.Range(0, months).Select(offset =>
        {
            var active = cohorts.Where(c => c.DesignId == designId && c.ActiveCount > 0 &&
                c.FlightHours + offset * c.Profile.UtilizationHoursPerMonth < c.RetirementHours).ToArray();
            var guaranteed = cohorts.Where(c => c.DesignId == designId && c.SupportEndMonth >= first + offset).Sum(c => c.ActiveCount);
            return new SupportForecastPoint(first + offset, guaranteed, active.Sum(c => c.ActiveCount),
                checked(cohorts.Where(c => c.DesignId == designId && c.ActiveCount > 0 && c.SupportEnabled)
                    .Sum(c => ForecastSpareParts(c, first, offset))));
        }).ToArray();
    }
    private FleetMonthReport ProcessEngineCohort(FleetCohortSnapshot initial, SimulationDate date, int month,
        IFleetEngineServiceProvider provider, List<FleetCohortSnapshot> output)
    {
        var c = initial with { Grounded = false };
        var remaining = c.Profile.UtilizationHoursPerMonth;
        var terms = c.Offer.EngineMaintenance!;
        var warranty = month <= c.SupportEndMonth;
        var hadSuccessfulService = false;
        int warrantyServiced = 0, warrantyMissed = 0;
        double hours = 0, revenue = 0, productionCost = 0, expectedLosses = 0;
        int requested = 0, completed = 0, overdue = 0, shortfall = 0, technical = 0, operational = 0, retired = 0;
        while (c.ActiveCount > 0)
        {
            if (c.FlightHours >= c.RetirementHours - 1e-7)
            {
                if (warranty && hadSuccessfulService) warrantyServiced += c.ActiveCount;
                retired += c.ActiveCount;
                c = c with { RetiredCount = c.RetiredCount + c.ActiveCount, ActiveCount = 0, Grounded = false };
                break;
            }
            var airframeThreshold = (c.CompletedServiceCycles + 1d) * c.Offer.ServiceIntervalHours;
            var engineThreshold = (c.CompletedEngineServiceCycles + 1d) * terms.ServiceIntervalHours;
            var airframeDue = c.FlightHours >= airframeThreshold - 1e-7;
            var replacementDue = c.EngineHoursSinceReplacement >= terms.ReplacementLifeHours - 1e-7;
            var engineDue = replacementDue || c.EngineHoursSinceReplacement >= engineThreshold - 1e-7;
            if (airframeDue || engineDue)
            {
                requested += c.ActiveCount;
                var fulfilled = !c.SupportEnabled ? 0 : airframeDue
                    ? provider.CompleteAirframeServices(c.Offer.AirframePartsGroupId, c.ActiveCount, c.Offer.SparePartsPerService)
                    : provider.CompleteEngineMaintenance(terms.EngineDesignId, terms.PartsGroupId, c.ActiveCount,
                        c.EngineCount, replacementDue ? 0 : terms.PartsPerService, replacementDue);
                if (fulfilled < 0 || fulfilled > c.ActiveCount) throw new InvalidOperationException("Engine service provider returned invalid aircraft fulfillment.");
                var missed = c.ActiveCount - fulfilled;
                if (missed > 0)
                {
                    overdue += missed; if (warranty) { shortfall += missed; warrantyMissed += missed; }
                    if (fulfilled == 0) { c = c with { Grounded = true }; break; }
                    output.Add(c with { CohortId = c.CohortId + "/g" + (c.SplitSequence + 1), OriginalCount = missed,
                        ActiveCount = missed, AccidentLosses = 0, RetiredCount = 0, Grounded = true,
                        LastProcessedMonth = month, SplitSequence = c.SplitSequence + 1 });
                    c = c with { OriginalCount = c.OriginalCount - missed, ActiveCount = fulfilled, SplitSequence = c.SplitSequence + 1 };
                }
                completed += fulfilled;
                hadSuccessfulService = true;
                if (provider is IFleetMaintenanceCostProvider costProvider)
                {
                    LifecycleCalculator.RequireFiniteNonNegative(costProvider.LastConsumptionCost, nameof(costProvider.LastConsumptionCost));
                    productionCost += costProvider.LastConsumptionCost;
                }
                if (airframeDue)
                {
                    revenue += fulfilled * (double)c.Offer.SparePartsPerService * c.Offer.SparePartUnitPrice;
                    c = c with { CompletedServiceCycles = c.CompletedServiceCycles + 1 };
                }
                else if (replacementDue)
                {
                    revenue += fulfilled * (double)c.EngineCount * terms.ReplacementUnitPrice;
                    c = c with { EngineHoursSinceReplacement = 0d, CompletedEngineServiceCycles = 0 };
                }
                else
                {
                    revenue += fulfilled * (double)c.EngineCount * terms.PartsPerService * terms.PartUnitPrice;
                    c = c with { CompletedEngineServiceCycles = c.CompletedEngineServiceCycles + 1 };
                }
                continue;
            }
            if (remaining <= 1e-7) break;
            var segment = System.Math.Min(remaining, System.Math.Min(c.RetirementHours - c.FlightHours,
                System.Math.Min(airframeThreshold - c.FlightHours,
                    System.Math.Min(engineThreshold, terms.ReplacementLifeHours) - c.EngineHoursSinceReplacement)));
            hours += c.ActiveCount * segment;
            var nextHours = c.FlightHours + segment;
            var nextWear = c.Wear + segment / c.Specification.ExpectedLifeHours;
            var rate = FleetAccidentCatalog.Get(date, c.Specification.Category, c.Profile.Id);
            expectedLosses += c.ActiveCount * FleetAccidentCatalog.BaselineLossProbability(rate, segment, c.Profile.AverageFlightHours);
            var technicalMu = FleetAccidentCatalog.LossIntensity(rate, segment, c.Profile.AverageFlightHours,
                c.Specification.Reliability, (nextWear + c.Wear) * .5d, (nextHours + c.FlightHours) / (c.ContractLifeHours + c.Specification.ExpectedLifeHours), true);
            var operationalMu = FleetAccidentCatalog.LossIntensity(rate, segment, c.Profile.AverageFlightHours,
                c.Specification.Reliability, nextWear, nextHours / ((c.ContractLifeHours + c.Specification.ExpectedLifeHours) * .5d), false);
            var losses = Binomial(c.ActiveCount, 1d - System.Math.Exp(-(technicalMu + operationalMu)));
            var technicalLoss = losses == 0 ? 0 : Binomial(losses, technicalMu / (technicalMu + operationalMu));
            technical += technicalLoss; operational += losses - technicalLoss;
            if (warranty && hadSuccessfulService) warrantyServiced += losses;
            c = c with { FlightHours = nextHours, Wear = nextWear, EngineHoursSinceReplacement = c.EngineHoursSinceReplacement + segment,
                ActiveCount = c.ActiveCount - losses, AccidentLosses = c.AccidentLosses + losses };
            remaining -= segment;
        }
        if (warranty && hadSuccessfulService && !c.Grounded) warrantyServiced += c.ActiveCount;
        output.Add(c with { LastProcessedMonth = month });
        return new FleetMonthReport(month, hours, requested, completed, overdue, shortfall, technical, operational,
            retired, c.Grounded ? c.ActiveCount : 0, revenue)
        {
            ActiveAircraftAtStart = initial.ActiveCount, WarrantyAircraft = warranty ? initial.ActiveCount : 0, WarrantyServicedAircraft = warrantyServiced,
            WarrantyMissedAircraft = warrantyMissed, ExpectedAccidentLosses = expectedLosses, ProductionCost = productionCost,
        };
    }

    public GuaranteeMaintenanceDemand GetGuaranteeMaintenanceDemand(string designId, SimulationDate date)
    {
        var month = SimulationMonthId.FromDate(date).Value;
        long airframe = 0, engineParts = 0, engines = 0;
        foreach (var c in cohorts.Where(c => c.DesignId == designId && c.ActiveCount > 0 && c.SupportEndMonth >= month))
        {
            var months = System.Math.Max(0L, (long)c.SupportEndMonth - System.Math.Max(System.Math.Max(month, lastProcessedMonth + 1), c.DeliveryMonth + 1) + 1L);
            var hours = System.Math.Max(0d, System.Math.Min(months * c.Profile.UtilizationHoursPerMonth,
                c.RetirementHours - c.FlightHours - 1e-6));
            if (c.FlightHours >= c.RetirementHours - 1e-7 || months == 0) continue;
            var cycles = System.Math.Max(0L, (long)System.Math.Floor((c.FlightHours + hours) / c.Offer.ServiceIntervalHours)
                - c.CompletedServiceCycles);
            var events = ForecastEngineEvents(c, hours);
            checked
            {
                airframe += cycles * c.ActiveCount * c.Offer.SparePartsPerService;
                engineParts += (long)events.services * c.ActiveCount * c.EngineCount * c.Offer.EngineMaintenance!.PartsPerService;
                engines += (long)events.replacements * c.ActiveCount * c.EngineCount;
            }
        }
        return new GuaranteeMaintenanceDemand(airframe, engineParts, engines);
    }

    private static (int services, int replacements) ForecastEngineEvents(FleetCohortSnapshot cohort, double remaining)
    {
        if (cohort.FlightHours >= cohort.RetirementHours - 1e-7) return (0, 0);
        var terms = cohort.Offer.EngineMaintenance!;
        var engineHours = cohort.EngineHoursSinceReplacement;
        var cycles = cohort.CompletedEngineServiceCycles;
        int services = 0, replacements = 0;
        while (true)
        {
            if (engineHours >= terms.ReplacementLifeHours - 1e-7)
            { replacements = checked(replacements + 1); engineHours = 0; cycles = 0; continue; }
            var threshold = (cycles + 1d) * terms.ServiceIntervalHours;
            if (engineHours >= threshold - 1e-7)
            { services = checked(services + 1); cycles++; continue; }
            if (remaining <= 1e-7) return (services, replacements);
            var segment = System.Math.Min(remaining, System.Math.Min(threshold, terms.ReplacementLifeHours) - engineHours);
            engineHours += segment; remaining -= segment;
        }
    }

    private static int ForecastSpareParts(FleetCohortSnapshot cohort, int firstMonth, int offset)
    {
        if (cohort.DeliveryMonth >= firstMonth + offset) return 0;
        var elapsed = System.Math.Max(0, firstMonth + offset - System.Math.Max(firstMonth, cohort.DeliveryMonth + 1));
        var previousHours = cohort.FlightHours + elapsed * cohort.Profile.UtilizationHoursPerMonth;
        var endHours = System.Math.Min(cohort.RetirementHours - .000001d, previousHours + cohort.Profile.UtilizationHoursPerMonth);
        var previousCycles = elapsed == 0 ? cohort.CompletedServiceCycles : (int)System.Math.Floor(previousHours / cohort.Offer.ServiceIntervalHours);
        var cycles = System.Math.Max(0, (int)System.Math.Floor(endHours / cohort.Offer.ServiceIntervalHours) - previousCycles);
        return checked(cycles * cohort.ActiveCount * cohort.Offer.SparePartsPerService);
    }
    public FleetRegistrySnapshot CreateSnapshot() => new(randomState, deliveryIds.OrderBy(x => x, StringComparer.Ordinal).ToArray(), cohorts.ToArray(), lastProcessedMonth);
    public void RestoreSnapshot(FleetRegistrySnapshot snapshot)
    {
        if (snapshot == null || snapshot.AccidentRandomState == 0 || snapshot.Cohorts == null || snapshot.RegisteredDeliveryIds == null) throw new ArgumentException("Invalid fleet snapshot.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var registered = new HashSet<string>(snapshot.RegisteredDeliveryIds, StringComparer.Ordinal);
        if (registered.Count != snapshot.RegisteredDeliveryIds.Length || registered.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Invalid registered delivery IDs.");
        var restored = new List<FleetCohortSnapshot>();
        foreach (var original in snapshot.Cohorts)
        {
            var c = original;
            if (c.LifetimePolicyVersion == 0)
            {
                if (c.ContractLifeHours <= 0 || c.RetirementHours < c.ContractLifeHours || c.RetirementHours > c.ContractLifeHours * 1.2d)
                    throw new ArgumentException("Invalid previous retirement policy.");
                var position = (c.RetirementHours / c.ContractLifeHours - 1d) / .2d;
                c = c with { RetirementHours = (c.ContractLifeHours + c.Specification.ExpectedLifeHours) * .5d
                    * (.9d + .2d * position), LifetimePolicyVersion = 1 };
            }
            c.Specification.Validate(); c.Profile.Validate(); c.Offer.Validate();
            if (c.Offer.EngineCount != c.EngineCount)
                throw new ArgumentException("Frozen maintenance engine count does not match the saved installed engines.");
            LifecycleCalculator.RequireFiniteNonNegative(c.FlightHours, nameof(c.FlightHours));
            LifecycleCalculator.RequireFiniteNonNegative(c.Wear, nameof(c.Wear));
            LifecycleCalculator.RequireFiniteNonNegative(c.ContractLifeHours, nameof(c.ContractLifeHours));
            LifecycleCalculator.RequireFiniteNonNegative(c.RetirementHours, nameof(c.RetirementHours));
            LifecycleCalculator.RequireFiniteNonNegative(c.EngineHoursSinceReplacement, nameof(c.EngineHoursSinceReplacement));
            if (c.CompletedEngineServiceCycles < 0 || (c.EngineHoursSinceReplacement > c.Offer.EngineMaintenance!.ReplacementLifeHours + 1e-7 ||
                 c.CompletedEngineServiceCycles > System.Math.Floor((c.EngineHoursSinceReplacement + 1e-7) / c.Offer.EngineMaintenance.ServiceIntervalHours)))
                throw new ArgumentException("Invalid installed-engine age or service phase.");
            if (!seen.Add(c.CohortId) || string.IsNullOrWhiteSpace(c.CohortId) || string.IsNullOrWhiteSpace(c.DesignId) || !registered.Contains(c.DeliveryId) ||
                c.OriginalCount != c.ActiveCount + c.AccidentLosses + c.RetiredCount || c.ActiveCount < 0 ||
                c.AccidentLosses < 0 || c.RetiredCount < 0 || c.EngineCount <= 0 || c.ContractLifeHours <= 0 ||
                c.LifetimePolicyVersion != 1 || c.RetirementHours < (c.ContractLifeHours + c.Specification.ExpectedLifeHours) * .45d - 1e-7
                || c.RetirementHours > (c.ContractLifeHours + c.Specification.ExpectedLifeHours) * .55d + 1e-7 ||
                c.CompletedServiceCycles < 0 || c.SplitSequence < 0 || c.SupportEndMonth < c.DeliveryMonth || c.LastProcessedMonth < c.DeliveryMonth) throw new ArgumentException("Invalid fleet cohort.");
            restored.Add(c);
        }
        cohorts.Clear(); cohorts.AddRange(restored); deliveryIds.Clear();
        foreach (var id in snapshot.RegisteredDeliveryIds) deliveryIds.Add(id);
        randomState = snapshot.AccidentRandomState; lastProcessedMonth = snapshot.LastProcessedMonth;
    }
    private double NextRandom()
    {
        randomState ^= randomState >> 12; randomState ^= randomState << 25; randomState ^= randomState >> 27;
        return ((randomState * 2685821657736338717UL) >> 11) * (1d / 9007199254740992d);
    }
    private int Binomial(int count, double probability)
    {
        if (probability <= 0) return 0; if (probability >= 1) return count;
        // Geometric gaps provide exact Bernoulli counts without per-aircraft work for rare accidents.
        var successes = 0; long position = -1; var denominator = System.Math.Log(1d - probability);
        while (true)
        {
            var gap = System.Math.Floor(System.Math.Log(System.Math.Max(NextRandom(), 1e-16)) / denominator) + 1d;
            if (position + gap >= count) return successes;
            position += (long)gap; successes++;
        }
    }
    private static uint StableHash(string value)
    { unchecked { uint hash = 2166136261; foreach (var c in value) { hash ^= c; hash *= 16777619; } return hash; } }
}
