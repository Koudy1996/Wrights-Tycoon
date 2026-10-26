using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Contracts;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    // Presentation-only memoization. Simulation and delivery validation never use these results.
    private object[] _contractUiInputs = Array.Empty<object>();
    private bool _contractUiCacheActive;
    private readonly Dictionary<Contract, ContractAvailabilitySnapshot> _contractUiAvailability = new();
    private readonly Dictionary<Contract, IReadOnlyList<ContractDeliveryCandidate>> _contractUiCandidates = new();
    private readonly Dictionary<(Contract, WrightTycoon.Core.Aircraft.Aircraft), ContractEvaluationReport> _contractUiEvaluations = new();
    private readonly Dictionary<SavedAircraftDesign, double> _contractUiAirframePartPrices = new();
    private readonly Dictionary<SavedEngineDesign, (double Part, double Replacement)> _contractUiEnginePrices = new();

    private IDisposable BeginContractUiCache()
    {
        var inputs = new List<object>
        {
            _contractBoard!, _productionFactory!, _researchBoard!, _currentDate.Year, _currentDate.Month,
            _headquarters.CompanyQualityLevel, _reputation.PriceMultiplier,
            _aviationFuelMarket.Get(_currentDate, true).DollarsPerKilogram,
            _aviationFuelMarket.Get(_currentDate, false).DollarsPerKilogram,
        };
        foreach (var contract in _contractBoard!.OpenContracts)
        {
            inputs.Add(contract);
            inputs.Add(contract.Requirements.EconomicSnapshot!);
            inputs.Add(contract.RemainingMonths);
        }
        foreach (var saved in _savedAircraftDesigns)
        {
            inputs.Add(saved);
            inputs.Add(saved.CatalogItem);
            inputs.Add(saved.IsDecommissioned);
            inputs.Add(IsAircraftDesignReadyForProduction(saved));
        }
        foreach (var saved in _savedEngines)
        {
            inputs.Add(saved);
            inputs.Add(saved.ApprovedYear ?? 0);
            inputs.Add(saved.FrozenYear ?? 0);
            inputs.Add(saved.FrozenMonth ?? 0);
        }
        foreach (var modifier in _researchModifiersByResearchId.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            inputs.Add(modifier.Key);
            inputs.Add(modifier.Value.ReliabilityPoints);
            inputs.Add(modifier.Value.ProductionOptimizationPoints);
            inputs.Add(modifier.Value.FastDevelopmentPoints);
        }
        foreach (var stock in _productionFactory!.WarehouseUnitsByDesign.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            inputs.Add(stock.Key);
            inputs.Add(stock.Value);
        }
        if (!_contractUiInputs.SequenceEqual(inputs))
        {
            ClearContractUiCache();
            _contractUiInputs = inputs.ToArray();
        }
        _contractUiCacheActive = true;
        return new ContractUiCacheScope(this);
    }

    private void ClearContractUiCache()
    {
        _contractUiCacheActive = false;
        _contractUiInputs = Array.Empty<object>();
        _contractUiAvailability.Clear();
        _contractUiCandidates.Clear();
        _contractUiEvaluations.Clear();
        _contractUiAirframePartPrices.Clear();
        _contractUiEnginePrices.Clear();
    }

    private ContractEvaluationReport EvaluateContractForUi(Contract contract, ContractBoard board,
        WrightTycoon.Core.Aircraft.Aircraft aircraft)
    {
        if (!_contractUiCacheActive) return board.Evaluate(contract, aircraft);
        var key = (contract, aircraft);
        if (!_contractUiEvaluations.TryGetValue(key, out var evaluation))
        {
            evaluation = board.Evaluate(contract, aircraft);
            _contractUiEvaluations.Add(key, evaluation);
        }
        return evaluation;
    }

    private double ContractUiAirframePartPrice(SavedAircraftDesign saved)
    {
        if (_contractUiCacheActive && _contractUiAirframePartPrices.TryGetValue(saved, out var price)) return price;
        price = WrightTycoon.Core.Fleet.FleetServiceBalance.AirframePartUnitPrice(BuildReferenceAircraft(saved));
        if (_contractUiCacheActive) _contractUiAirframePartPrices.Add(saved, price);
        return price;
    }

    private (double Part, double Replacement) ContractUiEnginePrices(SavedEngineDesign saved)
    {
        if (_contractUiCacheActive && _contractUiEnginePrices.TryGetValue(saved, out var prices)) return prices;
        var engine = BuildReferenceEngine(saved);
        prices = (WrightTycoon.Core.Fleet.FleetServiceBalance.EnginePartUnitPrice(engine),
            WrightTycoon.Core.Fleet.FleetServiceBalance.ReplacementEngineUnitPrice(engine));
        if (_contractUiCacheActive) _contractUiEnginePrices.Add(saved, prices);
        return prices;
    }

    private sealed class ContractUiCacheScope : IDisposable
    {
        private readonly WrightTycoonLaunchRunner _owner;
        public ContractUiCacheScope(WrightTycoonLaunchRunner owner) => _owner = owner;
        public void Dispose() => _owner._contractUiCacheActive = false;
    }
}
