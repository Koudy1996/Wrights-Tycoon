using System.Collections.Generic;
using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentContractBoardSnapshot
{
    public int ContractSequence { get; set; }

    public GovernmentContractSnapshot? ActiveContract { get; set; }

    public List<GovernmentContractSnapshot> ExpiredContracts { get; set; } = new();

    public List<GovernmentContractSnapshot> EvaluatedContracts { get; set; } = new();

    public List<GovernmentDeliveryContractSnapshot> ActiveDeliveryContracts { get; set; } = new();

    public List<GovernmentDeliveryContractSnapshot> ClosedDeliveryContracts { get; set; } = new();
}

public sealed class GovernmentContractSnapshot
{
    public double? LockedUnitPrice { get; set; }
    public WrightTycoon.Core.Fleet.ServiceOfferSnapshot? LockedServiceOffer { get; set; }
    public WrightTycoon.Core.Contracts.LifecycleCostQuote? LockedLifecycleCostQuote { get; set; }
    public string ContractId { get; set; } = string.Empty;

    public int GeneratedOnYear { get; set; }

    public int GeneratedOnMonth { get; set; }

    public GovernmentContractStatus Status { get; set; }

    public GovernmentContractRequirementsSnapshot Requirements { get; set; } = new();

    public int? AcceptedOnYear { get; set; }

    public int? AcceptedOnMonth { get; set; }

    public double UpfrontFundingGranted { get; set; }

    public GovernmentContractSubmissionCandidateSnapshot? AcceptedSubmission { get; set; }

    public GovernmentContractEvaluationResultSnapshot? EvaluationResult { get; set; }
}

public sealed class GovernmentContractRequirementsSnapshot
{
    public WrightTycoon.Core.Contracts.ContractEconomicSnapshot? EconomicSnapshot { get; set; }
    public int LifecycleModelVersion { get; set; }
    public double MinimumExpectedLifeHours { get; set; }
    public int MaximumServiceIntervalHours { get; set; } = 1000;
    public int MinimumSupportMonths { get; set; } = 120;
    public double UtilizationHoursPerMonth { get; set; } = 100d;
    public double AverageFlightHours { get; set; } = 2d;
    public string OperatingProfileId { get; set; } = "civil";
    public WrightTycoon.Core.Contracts.ProcurementPreference ProcurementPreference { get; set; }
    public double MaximumOperatingCostPerUnit { get; set; }
    public AircraftCategory RequiredAircraftType { get; set; }

    public GovernmentContractSpeedAltitudeBand SpeedAltitudeBand { get; set; }

    public double MinimumMaximumSpeedMetersPerSecond { get; set; }

    public double MinimumRangeKilometers { get; set; }

    public double MinimumPayloadKilograms { get; set; }

    public double MinimumReliability { get; set; }

    public double MinimumManeuverability { get; set; }

    public int MinimumCrewSize { get; set; }

    public double MaximumRequiredRunwayLengthMeters { get; set; }
}

public sealed class GovernmentContractSubmissionCandidateSnapshot
{
    public string DesignId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public AircraftLifecycleStage LifecycleStage { get; set; }

    public GovernmentContractAircraftMetricsSnapshot Metrics { get; set; } = new();

    public GovernmentContractDevelopmentPriceBreakdownSnapshot DevelopmentPriceBreakdown { get; set; } = new();
}

public sealed class GovernmentContractAircraftMetricsSnapshot
{
    public double ExpectedLifeHours { get; set; } = 0d;
    public int ServiceIntervalHours { get; set; } = 1000;
    public int GuaranteedSupportMonths { get; set; } = 120;
    public double FuelBurnKilogramsPerHour { get; set; } = 0d;
    public double UnitProductionCost { get; set; } = 0d;
    public double FuelPricePerKilogram { get; set; } = WrightTycoon.Core.Contracts.LifecycleCostCalculator.DefaultFuelPricePerKilogram;
    public double? ServicePricePerEvent { get; set; }
    public int MaintenanceModelVersion { get; set; } = 2;
    public WrightTycoon.Core.Fleet.ServiceOfferSnapshot? CombinedServiceOffer { get; set; }
    // Retained for backward compatibility with saves created before modular categories.
    public AircraftCategory AircraftType { get; set; }

    public bool UsesDynamicCategoryQualification { get; set; }

    public List<AircraftCategory> QualifiedAircraftTypes { get; set; } = new();

    public double MaximumSpeedLowAltitudeMetersPerSecond { get; set; }

    public double MaximumSpeedMediumAltitudeMetersPerSecond { get; set; }

    public double MaximumSpeedHighAltitudeMetersPerSecond { get; set; }

    public double RangeKilometers { get; set; }

    public double PayloadKilograms { get; set; }

    public double Reliability { get; set; }

    public double Maneuverability { get; set; }

    public int CrewSize { get; set; }

    public double RequiredRunwayLengthMeters { get; set; }

    public double DevelopmentCost { get; set; }
}

public sealed class GovernmentContractDevelopmentPriceBreakdownSnapshot
{
    public double EnginePrice { get; set; }

    public double FamilyPrice { get; set; }

    public double TypePrice { get; set; }
}

public sealed class GovernmentContractEvaluationResultSnapshot
{
    public int EvaluatedOnYear { get; set; }

    public int EvaluatedOnMonth { get; set; }

    public bool IsSuccess { get; set; }

    public double FinalProbability { get; set; }

    public double RandomRoll { get; set; }

    public List<GovernmentContractParameterBonusSnapshot> ParameterBonuses { get; set; } = new();
}

public sealed class GovernmentContractParameterBonusSnapshot
{
    public GovernmentContractParameter Parameter { get; set; }

    public double RequiredValue { get; set; }

    public double ActualValue { get; set; }

    public double ExcessRatio { get; set; }

    public double BonusValue { get; set; }
}

public sealed class GovernmentDeliveryContractSnapshot
{
    public WrightTycoon.Core.Fleet.ServiceOfferSnapshot? ServiceOffer { get; set; }
    public WrightTycoon.Core.Contracts.LifecycleCostQuote? LifecycleCostQuote { get; set; }
    public GovernmentContractRequirementsSnapshot? LifecycleRequirements { get; set; }
    public GovernmentContractAircraftMetricsSnapshot? LifecycleAircraftMetrics { get; set; }
    public string DeliveryId { get; set; } = string.Empty;

    public string GovernmentContractId { get; set; } = string.Empty;

    public string AircraftDesignId { get; set; } = string.Empty;

    public int StartedOnYear { get; set; }

    public int StartedOnMonth { get; set; }

    public int EndsOnYear { get; set; }

    public int EndsOnMonth { get; set; }

    public int TotalUnitsRequired { get; set; }

    public int DeliveredUnits { get; set; }

    public double UnitPrice { get; set; }

    public double TotalContractValue { get; set; }

    public GovernmentDeliveryContractStatus Status { get; set; }

    public int? CompletedOnYear { get; set; }

    public int? CompletedOnMonth { get; set; }

    public bool CompletionBonusGranted { get; set; }
}
