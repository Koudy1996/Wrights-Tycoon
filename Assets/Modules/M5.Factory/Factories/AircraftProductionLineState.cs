using System;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Factories;

public sealed class AircraftProductionLineState
{
    public AircraftProductionLineState(string lineId)
    {
        LineId = lineId ?? throw new ArgumentNullException(nameof(lineId));
        Validate();
    }

    public string LineId { get; }

    public string? AssignedDesignId { get; private set; }

    public int MonthlyProductionTargetUnits { get; private set; }

    public int TotalProductionTargetUnits { get; private set; }

    public int Employees { get; private set; }

    public double CarryOverManDays { get; private set; }

    public int LifetimeProducedUnits { get; private set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LineId))
        {
            throw new ArgumentException("Line id cannot be empty.", nameof(LineId));
        }

        if (Employees < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Employees), "Employees must be >= 0.");
        }

        if (MonthlyProductionTargetUnits < 0
            || MonthlyProductionTargetUnits > FactoryBalance.MaximumAircraftPerLinePerMonth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MonthlyProductionTargetUnits),
                $"Monthly production target must be between 0 and {FactoryBalance.MaximumAircraftPerLinePerMonth}.");
        }

        if (TotalProductionTargetUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TotalProductionTargetUnits), "Total production target must be >= 0.");
        }

        Numeric.ThrowIfNotFinite(CarryOverManDays, nameof(CarryOverManDays));
        if (CarryOverManDays < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(CarryOverManDays), "Carry-over man-days must be >= 0.");
        }

        if (LifetimeProducedUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LifetimeProducedUnits), "Lifetime produced units must be >= 0.");
        }
    }

    internal void AssignDesign(string? designId) => AssignedDesignId = string.IsNullOrWhiteSpace(designId) ? null : designId.Trim();

    internal void SetMonthlyProductionTargetUnits(int targetUnits)
    {
        if (targetUnits < 0 || targetUnits > FactoryBalance.MaximumAircraftPerLinePerMonth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetUnits),
                $"Monthly production target must be between 0 and {FactoryBalance.MaximumAircraftPerLinePerMonth}.");
        }

        MonthlyProductionTargetUnits = targetUnits;
    }

    internal void SetTotalProductionTargetUnits(int targetUnits)
    {
        if (targetUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetUnits), "Total production target must be >= 0.");
        }

        TotalProductionTargetUnits = targetUnits;
    }

    internal void SetAutoAssignedEmployees(int employees)
    {
        if (employees < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(employees), "Employees must be >= 0.");
        }

        Employees = employees;
    }

    internal void ApplyMonthResult(int producedUnits, double carryOverManDays)
    {
        if (producedUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(producedUnits), "Produced units must be >= 0.");
        }

        Numeric.ThrowIfNotFinite(carryOverManDays, nameof(carryOverManDays));
        if (carryOverManDays < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(carryOverManDays), "Carry-over man-days must be >= 0.");
        }

        LifetimeProducedUnits += producedUnits;
        CarryOverManDays = carryOverManDays;
    }

    internal void SetLifetimeProducedUnits(int producedUnits)
    {
        if (producedUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(producedUnits), "Lifetime produced units must be >= 0.");
        }

        LifetimeProducedUnits = producedUnits;
    }

    internal void SetCarryOverWithoutProduction(double carryOverManDays)
    {
        Numeric.ThrowIfNotFinite(carryOverManDays, nameof(carryOverManDays));
        if (carryOverManDays < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(carryOverManDays), "Carry-over man-days must be >= 0.");
        }

        CarryOverManDays = carryOverManDays;
    }
}
