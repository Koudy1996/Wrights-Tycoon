using System;
using System.Collections.Generic;
using System.Linq;

namespace WrightTycoon.Core.Company;

public sealed class CompanyRunStatistics
{
    private readonly HashSet<string> _developedAircraftTypeIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _developedAircraftVariantIds = new(StringComparer.Ordinal);

    public int DevelopedAircraftTypes => _developedAircraftTypeIds.Count;

    public int DevelopedAircraftVariants => _developedAircraftVariantIds.Count;

    public int TotalAircraftVariants => DevelopedAircraftTypes + DevelopedAircraftVariants;

    public int TotalAircraftProduced { get; private set; }

    public int TotalAircraftSold { get; private set; }

    public void RegisterDevelopedAircraftType(string researchId, bool isVariant)
    {
        if (string.IsNullOrWhiteSpace(researchId))
        {
            return;
        }

        var normalizedId = researchId.Trim();
        if (isVariant)
        {
            _developedAircraftTypeIds.Remove(normalizedId);
            _developedAircraftVariantIds.Add(normalizedId);
            return;
        }

        if (!_developedAircraftVariantIds.Contains(normalizedId))
        {
            _developedAircraftTypeIds.Add(normalizedId);
        }
    }

    public void RegisterAircraftProduced(int units)
    {
        if (units < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(units));
        }

        TotalAircraftProduced = checked(TotalAircraftProduced + units);
    }

    public void RegisterAircraftSold(int units)
    {
        if (units < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(units));
        }

        TotalAircraftSold = checked(TotalAircraftSold + units);
    }

    public CompanyRunStatisticsSnapshot CreateSnapshot() => new()
    {
        DevelopedAircraftTypeIds = _developedAircraftTypeIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
        DevelopedAircraftVariantIds = _developedAircraftVariantIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
        TotalAircraftProduced = TotalAircraftProduced,
        TotalAircraftSold = TotalAircraftSold,
    };

    public void RestoreSnapshot(CompanyRunStatisticsSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        _developedAircraftTypeIds.Clear();
        RestoreIds(snapshot.DevelopedAircraftTypeIds, _developedAircraftTypeIds);
        _developedAircraftVariantIds.Clear();
        RestoreIds(snapshot.DevelopedAircraftVariantIds, _developedAircraftVariantIds);
        _developedAircraftTypeIds.ExceptWith(_developedAircraftVariantIds);
        TotalAircraftProduced = System.Math.Max(snapshot.TotalAircraftProduced, 0);
        TotalAircraftSold = System.Math.Max(snapshot.TotalAircraftSold, 0);
    }

    private static void RestoreIds(IEnumerable<string>? source, HashSet<string> target)
    {
        if (source is null)
        {
            return;
        }

        foreach (var id in source)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                target.Add(id.Trim());
            }
        }
    }
}

public sealed class CompanyRunStatisticsSnapshot
{
    public List<string> DevelopedAircraftTypeIds { get; set; } = new();

    public List<string> DevelopedAircraftVariantIds { get; set; } = new();

    public int TotalAircraftProduced { get; set; }

    public int TotalAircraftSold { get; set; }
}

public static class BankruptcyPolicy
{
    public static bool IsBankrupt(double cash)
    {
        if (double.IsNaN(cash) || double.IsInfinity(cash))
        {
            throw new ArgumentOutOfRangeException(nameof(cash), "Cash must be finite.");
        }

        return cash < 0d;
    }
}
