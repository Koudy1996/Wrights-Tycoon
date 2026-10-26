using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using WrightTycoon.Core.Company;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.GovernmentContracts;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private int _typeConstructionDurabilityLevel;
    private int _typeServiceIntervalHours = 1000;
    private CompanyReputation _reputation = new();
    private string _factoryForecastCacheKey = string.Empty;
    private double[] _factorySupportForecastMd = new double[12];

    private static int NormalizeServiceInterval(int value) =>
        value == 8000 ? 4000 : LifecycleBalance.ServiceIntervalsHours.Contains(value) ? value : 1000;

    private void DrawLifecycleDesignerControls()
    {
        DrawModernSectionHeader("Lifecycle + Support", "Base life: 30,000 h at 80% reliability and 1,000 h service. Same baseline for every aircraft.");
        _typeConstructionDurabilityLevel = DrawIntSlider("Construction Durability", _typeConstructionDurabilityLevel,
            0, 10, useLinearScale: true);
        GUILayout.Label("Each level: +8% lifetime factor; +2% structural mass, +3.5% structural parts/work, +0.8% drag.");
        var intervals = LifecycleBalance.ServiceIntervalsHours.ToArray();
        var selection = Array.IndexOf(intervals, _typeServiceIntervalHours);
        selection = DrawListSelector("Main Service Interval", Mathf.Max(0, selection), intervals.Select(v => $"{v:N0} h").ToArray());
        _typeServiceIntervalHours = intervals[selection];
        GUILayout.Label("Guaranteed support is set by each contract, not by the aircraft design.");
    }

    private readonly MonthlyAviationFuelMarket _aviationFuelMarket = new();

    private static string FormatOperationCurrency(double value) => "$" + FormatUiNumber(value, "0.00");

    private void DrawLifecyclePreview()
    {
        if (_aircraftPreview is not null) DrawAircraftOwnershipMetrics(_aircraftPreview);
    }

    private void DrawAircraftOwnershipMetrics(WrightTycoon.Core.Aircraft.Aircraft aircraft)
    {
        var specification = AircraftLifecycleSpecification.FromAircraft(aircraft, _headquarters.CompanyQualityLevel);
        var fuel = _aviationFuelMarket.Get(_currentDate, !IsJetFamily(aircraft.Engine.Definition.EngineType));
        var saved = _savedAircraftDesigns.FirstOrDefault(d => ReferenceEquals(d.CatalogItem.Aircraft, aircraft)
            || d.CatalogItem.Aircraft.TypeDefinition.Id == aircraft.TypeDefinition.Id);
        var offer = saved is null ? LifecycleCostCalculator.CreateEngineLifecycleServiceOffer(aircraft, specification.Reliability)
            : BuildCombinedMaintenanceOffer(saved, specification.Reliability, preview: aircraft);
        var purchasePrice = aircraft.Performance.CompleteProductionCost
            * (1d + ((1d + WrightTycoon.Core.Balance.ContractBalance.SmallOrderContractMarkup)
                * WrightTycoon.Core.Balance.ContractBalance.ContractPriceMarginImprovementMultiplier - 1d)
                * WrightTycoon.Core.Balance.ContractBalance.AdditionalMarginMultiplier)
            * _reputation.PriceMultiplier;
        var quote = AircraftOperationCostCalculator.Calculate(purchasePrice, specification.ExpectedLifeHours,
            aircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour, fuel.DollarsPerKilogram,
            aircraft.TypeDefinition.ServiceIntervalHours, offer.SparePartsPerService, offer.SparePartUnitPrice);
        var engineCosts = EngineOwnershipCostCalculator.Calculate(specification.ExpectedLifeHours,
            offer.EngineMaintenance!, aircraft.Airframe.Parameters.EngineCount);
        var engineRoutinePerHour = engineCosts.RoutinePartsCost / specification.ExpectedLifeHours;
        var engineReplacementPerHour = engineCosts.ReplacementCost / specification.ExpectedLifeHours;
        var totalPerHour = quote.TotalPerHour + engineRoutinePerHour + engineReplacementPerHour;
        DrawParameterValueRow("Expected Life", $"{FormatUiNumber(specification.ExpectedLifeHours, "0")} flight hours",
            new ModifierTooltipDefinition("aircraft.expected-life", "Expected Life", neutralLines: new[]
                {
                    "Base: 30,000 flight hours at 80% reliability, 1,000 h service and durability 0, independent of market classification.",
                    "Life = base × reliability factor × service factor × (1 + 0.08 × durability).",
                    "Reliability <= 100%: max(0.5, (effective reliability / 0.80)^1.5).",
                    "Above 100%: (1 / 0.80)^1.5 × (1 + 1.5 × (reliability - 1)); continuous linear growth, no ceiling.",
                    "Service factor: (1,000 / interval hours)^0.2, limited to 0.65–1.2.",
                    "Includes effective reliability bonuses. Delivered aircraft retain their agreed life; contract requirements are unchanged."
                }));
        var tooltip = $"Per flight hour: depreciation {FormatOperationCurrency(quote.DepreciationPerHour)} + airframe parts {FormatOperationCurrency(quote.SparePartsPerHour)} + engine parts {FormatOperationCurrency(engineRoutinePerHour)} + replacement engines {FormatOperationCurrency(engineReplacementPerHour)} + fuel {FormatOperationCurrency(quote.FuelPerHour)}.\n"
            + $"Depreciation = reference purchase price {FormatCurrency(purchasePrice)} / expected life. Reference price uses standard small-order sales markup and company reputation; actual contract prices can differ.\n"
            + $"Airframe parts = {quote.MaintenanceCycles} cycles before retirement × {offer.SparePartsPerService} parts × {FormatOperationCurrency(offer.SparePartUnitPrice)} / expected life.\n"
            + $"Engine upkeep over aircraft life: {engineCosts.RoutineCycles} routine cycles and {engineCosts.Replacements} replacements per engine, for {aircraft.Airframe.Parameters.EngineCount} installed engines. Original engines are included once in the aircraft purchase price; only subsequent replacements are added.\n"
            + $"Fuel price for {_currentDate.Year}-{_currentDate.Month:00}: {FormatUiNumber(fuel.DollarsPerKilogram, "0.0000")} $/kg; {fuel.Source}. "
            + (fuel.IsEstimated ? "MODEL ESTIMATE, not a direct aviation-fuel observation. " : "")
            + "Prices change only with the simulation month. Fuel cost assumes the current monthly price throughout the reference life; downtime and risk are excluded.";
        DrawParameterValueRow("Operation Costs", $"{FormatOperationCurrency(totalPerHour)}/h",
            new ModifierTooltipDefinition("aircraft.operation-costs", "Operation Costs", neutralLines: tooltip.Split('\n')));
    }

    private void DrawCivilLifecycleComparison(Contract contract, IReadOnlyList<ContractDeliveryAllocation> allocations)
    {
        if (contract.Requirements.LifecycleModelVersion <= 0 || allocations.Count == 0) return;
        var totalCost = 0d;
        var units = allocations.Sum(a => a.UnitsToDeliver);
        var minimumLife = double.MaxValue;
        foreach (var allocation in allocations)
        {
            var aircraft = allocation.Candidate.CatalogItem.Aircraft;
            var specification = AircraftLifecycleSpecification.FromAircraft(aircraft, _headquarters.CompanyQualityLevel);
            minimumLife = System.Math.Min(minimumLife, specification.ExpectedLifeHours);
            var quote = LifecycleCostCalculator.Calculate(LifecycleCostCalculator.ExpectedOperatingLifeHours(contract.Requirements.MinimumExpectedLifeHours, specification.ExpectedLifeHours),
                GetContractUnitPrice(contract), aircraft.Performance.AverageCruiseFuelBurnKilogramsPerHour,
                _aviationFuelMarket.Get(_currentDate, !IsJetFamily(aircraft.Engine.Definition.EngineType)).DollarsPerKilogram,
                CreateCivilContractServiceOffer(aircraft, contract.Requirements, specification.Reliability),
                contract.Requirements.UtilizationHoursPerMonth);
            totalCost += quote.OperationCostsPerHour * allocation.UnitsToDeliver;
        }
        DrawModernInfoRow("Lowest Expected Life in Delivery Plan", $"{FormatUiNumber(minimumLife, "0")} h");
        DrawModernInfoRow("Plan Operational Costs (Customer)", $"{FormatOperationCurrency(totalCost / System.Math.Max(1, units))} / flight hour");
        GUILayout.Label("Includes purchase amortization over the mean of required and actual design life, fuel, airframe/engine parts and replacement engines; excludes downtime and risk. This is customer expenditure, not manufacturer profit.");
    }

    private void DrawGovernmentLifecycleComparison(GovernmentContract contract, GovernmentContractSubmissionCandidate candidate)
    {
        if (contract.Requirements.LifecycleModelVersion <= 0) return;
        var locked = contract.AcceptedSubmission?.DesignId == candidate.DesignId ? contract.LockedLifecycleCostQuote : null;
        var unitPrice = contract.LockedUnitPrice ?? new GovernmentContractPricingService(() => _reputation.PriceMultiplier)
            .CalculateQuote(contract.Requirements, 1).UnitPrice;
        var quote = locked ?? candidate.Metrics.QuoteForContract(contract.Requirements, unitPrice);
        foreach (var line in ContractLifecycleText.Comparison(candidate.Metrics.ExpectedLifeHours,
            contract.Requirements.MinimumExpectedLifeHours, quote, contract.Requirements.LifecycleModelVersion)) GUILayout.Label(line);
    }

}
