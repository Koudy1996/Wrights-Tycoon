using WrightTycoon.Core.Units;
using WrightTycoon.Core.Production;
using WrightTycoon.Core.Balance;

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
    double AerodynamicCleanlinessPercent = 0d,
    ProductionBreakdown? CostBreakdown = null,
    ProductionBreakdown? ProductionManDaysBreakdown = null)
{
    public double LaborCost => ProductionEconomyBalance.CalculateLaborCost(ProductionManDays);

    public double CompleteProductionCost => ProductionEconomyBalance.CalculateCompleteCost(ProductionCost, ProductionManDays);
}
