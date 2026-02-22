namespace WrightTycoon.Core.Engines;

public readonly record struct EngineAltitudePerformance(
    double AltitudeMeters,
    double Output,
    double FuelConsumptionKilogramsPerHour,
    double ThrustNewtons = 0d);
