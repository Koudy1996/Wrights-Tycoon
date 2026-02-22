using System;
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

    public int Employees { get; private set; } = 10;

    public int QueuedUnits { get; private set; }

    public double CarryOverManDays { get; private set; }

    public int LineToolLevel { get; private set; } = 1;

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

        if (QueuedUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(QueuedUnits), "Queued units must be >= 0.");
        }

        Numeric.ThrowIfNotFinite(CarryOverManDays, nameof(CarryOverManDays));
        if (CarryOverManDays < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(CarryOverManDays), "Carry-over man-days must be >= 0.");
        }

        if (LineToolLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(LineToolLevel), "Line tool level must be >= 1.");
        }
    }

    public void AssignDesign(string? designId) => AssignedDesignId = string.IsNullOrWhiteSpace(designId) ? null : designId.Trim();

    public void SetEmployees(int employees)
    {
        if (employees < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(employees), "Employees must be >= 0.");
        }

        Employees = employees;
    }

    public void SetQueuedUnits(int queuedUnits)
    {
        if (queuedUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queuedUnits), "Queued units must be >= 0.");
        }

        QueuedUnits = queuedUnits;
    }

    public void AddQueuedUnits(int units)
    {
        var next = QueuedUnits + units;
        if (next < 0)
        {
            next = 0;
        }

        QueuedUnits = next;
    }

    public void UpgradeLineToolLevel(int targetLevel)
    {
        if (targetLevel < LineToolLevel)
        {
            throw new InvalidOperationException("Line tooling cannot be downgraded.");
        }

        if (targetLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(targetLevel), "Line tool level must be >= 1.");
        }

        LineToolLevel = targetLevel;
    }

    public void ApplyMonthResult(int producedUnits, double carryOverManDays)
    {
        if (producedUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(producedUnits), "Produced units must be >= 0.");
        }

        if (producedUnits > QueuedUnits)
        {
            throw new ArgumentOutOfRangeException(nameof(producedUnits), "Produced units cannot exceed queued units.");
        }

        Numeric.ThrowIfNotFinite(carryOverManDays, nameof(carryOverManDays));
        if (carryOverManDays < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(carryOverManDays), "Carry-over man-days must be >= 0.");
        }

        QueuedUnits -= producedUnits;
        LifetimeProducedUnits += producedUnits;
        CarryOverManDays = carryOverManDays;
    }

    public void SetCarryOverWithoutProduction(double carryOverManDays)
    {
        Numeric.ThrowIfNotFinite(carryOverManDays, nameof(carryOverManDays));
        if (carryOverManDays < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(carryOverManDays), "Carry-over man-days must be >= 0.");
        }

        CarryOverManDays = carryOverManDays;
    }
}
