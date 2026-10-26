using System.Collections.Generic;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Presentation;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Fleet;

namespace WrightTycoon.Game.Launch;

/// <summary>Shared, testable lifecycle labels for contract cards, reports and the government notebook.</summary>
public static class ContractLifecycleText
{
    private static string HourlyCurrency(double value) => "$" + FinancialValueFormatter.FormatNumber(value, "0.00");
    public static IReadOnlyList<string> Requirements(ContractRequirements r) => Requirements(r.LifecycleModelVersion,
        r.MinimumExpectedLifeHours, r.MinimumServiceIntervalHours, r.MinimumSupportMonths,
        r.ProcurementPreference, r.MaximumOperatingCostPerUnit, r.EconomicSnapshot);

    public static IReadOnlyList<string> Requirements(GovernmentContractRequirements r) => Requirements(r.LifecycleModelVersion,
        r.MinimumExpectedLifeHours, r.MinimumServiceIntervalHours, r.MinimumSupportMonths,
        r.ProcurementPreference, r.MaximumOperatingCostPerUnit, r.EconomicSnapshot);

    private static IReadOnlyList<string> Requirements(int version, double life, int interval, int support,
        ProcurementPreference preference, double budget, ContractEconomicSnapshot? economic)
    {
        if (version <= 0) return new[]
        {
            "- Expected life: unspecified",
            "- Operational costs: unspecified",
        };
        var wholeLife = economic is not null || preference == ProcurementPreference.WholeLifeCost;
        var priorityLabel = economic?.Priority switch
        {
            OperationalCostPriority.PurchasePrice => "purchase price",
            OperationalCostPriority.Balanced => "balanced",
            OperationalCostPriority.OperationalCosts => "operational costs",
            _ => preference == ProcurementPreference.WholeLifeCost ? "operational costs" : "purchase price",
        };
        return new[]
        {
            $"- Expected life: >= {FinancialValueFormatter.FormatNumber(life, "0")} h",
            $"- Service interval: >= {FinancialValueFormatter.FormatNumber(interval, "0")} h",
            $"- Operational costs{(wholeLife ? ": <= " : " (reference): ")}{HourlyCurrency(life > 0 ? budget / life : 0)}/h",
            $"- Support: {FinancialValueFormatter.FormatNumber(support / 12d, "0.#")} years/delivery",
            $"- Priority: {priorityLabel}",
        };
    }

    public static string AgreedSupport(ServiceOfferSnapshot offer) =>
        $"Agreed Guaranteed Support (Frozen): {FinancialValueFormatter.FormatNumber(offer.GuaranteedSupportMonths / 12d, "0.#")} years from each delivery";

    public static IReadOnlyList<string> Comparison(double expectedLife, double requiredLife, LifecycleCostQuote quote, int lifecycleModelVersion = 2) => new[]
    {
        $"Aircraft Expected Life: {FinancialValueFormatter.FormatNumber(expectedLife, "0")} h | required >= {FinancialValueFormatter.FormatNumber(requiredLife, "0")} h",
        $"Operational Costs (Customer): {HourlyCurrency(quote.OperationCostsPerHour)} / flight hour over {FinancialValueFormatter.FormatNumber(quote.ContractHours, "0")} h",
        $"Purchase {FinancialValueFormatter.FormatCurrency(quote.PurchasePrice)} | fuel {FinancialValueFormatter.FormatCurrency(quote.FuelCost)} | scheduled maintenance {FinancialValueFormatter.FormatCurrency(quote.ScheduledServiceCost)}",
        "Operational costs = purchase amortized over the aircraft's expected life + fuel + airframe/engine parts + replacement engines. Downtime and risk are excluded.",
    };
}
