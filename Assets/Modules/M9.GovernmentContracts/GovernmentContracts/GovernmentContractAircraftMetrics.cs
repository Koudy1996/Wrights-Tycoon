using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.GovernmentContracts;

public readonly struct GovernmentContractAircraftMetrics
{
    public GovernmentContractAircraftMetrics(
        AircraftCategory AircraftType,
        double MaximumSpeedLowAltitudeMetersPerSecond,
        double MaximumSpeedMediumAltitudeMetersPerSecond,
        double MaximumSpeedHighAltitudeMetersPerSecond,
        double RangeKilometers,
        double PayloadKilograms,
        double Reliability,
        double Maneuverability,
        int CrewSize,
        double RequiredRunwayLengthMeters,
        double DevelopmentCost)
        : this(
            AircraftType,
            MaximumSpeedLowAltitudeMetersPerSecond,
            MaximumSpeedMediumAltitudeMetersPerSecond,
            MaximumSpeedHighAltitudeMetersPerSecond,
            RangeKilometers,
            PayloadKilograms,
            Reliability,
            Maneuverability,
            CrewSize,
            RequiredRunwayLengthMeters,
            DevelopmentCost,
            new[] { AircraftType })
    {
    }

    public GovernmentContractAircraftMetrics(
        AircraftCategory AircraftType,
        double MaximumSpeedLowAltitudeMetersPerSecond,
        double MaximumSpeedMediumAltitudeMetersPerSecond,
        double MaximumSpeedHighAltitudeMetersPerSecond,
        double RangeKilometers,
        double PayloadKilograms,
        double Reliability,
        double Maneuverability,
        int CrewSize,
        double RequiredRunwayLengthMeters,
        double DevelopmentCost,
        IReadOnlyList<AircraftCategory>? QualifiedAircraftTypes,
        double ExpectedLifeHours = 0d,
        int ServiceIntervalHours = 1000,
        int GuaranteedSupportMonths = 120,
        double FuelBurnKilogramsPerHour = 0d,
        double UnitProductionCost = 0d,
        double FuelPricePerKilogram = WrightTycoon.Core.Contracts.LifecycleCostCalculator.DefaultFuelPricePerKilogram,
        double? ServicePricePerEvent = null,
        int MaintenanceModelVersion = 2,
        WrightTycoon.Core.Fleet.ServiceOfferSnapshot? CombinedServiceOffer = null)
    {
        this.AircraftType = AircraftType;
        this.ExpectedLifeHours = ExpectedLifeHours;
        this.ServiceIntervalHours = ServiceIntervalHours;
        this.GuaranteedSupportMonths = GuaranteedSupportMonths;
        this.FuelBurnKilogramsPerHour = FuelBurnKilogramsPerHour;
        this.UnitProductionCost = UnitProductionCost;
        this.FuelPricePerKilogram = FuelPricePerKilogram;
        this.ServicePricePerEvent = ServicePricePerEvent;
        this.MaintenanceModelVersion = MaintenanceModelVersion;
        this.CombinedServiceOffer = CombinedServiceOffer;
        this.MaximumSpeedLowAltitudeMetersPerSecond = MaximumSpeedLowAltitudeMetersPerSecond;
        this.MaximumSpeedMediumAltitudeMetersPerSecond = MaximumSpeedMediumAltitudeMetersPerSecond;
        this.MaximumSpeedHighAltitudeMetersPerSecond = MaximumSpeedHighAltitudeMetersPerSecond;
        this.RangeKilometers = RangeKilometers;
        this.PayloadKilograms = PayloadKilograms;
        this.Reliability = Reliability;
        this.Maneuverability = Maneuverability;
        this.CrewSize = CrewSize;
        this.RequiredRunwayLengthMeters = RequiredRunwayLengthMeters;
        this.DevelopmentCost = DevelopmentCost;
        this.QualifiedAircraftTypes = QualifiedAircraftTypes is null
            ? new[] { AircraftType }
            : QualifiedAircraftTypes.Distinct().ToArray();
    }

    // Legacy single-category value retained for old snapshots; eligibility uses explicit metrics.
    public AircraftCategory AircraftType { get; }
    public double ExpectedLifeHours { get; }
    public int ServiceIntervalHours { get; }
    public int GuaranteedSupportMonths { get; }
    public double FuelBurnKilogramsPerHour { get; }
    public double UnitProductionCost { get; }
    public double FuelPricePerKilogram { get; }
    public double? ServicePricePerEvent { get; }
    public int MaintenanceModelVersion { get; }
    public WrightTycoon.Core.Fleet.ServiceOfferSnapshot? CombinedServiceOffer { get; }

    public WrightTycoon.Core.Fleet.ServiceOfferSnapshot CreateServiceOffer()
    {
        if (MaintenanceModelVersion != 2)
            throw new InvalidOperationException("Only separate airframe and installed-engine maintenance is supported.");
        return CombinedServiceOffer ?? throw new InvalidOperationException("Combined engine maintenance terms are missing.");
    }

    public WrightTycoon.Core.Fleet.ServiceOfferSnapshot CreateContractServiceOffer(GovernmentContractRequirements requirements) =>
        requirements.LifecycleModelVersion > 0
            ? CreateServiceOffer() with { GuaranteedSupportMonths = requirements.MinimumSupportMonths }
            : CreateServiceOffer();

    public WrightTycoon.Core.Contracts.LifecycleCostQuote QuoteForContract(GovernmentContractRequirements requirements, double purchasePrice) =>
        WrightTycoon.Core.Contracts.LifecycleCostCalculator.Calculate(WrightTycoon.Core.Contracts.LifecycleCostCalculator.ExpectedOperatingLifeHours(requirements.MinimumExpectedLifeHours, ExpectedLifeHours),
            purchasePrice, FuelBurnKilogramsPerHour, FuelPricePerKilogram,
            CreateContractServiceOffer(requirements), requirements.UtilizationHoursPerMonth);

    public double MaximumSpeedLowAltitudeMetersPerSecond { get; }

    public double MaximumSpeedMediumAltitudeMetersPerSecond { get; }

    public double MaximumSpeedHighAltitudeMetersPerSecond { get; }

    public double RangeKilometers { get; }

    public double PayloadKilograms { get; }

    public double Reliability { get; }

    public double Maneuverability { get; }

    public int CrewSize { get; }

    public double RequiredRunwayLengthMeters { get; }

    public double DevelopmentCost { get; }

    public IReadOnlyList<AircraftCategory> QualifiedAircraftTypes { get; }

    public double GetMaximumSpeedForBand(GovernmentContractSpeedAltitudeBand band)
    {
        return band switch
        {
            GovernmentContractSpeedAltitudeBand.Low => MaximumSpeedLowAltitudeMetersPerSecond,
            GovernmentContractSpeedAltitudeBand.Medium => MaximumSpeedMediumAltitudeMetersPerSecond,
            GovernmentContractSpeedAltitudeBand.High => MaximumSpeedHighAltitudeMetersPerSecond,
            _ => MaximumSpeedMediumAltitudeMetersPerSecond,
        };
    }

    public void Validate()
    {
        if (MaintenanceModelVersion != 2)
            throw new ArgumentOutOfRangeException(nameof(MaintenanceModelVersion));
        if (ExpectedLifeHours > 0d || CombinedServiceOffer is not null)
        {
            if (CombinedServiceOffer is null || CombinedServiceOffer.MaintenanceModelVersion != 2)
                throw new ArgumentException("Combined engine maintenance terms are required.");
            CombinedServiceOffer.Validate();
        }
        WrightTycoon.Core.Fleet.LifecycleCalculator.RequireFiniteNonNegative(ExpectedLifeHours, nameof(ExpectedLifeHours));
        WrightTycoon.Core.Fleet.LifecycleCalculator.RequireFiniteNonNegative(FuelBurnKilogramsPerHour, nameof(FuelBurnKilogramsPerHour));
        WrightTycoon.Core.Fleet.LifecycleCalculator.RequireFiniteNonNegative(UnitProductionCost, nameof(UnitProductionCost));
        WrightTycoon.Core.Fleet.LifecycleCalculator.RequireFiniteNonNegative(FuelPricePerKilogram, nameof(FuelPricePerKilogram));
        if (ServicePricePerEvent.HasValue)
            WrightTycoon.Core.Fleet.LifecycleCalculator.RequireFiniteNonNegative(ServicePricePerEvent.Value, nameof(ServicePricePerEvent));
        if (ExpectedLifeHours > 0d)
        {
            WrightTycoon.Core.Fleet.LifecycleCalculator.ValidateServiceInterval(ServiceIntervalHours);
            if (GuaranteedSupportMonths <= 0) throw new ArgumentOutOfRangeException(nameof(GuaranteedSupportMonths));
        }
        _ = AircraftCategoryCatalog.GetProfile(AircraftType);

        Numeric.ThrowIfNotFinite(MaximumSpeedLowAltitudeMetersPerSecond, nameof(MaximumSpeedLowAltitudeMetersPerSecond));
        Numeric.ThrowIfNotFinite(MaximumSpeedMediumAltitudeMetersPerSecond, nameof(MaximumSpeedMediumAltitudeMetersPerSecond));
        Numeric.ThrowIfNotFinite(MaximumSpeedHighAltitudeMetersPerSecond, nameof(MaximumSpeedHighAltitudeMetersPerSecond));
        Numeric.ThrowIfNotFinite(RangeKilometers, nameof(RangeKilometers));
        Numeric.ThrowIfNotFinite(PayloadKilograms, nameof(PayloadKilograms));
        Numeric.ThrowIfNotFinite(Reliability, nameof(Reliability));
        Numeric.ThrowIfNotFinite(Maneuverability, nameof(Maneuverability));
        Numeric.ThrowIfNotFinite(RequiredRunwayLengthMeters, nameof(RequiredRunwayLengthMeters));
        Numeric.ThrowIfNotFinite(DevelopmentCost, nameof(DevelopmentCost));

        if (MaximumSpeedLowAltitudeMetersPerSecond <= 0d
            || MaximumSpeedMediumAltitudeMetersPerSecond <= 0d
            || MaximumSpeedHighAltitudeMetersPerSecond <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumSpeedLowAltitudeMetersPerSecond), "All speed samples must be > 0.");
        }

        if (RangeKilometers <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(RangeKilometers), "Range must be > 0.");
        }

        if (PayloadKilograms < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(PayloadKilograms), "Payload must be >= 0.");
        }

        if (double.IsNaN(Reliability) || double.IsInfinity(Reliability) || Reliability < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(Reliability), "Reliability must be finite and nonnegative.");
        }

        if (Maneuverability < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(Maneuverability), "Maneuverability must be >= 0.");
        }

        if (CrewSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(CrewSize), "Crew size must be >= 1.");
        }

        if (RequiredRunwayLengthMeters <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(RequiredRunwayLengthMeters), "Required runway length must be > 0.");
        }

        if (DevelopmentCost < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(DevelopmentCost), "Development cost must be >= 0.");
        }

        for (var index = 0; index < QualifiedAircraftTypes.Count; index++)
        {
            _ = AircraftCategoryCatalog.GetProfile(QualifiedAircraftTypes[index]);
        }
    }
}
