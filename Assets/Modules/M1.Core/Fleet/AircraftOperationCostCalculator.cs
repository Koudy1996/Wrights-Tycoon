using System;

namespace WrightTycoon.Core.Fleet;

public sealed record AircraftOperationCostQuote(
    double DepreciationPerHour,
    double SparePartsPerHour,
    double FuelPerHour,
    double TotalPerHour,
    int MaintenanceCycles);

/// <summary>Customer hourly ownership cost: purchase-price depreciation, consumed parts, and fuel only.</summary>
public static class AircraftOperationCostCalculator
{
    public static AircraftOperationCostQuote Calculate(double aircraftPrice, double expectedLifeHours,
        double fuelBurnKilogramsPerHour, double fuelPricePerKilogram, int serviceIntervalHours,
        int sparePartsPerCycle, double sparePartUnitPrice)
    {
        LifecycleCalculator.RequireFiniteNonNegative(aircraftPrice, nameof(aircraftPrice));
        LifecycleCalculator.RequireFiniteNonNegative(expectedLifeHours, nameof(expectedLifeHours));
        LifecycleCalculator.RequireFiniteNonNegative(fuelBurnKilogramsPerHour, nameof(fuelBurnKilogramsPerHour));
        LifecycleCalculator.RequireFiniteNonNegative(fuelPricePerKilogram, nameof(fuelPricePerKilogram));
        LifecycleCalculator.RequireFiniteNonNegative(sparePartUnitPrice, nameof(sparePartUnitPrice));
        if (expectedLifeHours <= 0d) throw new ArgumentOutOfRangeException(nameof(expectedLifeHours));
        LifecycleCalculator.ValidateServiceInterval(serviceIntervalHours);
        if (sparePartsPerCycle < 1) throw new ArgumentOutOfRangeException(nameof(sparePartsPerCycle));

        // Retirement at the exact endpoint precedes maintenance: 1,000 h at a 500 h interval needs one cycle.
        var cycleCount = System.Math.Max(0d, System.Math.Ceiling(expectedLifeHours / serviceIntervalHours) - 1d);
        if (cycleCount > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(expectedLifeHours));
        var cycles = (int)cycleCount;
        var depreciation = aircraftPrice / expectedLifeHours;
        var parts = cycles * (double)sparePartsPerCycle * sparePartUnitPrice / expectedLifeHours;
        var fuel = fuelBurnKilogramsPerHour * fuelPricePerKilogram;
        var total = depreciation + parts + fuel;
        LifecycleCalculator.RequireFiniteNonNegative(total, nameof(aircraftPrice));
        return new AircraftOperationCostQuote(depreciation, parts, fuel, total, cycles);
    }
}
