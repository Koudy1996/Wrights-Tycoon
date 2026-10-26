using System.Collections.Generic;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Presentation;

namespace WrightTycoon.Game.Launch;

/// <summary>Market reference driven by the same lifecycle policy as new contract generation.</summary>
public static class DemandLifecycleText
{
    public static string Availability(AircraftCategory category, int year, int forecastYear) =>
        $"{year}: {(ContractBalance.IsCategoryAvailableForYear(category, year) ? "available by year" : "not yet available")} | {forecastYear}: {(ContractBalance.IsCategoryAvailableForYear(category, forecastYear) ? "available by year" : "not yet available")}; campaign unlocks also apply";
    public static IReadOnlyList<string> ForCategory(AircraftCategory category)
    {
        var life = LifecycleContractPolicy.RequiredLife(category);
        var fuel = ContractBalance.ResolveFuelConsumptionRequirementRangeKilogramsPerHour(category);
        string N(double value, string format = "0") => FinancialValueFormatter.FormatNumber(value, format);
        return new[]
        {
            $"Required Expected Life: >= {N(life)} flight hours (current contract policy; unchanged in +3y forecast)",
            $"Civil Maximum Fuel-burn Requirement Range: {N(fuel.Item1, "0.0")}–{N(fuel.Item2, "0.0")} kg/h",
        };
    }
}
