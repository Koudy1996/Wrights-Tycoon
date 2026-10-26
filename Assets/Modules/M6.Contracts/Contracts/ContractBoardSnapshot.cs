using System.Collections.Generic;
using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.Contracts;

public sealed class ContractBoardSnapshot
{
    public int CurrentMonthIndex { get; set; }

    public List<ContractSnapshot> OpenContracts { get; set; } = new();

    public List<ContractSnapshot> DeliveredContracts { get; set; } = new();

    public List<ContractSnapshot> CanceledContracts { get; set; } = new();
}

public sealed class ContractSnapshot
{
    public string Id { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public int QuantityUnits { get; set; }

    public int DeadlineMonths { get; set; }

    public int CreatedMonthIndex { get; set; }

    public double UnitPrice { get; set; }

    public double EstimatedMinimumUnitCost { get; set; }

    public double AppliedMarkup { get; set; }

    public ContractStatus Status { get; set; }

    public int RemainingMonths { get; set; }

    public int? DeliveredMonthIndex { get; set; }

    public int? CanceledMonthIndex { get; set; }

    public string? CancellationReason { get; set; }

    public ContractRequirementsSnapshot Requirements { get; set; } = new();
}

public sealed class ContractRequirementsSnapshot
{
    public ContractEconomicSnapshot? EconomicSnapshot { get; set; }
    public int LifecycleModelVersion { get; set; }
    public int MaintenanceModelVersion { get; set; } = 2;
    public double MinimumExpectedLifeHours { get; set; }
    public int MaximumServiceIntervalHours { get; set; } = 1000;
    public int MinimumSupportMonths { get; set; } = 120;
    public double UtilizationHoursPerMonth { get; set; } = 100d;
    public double AverageFlightHours { get; set; } = 2d;
    public string OperatingProfileId { get; set; } = "civil";
    public WrightTycoon.Core.Contracts.ProcurementPreference ProcurementPreference { get; set; }
    public double MaximumOperatingCostPerUnit { get; set; }
    public AircraftCategory? RequiredCategory { get; set; }

    public double? MinimumReliability { get; set; }

    public double? MaximumTotalWeightKilograms { get; set; }

    public double? MinimumRangeKilometers { get; set; }

    public double? MinimumPayloadKilograms { get; set; }

    public double? MinimumManeuverability { get; set; }

    public int? MinimumCrew { get; set; }

    public double? MinimumSpeedAltitudeMeters { get; set; }

    public double? MinimumSpeedMetersPerSecond { get; set; }

    public double? MaximumFuelConsumptionKilogramsPerHour { get; set; }
    public double? MaximumRequiredRunwayLengthMeters { get; set; }
}
