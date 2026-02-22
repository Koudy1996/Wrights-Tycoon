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
    public AircraftCategory? RequiredCategory { get; set; }

    public double? MinimumReliability { get; set; }

    public double? MaximumTotalWeightKilograms { get; set; }

    public double? MinimumRangeKilometers { get; set; }

    public double? MinimumPayloadKilograms { get; set; }

    public int? MinimumCrew { get; set; }

    public double? MinimumSpeedAltitudeMeters { get; set; }

    public double? MinimumSpeedMetersPerSecond { get; set; }
}
