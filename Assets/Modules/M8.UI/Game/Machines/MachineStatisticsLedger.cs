using System;
using System.Collections.Generic;

namespace WrightTycoon.Game.Machines;

public sealed class MachineStatisticsLedger
{
    private readonly Dictionary<string, int> _producedAircraftUnitsByDesignId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _soldAircraftUnitsByDesignId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _producedEngineUnitsByResearchId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _soldEngineUnitsByResearchId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _salesRevenueByDesignId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _estimatedSalesRevenueDesignIds = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> ProducedAircraftUnitsByDesignId => _producedAircraftUnitsByDesignId;

    public IReadOnlyDictionary<string, int> SoldAircraftUnitsByDesignId => _soldAircraftUnitsByDesignId;

    public IReadOnlyDictionary<string, int> ProducedEngineUnitsByResearchId => _producedEngineUnitsByResearchId;

    public IReadOnlyDictionary<string, int> SoldEngineUnitsByResearchId => _soldEngineUnitsByResearchId;

    public IReadOnlyDictionary<string, double> SalesRevenueByDesignId => _salesRevenueByDesignId;

    public void RegisterProduction(string designId, string engineResearchId, int enginesPerAircraft, int aircraftUnits, bool includeEngineProduction = true)
    {
        ValidateMachineEvent(designId, engineResearchId, enginesPerAircraft, aircraftUnits);
        var engineUnits = checked(enginesPerAircraft * aircraftUnits);
        AddUnits(_producedAircraftUnitsByDesignId, designId, aircraftUnits);
        if (includeEngineProduction) AddUnits(_producedEngineUnitsByResearchId, engineResearchId, engineUnits);
    }

    public void RegisterEngineProduction(string engineResearchId, int units) => RegisterEngineUnits(_producedEngineUnitsByResearchId, engineResearchId, units);

    public void RegisterEngineSale(string engineResearchId, int units) => RegisterEngineUnits(_soldEngineUnitsByResearchId, engineResearchId, units);

    private static void RegisterEngineUnits(Dictionary<string, int> values, string id, int units)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Engine ID is required.", nameof(id));
        if (units <= 0) throw new ArgumentOutOfRangeException(nameof(units));
        AddUnits(values, id, units);
    }

    public void RegisterSale(
        string designId,
        string engineResearchId,
        int enginesPerAircraft,
        int aircraftUnits,
        double salesRevenue)
    {
        ValidateMachineEvent(designId, engineResearchId, enginesPerAircraft, aircraftUnits);
        if (double.IsNaN(salesRevenue) || double.IsInfinity(salesRevenue) || salesRevenue < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(salesRevenue), "Sales revenue must be finite and >= 0.");
        }

        var engineUnits = checked(enginesPerAircraft * aircraftUnits);
        AddUnits(_soldAircraftUnitsByDesignId, designId, aircraftUnits);
        AddUnits(_soldEngineUnitsByResearchId, engineResearchId, engineUnits);
        AddRevenue(designId, salesRevenue);
    }

    public void AddSalesRevenue(string designId, double salesRevenue, bool isEstimate = false)
    {
        if (string.IsNullOrWhiteSpace(designId))
        {
            throw new ArgumentException("Design ID cannot be empty.", nameof(designId));
        }

        if (double.IsNaN(salesRevenue) || double.IsInfinity(salesRevenue) || salesRevenue < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(salesRevenue), "Sales revenue must be finite and >= 0.");
        }

        AddRevenue(designId, salesRevenue);
        if (isEstimate)
        {
            _estimatedSalesRevenueDesignIds.Add(designId.Trim());
        }
    }

    public int GetProducedAircraftUnits(string designId) => GetUnits(_producedAircraftUnitsByDesignId, designId);

    public int GetSoldAircraftUnits(string designId) => GetUnits(_soldAircraftUnitsByDesignId, designId);

    public int GetProducedEngineUnits(string researchId) => GetUnits(_producedEngineUnitsByResearchId, researchId);

    public int GetSoldEngineUnits(string researchId) => GetUnits(_soldEngineUnitsByResearchId, researchId);

    public double GetSalesRevenue(string designId) =>
        !string.IsNullOrWhiteSpace(designId) && _salesRevenueByDesignId.TryGetValue(designId.Trim(), out var value)
            ? value
            : 0d;

    public bool IsSalesRevenueEstimated(string designId) =>
        !string.IsNullOrWhiteSpace(designId) && _estimatedSalesRevenueDesignIds.Contains(designId.Trim());

    public MachineStatisticsSnapshot CreateSnapshot() => new()
    {
        ProducedAircraftUnitsByDesignId = new Dictionary<string, int>(_producedAircraftUnitsByDesignId, StringComparer.Ordinal),
        SoldAircraftUnitsByDesignId = new Dictionary<string, int>(_soldAircraftUnitsByDesignId, StringComparer.Ordinal),
        ProducedEngineUnitsByResearchId = new Dictionary<string, int>(_producedEngineUnitsByResearchId, StringComparer.Ordinal),
        SoldEngineUnitsByResearchId = new Dictionary<string, int>(_soldEngineUnitsByResearchId, StringComparer.Ordinal),
        SalesRevenueByDesignId = new Dictionary<string, double>(_salesRevenueByDesignId, StringComparer.Ordinal),
        EstimatedSalesRevenueDesignIds = new List<string>(_estimatedSalesRevenueDesignIds),
    };

    public void RestoreSnapshot(MachineStatisticsSnapshot? snapshot)
    {
        Clear();
        if (snapshot is null)
        {
            return;
        }

        RestoreUnits(snapshot.ProducedAircraftUnitsByDesignId, _producedAircraftUnitsByDesignId);
        RestoreUnits(snapshot.SoldAircraftUnitsByDesignId, _soldAircraftUnitsByDesignId);
        RestoreUnits(snapshot.ProducedEngineUnitsByResearchId, _producedEngineUnitsByResearchId);
        RestoreUnits(snapshot.SoldEngineUnitsByResearchId, _soldEngineUnitsByResearchId);
        if (snapshot.SalesRevenueByDesignId is not null)
        {
            foreach (var entry in snapshot.SalesRevenueByDesignId)
            {
                if (!string.IsNullOrWhiteSpace(entry.Key)
                    && !double.IsNaN(entry.Value)
                    && !double.IsInfinity(entry.Value)
                    && entry.Value > 0d)
                {
                    _salesRevenueByDesignId[entry.Key.Trim()] = entry.Value;
                }
            }
        }

        if (snapshot.EstimatedSalesRevenueDesignIds is not null)
        {
            foreach (var designId in snapshot.EstimatedSalesRevenueDesignIds)
            {
                if (!string.IsNullOrWhiteSpace(designId))
                {
                    _estimatedSalesRevenueDesignIds.Add(designId.Trim());
                }
            }
        }
    }

    public void Clear()
    {
        _producedAircraftUnitsByDesignId.Clear();
        _soldAircraftUnitsByDesignId.Clear();
        _producedEngineUnitsByResearchId.Clear();
        _soldEngineUnitsByResearchId.Clear();
        _salesRevenueByDesignId.Clear();
        _estimatedSalesRevenueDesignIds.Clear();
    }

    private static void ValidateMachineEvent(
        string designId,
        string engineResearchId,
        int enginesPerAircraft,
        int aircraftUnits)
    {
        if (string.IsNullOrWhiteSpace(designId))
        {
            throw new ArgumentException("Design ID cannot be empty.", nameof(designId));
        }

        if (string.IsNullOrWhiteSpace(engineResearchId))
        {
            throw new ArgumentException("Engine research ID cannot be empty.", nameof(engineResearchId));
        }

        if (enginesPerAircraft <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(enginesPerAircraft));
        }

        if (aircraftUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(aircraftUnits));
        }
    }

    private void AddRevenue(string designId, double salesRevenue)
    {
        var key = designId.Trim();
        _salesRevenueByDesignId.TryGetValue(key, out var currentRevenue);
        var updated = currentRevenue + salesRevenue;
        if (double.IsInfinity(updated))
        {
            throw new OverflowException("Sales revenue overflowed.");
        }

        _salesRevenueByDesignId[key] = updated;
    }

    private static void AddUnits(Dictionary<string, int> values, string key, int units)
    {
        var normalizedKey = key.Trim();
        values.TryGetValue(normalizedKey, out var currentUnits);
        values[normalizedKey] = checked(currentUnits + units);
    }

    private static int GetUnits(IReadOnlyDictionary<string, int> values, string key) =>
        !string.IsNullOrWhiteSpace(key) && values.TryGetValue(key.Trim(), out var units) ? units : 0;

    private static void RestoreUnits(
        IReadOnlyDictionary<string, int>? source,
        Dictionary<string, int> destination)
    {
        if (source is null)
        {
            return;
        }

        foreach (var entry in source)
        {
            if (!string.IsNullOrWhiteSpace(entry.Key) && entry.Value > 0)
            {
                destination[entry.Key.Trim()] = entry.Value;
            }
        }
    }
}

