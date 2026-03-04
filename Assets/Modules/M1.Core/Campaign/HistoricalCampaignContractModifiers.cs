using System;
using System.Collections.Generic;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.Campaign;

public sealed class HistoricalCampaignContractModifiers
{
    private readonly HashSet<AircraftCategory> _lockedCategories = new();

    public double CivilCategoryWeightMultiplier { get; private set; } = 1d;

    public double MilitaryCategoryWeightMultiplier { get; private set; } = 1d;

    public double CivilPriceMultiplier { get; private set; } = 1d;

    public double MilitaryPriceMultiplier { get; private set; } = 1d;

    public bool HasCategoryMixAdjustment =>
        !IsApproximatelyOne(CivilCategoryWeightMultiplier) ||
        !IsApproximatelyOne(MilitaryCategoryWeightMultiplier);

    public void ApplyCategoryMixMultiplier(string? target, double multiplier)
    {
        if (!IsValidMultiplier(multiplier))
        {
            return;
        }

        if (IsCivilTarget(target))
        {
            CivilCategoryWeightMultiplier *= multiplier;
        }
        else if (IsMilitaryTarget(target))
        {
            MilitaryCategoryWeightMultiplier *= multiplier;
        }
    }

    public void ApplyPriceMultiplier(string? target, double multiplier)
    {
        if (!IsValidMultiplier(multiplier))
        {
            return;
        }

        if (IsCivilTarget(target))
        {
            CivilPriceMultiplier *= multiplier;
        }
        else if (IsMilitaryTarget(target))
        {
            MilitaryPriceMultiplier *= multiplier;
        }
    }

    public double ResolveCategoryWeightMultiplier(AircraftCategory category) =>
        ContractBalance.IsMilitaryContractCategory(category)
            ? MilitaryCategoryWeightMultiplier
            : CivilCategoryWeightMultiplier;

    public double ResolvePriceMultiplier(AircraftCategory? category) =>
        category.HasValue && ContractBalance.IsMilitaryContractCategory(category.Value)
            ? MilitaryPriceMultiplier
            : CivilPriceMultiplier;

    public bool IsCategoryUnlocked(AircraftCategory category) => !_lockedCategories.Contains(category);

    public void LockCategory(AircraftCategory category)
    {
        _lockedCategories.Add(category);
    }

    public void UnlockCategory(AircraftCategory category)
    {
        _lockedCategories.Remove(category);
    }

    private static bool IsCivilTarget(string? target) =>
        string.Equals(target?.Trim(), "civil", StringComparison.OrdinalIgnoreCase);

    private static bool IsMilitaryTarget(string? target) =>
        string.Equals(target?.Trim(), "military", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidMultiplier(double multiplier) =>
        !double.IsNaN(multiplier) &&
        !double.IsInfinity(multiplier) &&
        multiplier > 0d;

    private static bool IsApproximatelyOne(double value) =>
        System.Math.Abs(value - 1d) <= 1e-9d;
}
