using System;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Categories;

public sealed class AircraftCategoryProfile
{
    public AircraftCategoryProfile(
        AircraftCategory category,
        string displayName,
        int minCrew,
        int maxCrew,
        double minMaximumWeightKilograms,
        double maxMaximumWeightKilograms,
        double minRequiredSpeedKilometersPerHour,
        double maxRequiredSpeedKilometersPerHour,
        double minRequiredRangeKilometers,
        double maxRequiredRangeKilometers,
        double minRequiredPayloadKilograms,
        double maxRequiredPayloadKilograms,
        double minRequiredManeuverability,
        double maxRequiredManeuverability,
        double weightModifierKilograms = 0d,
        double speedModifierMetersPerSecond = 0d,
        double reliabilityModifierDelta = 0d,
        double fuelConsumptionMultiplier = 1d)
    {
        Category = category;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        MinCrew = minCrew;
        MaxCrew = maxCrew;
        MinMaximumWeightKilograms = minMaximumWeightKilograms;
        MaxMaximumWeightKilograms = maxMaximumWeightKilograms;
        MinRequiredSpeedKilometersPerHour = minRequiredSpeedKilometersPerHour;
        MaxRequiredSpeedKilometersPerHour = maxRequiredSpeedKilometersPerHour;
        MinRequiredRangeKilometers = minRequiredRangeKilometers;
        MaxRequiredRangeKilometers = maxRequiredRangeKilometers;
        MinRequiredPayloadKilograms = minRequiredPayloadKilograms;
        MaxRequiredPayloadKilograms = maxRequiredPayloadKilograms;
        MinRequiredManeuverability = minRequiredManeuverability;
        MaxRequiredManeuverability = maxRequiredManeuverability;
        WeightModifierKilograms = weightModifierKilograms;
        SpeedModifierMetersPerSecond = speedModifierMetersPerSecond;
        ReliabilityModifierDelta = reliabilityModifierDelta;
        FuelConsumptionMultiplier = fuelConsumptionMultiplier;

        Validate();
    }

    public AircraftCategory Category { get; }

    public string DisplayName { get; }

    public int MinCrew { get; }

    public int MaxCrew { get; }

    public double MinMaximumWeightKilograms { get; }

    public double MaxMaximumWeightKilograms { get; }

    public double MinRequiredSpeedKilometersPerHour { get; }

    public double MaxRequiredSpeedKilometersPerHour { get; }

    public double MinRequiredRangeKilometers { get; }

    public double MaxRequiredRangeKilometers { get; }

    public double MinRequiredPayloadKilograms { get; }

    public double MaxRequiredPayloadKilograms { get; }

    public double MinRequiredManeuverability { get; }

    public double MaxRequiredManeuverability { get; }

    public double WeightModifierKilograms { get; }

    public double SpeedModifierMetersPerSecond { get; }

    public double ReliabilityModifierDelta { get; }

    public double FuelConsumptionMultiplier { get; }

    public double MinRequiredSpeedMetersPerSecond => MinRequiredSpeedKilometersPerHour / 3.6d;

    public double MaxRequiredSpeedMetersPerSecond => MaxRequiredSpeedKilometersPerHour / 3.6d;

    public bool HasPassengerCapacityRequirement =>
        AircraftCategoryRequirementBalance.GetPolicy(Category).RoleRequirement ==
        AircraftCategoryRoleRequirement.PassengerCabin;

    public int GetMinimumPassengerCapacity(double kilogramsPerPassenger = PlaneDesignerBalance.Categories.PayloadKilogramsPerPassenger)
    {
        if (!HasPassengerCapacityRequirement)
        {
            return 0;
        }

        Numeric.ThrowIfNotFinite(kilogramsPerPassenger, nameof(kilogramsPerPassenger));
        if (kilogramsPerPassenger <= Numeric.Epsilon)
        {
            throw new ArgumentOutOfRangeException(nameof(kilogramsPerPassenger), "Kilograms per passenger must be greater than zero.");
        }

        return (int)System.Math.Floor(MinRequiredPayloadKilograms / kilogramsPerPassenger);
    }

    public int GetMaximumPassengerCapacity(double kilogramsPerPassenger = PlaneDesignerBalance.Categories.PayloadKilogramsPerPassenger)
    {
        if (!HasPassengerCapacityRequirement)
        {
            return 0;
        }

        Numeric.ThrowIfNotFinite(kilogramsPerPassenger, nameof(kilogramsPerPassenger));
        if (kilogramsPerPassenger <= Numeric.Epsilon)
        {
            throw new ArgumentOutOfRangeException(nameof(kilogramsPerPassenger), "Kilograms per passenger must be greater than zero.");
        }

        var minimumPassengers = GetMinimumPassengerCapacity(kilogramsPerPassenger);
        var maximumPassengers = (int)System.Math.Floor(MaxRequiredPayloadKilograms / kilogramsPerPassenger);
        return System.Math.Max(minimumPassengers, maximumPassengers);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(DisplayName));
        }

        if (MinCrew < 1 || MaxCrew < MinCrew)
        {
            throw new ArgumentOutOfRangeException(nameof(MinCrew), "Crew range is invalid.");
        }

        ValidateRange(MinMaximumWeightKilograms, MaxMaximumWeightKilograms, nameof(MinMaximumWeightKilograms), nameof(MaxMaximumWeightKilograms));
        ValidateRange(MinRequiredSpeedKilometersPerHour, MaxRequiredSpeedKilometersPerHour, nameof(MinRequiredSpeedKilometersPerHour), nameof(MaxRequiredSpeedKilometersPerHour));
        ValidateRange(MinRequiredRangeKilometers, MaxRequiredRangeKilometers, nameof(MinRequiredRangeKilometers), nameof(MaxRequiredRangeKilometers));
        ValidateRange(MinRequiredPayloadKilograms, MaxRequiredPayloadKilograms, nameof(MinRequiredPayloadKilograms), nameof(MaxRequiredPayloadKilograms));
        ValidateRange(MinRequiredManeuverability, MaxRequiredManeuverability, nameof(MinRequiredManeuverability), nameof(MaxRequiredManeuverability));

        Numeric.ThrowIfNotFinite(WeightModifierKilograms, nameof(WeightModifierKilograms));
        Numeric.ThrowIfNotFinite(SpeedModifierMetersPerSecond, nameof(SpeedModifierMetersPerSecond));
        Numeric.ThrowIfNotFinite(ReliabilityModifierDelta, nameof(ReliabilityModifierDelta));
        Numeric.ThrowIfNotFinite(FuelConsumptionMultiplier, nameof(FuelConsumptionMultiplier));
        if (FuelConsumptionMultiplier <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(FuelConsumptionMultiplier), "Fuel consumption multiplier must be greater than zero.");
        }
    }

    private static void ValidateRange(double minValue, double maxValue, string minName, string maxName)
    {
        Numeric.ThrowIfNotFinite(minValue, minName);
        Numeric.ThrowIfNotFinite(maxValue, maxName);
        if (minValue < 0d)
        {
            throw new ArgumentOutOfRangeException(minName, $"{minName} must be >= 0.");
        }

        if (maxValue < minValue)
        {
            throw new ArgumentException($"{minName} must be less than or equal to {maxName}.");
        }
    }
}