public sealed class MachineStatisticsSnapshot
{
    public Dictionary<string, int> ProducedAircraftUnitsByDesignId { get; set; } = new();

    public Dictionary<string, int> SoldAircraftUnitsByDesignId { get; set; } = new();

    public Dictionary<string, int> ProducedEngineUnitsByResearchId { get; set; } = new();

    public Dictionary<string, int> SoldEngineUnitsByResearchId { get; set; } = new();

    public Dictionary<string, double> SalesRevenueByDesignId { get; set; } = new();

    public List<string> EstimatedSalesRevenueDesignIds { get; set; } = new();
}

public readonly struct MachineCostSummary
{
    public MachineCostSummary(double fixedCosts, double totalCosts, double totalProfit)
    {
        FixedCosts = fixedCosts;
        TotalCosts = totalCosts;
        TotalProfit = totalProfit;
    }

    public double FixedCosts { get; }

    public double TotalCosts { get; }

    public double TotalProfit { get; }
}

public static class MachineEconomicsCalculator
{
    public static double AllocateSharedResearchCost(double researchCost, int exactDesignUsers)
    {
        if (double.IsNaN(researchCost) || double.IsInfinity(researchCost) || researchCost < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(researchCost));
        }

        return exactDesignUsers <= 0 ? 0d : researchCost / exactDesignUsers;
    }

    public static MachineCostSummary Calculate(
        double fixedCosts,
        int producedUnits,
        double unitProductionCost,
        double salesRevenue)
    {
        if (!IsFiniteNonNegative(fixedCosts)
            || producedUnits < 0
            || !IsFiniteNonNegative(unitProductionCost)
            || !IsFiniteNonNegative(salesRevenue))
        {
            throw new ArgumentOutOfRangeException("Machine economics inputs must be >= 0.");
        }

        var totalCosts = fixedCosts + producedUnits * unitProductionCost;
        if (double.IsInfinity(totalCosts))
        {
            throw new OverflowException("Total machine costs overflowed.");
        }

        return new MachineCostSummary(fixedCosts, totalCosts, salesRevenue - totalCosts);
    }

    private static bool IsFiniteNonNegative(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
}
