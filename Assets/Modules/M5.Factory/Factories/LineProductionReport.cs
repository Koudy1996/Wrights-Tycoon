namespace WrightTycoon.Core.Factories;

public sealed record LineProductionReport(
    string LineId,
    string? AssignedDesignId,
    string AssignedDesignName,
    int Employees,
    int TargetUnits,
    int LineToolLevel,
    int ProducedUnits,
    double AvailableManDays,
    double UsedManDays,
    double CarryOverManDays,
    int CapacityUnitsAtCurrentStaff,
    bool IsBlocked,
    string StatusMessage,
    int RequiredFactoryToolLevel,
    int RequiredLineToolLevel,
    int RequiredEngines,
    int EngineFactoryEmployees,
    int EngineDiscardedEngines,
    int WarehouseUnitsAfterProduction);
