using System.Collections.Generic;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Factories;

public sealed record FactoryMonthlyProductionReport(
    int MonthIndex,
    SimulationDate Date,
    IReadOnlyList<LineProductionReport> LineReports,
    EngineFactoryProductionReport EngineFactoryReport,
    double TotalAvailableManDays,
    double TotalUsedManDays,
    double CapacityUtilization,
    int TotalAircraftProduced,
    IReadOnlyDictionary<string, int> WarehouseUnitsByDesign,
    int TotalWarehouseUnits,
    double TotalFactoryMaintenanceExpenses,
    double TotalActiveEquipmentExpenses)
{
    public double TotalPhysicalCapacityManDays { get; init; }
    public double TotalStaffCapacityManDays { get; init; }
    public double SupportProductionManDays { get; init; }
    public double PhysicalCapacityUtilization { get; init; }
    public bool UsesExternalEngineSupply { get; init; }
}
