using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Airframes;

public sealed record AirframePerformanceReport(
    Mass TotalWeight,
    double DragCoefficient,
    double LiftCoefficient,
    double Maneuverability,
    double ProductionCost,
    double ProductionManDays,
    double ReliabilityModifier,
    Force ReferenceLiftAtSeaLevel100Ms,
    Force ReferenceDragAtSeaLevel100Ms,
    double AircraftLengthMeters = 0d,
    double AirframeFrontalAreaSquareMeters = 0d,
    double EffectiveDragAreaSquareMeters = 0d,
    double AerodynamicCleanlinessPercent = 0d);
