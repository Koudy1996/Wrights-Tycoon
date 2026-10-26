using System;
using WrightTycoon.Core.Math;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.GovernmentContracts;

public sealed class GovernmentDeliveryContract
{
    public GovernmentDeliveryContract(
        string deliveryId,
        string governmentContractId,
        string aircraftDesignId,
        SimulationDate startedOn,
        SimulationDate endsOn,
        int totalUnitsRequired,
        double unitPrice,
        double totalContractValue,
        GovernmentContractRequirements? lifecycleRequirements = null,
        GovernmentContractAircraftMetrics? lifecycleAircraftMetrics = null,
        WrightTycoon.Core.Fleet.ServiceOfferSnapshot? serviceOffer = null,
        WrightTycoon.Core.Contracts.LifecycleCostQuote? lifecycleCostQuote = null)
    {
        DeliveryId = string.IsNullOrWhiteSpace(deliveryId)
            ? throw new ArgumentException("Delivery id cannot be empty.", nameof(deliveryId))
            : deliveryId.Trim();
        GovernmentContractId = string.IsNullOrWhiteSpace(governmentContractId)
            ? throw new ArgumentException("Government contract id cannot be empty.", nameof(governmentContractId))
            : governmentContractId.Trim();
        AircraftDesignId = string.IsNullOrWhiteSpace(aircraftDesignId)
            ? throw new ArgumentException("Aircraft design id cannot be empty.", nameof(aircraftDesignId))
            : aircraftDesignId.Trim();
        StartedOn = startedOn;
        EndsOn = endsOn;
        TotalUnitsRequired = totalUnitsRequired;
        UnitPrice = unitPrice;
        TotalContractValue = totalContractValue;
        LifecycleRequirements = lifecycleRequirements;
        LifecycleAircraftMetrics = lifecycleAircraftMetrics;
        ServiceOffer = serviceOffer;
        LifecycleCostQuote = lifecycleCostQuote;
        if (ServiceOffer is null && lifecycleRequirements?.LifecycleModelVersion > 0 && lifecycleAircraftMetrics.HasValue)
        {
            var metrics = lifecycleAircraftMetrics.Value;
            ServiceOffer = metrics.CreateServiceOffer();
        }
        Status = GovernmentDeliveryContractStatus.Active;

        Validate();
    }

    public string DeliveryId { get; }

    public string GovernmentContractId { get; }

    public string AircraftDesignId { get; }

    public SimulationDate StartedOn { get; }

    public SimulationDate EndsOn { get; }

    public int TotalUnitsRequired { get; }

    public int DeliveredUnits { get; private set; }

    public int RemainingUnits => System.Math.Max(TotalUnitsRequired - DeliveredUnits, 0);

    public double UnitPrice { get; }
    public GovernmentContractRequirements? LifecycleRequirements { get; }
    public GovernmentContractAircraftMetrics? LifecycleAircraftMetrics { get; }
    public WrightTycoon.Core.Fleet.ServiceOfferSnapshot? ServiceOffer { get; }
    public WrightTycoon.Core.Contracts.LifecycleCostQuote? LifecycleCostQuote { get; }

    public double TotalContractValue { get; }

    public GovernmentDeliveryContractStatus Status { get; private set; }

    public SimulationDate? CompletedOn { get; private set; }

    public bool CompletionBonusGranted { get; private set; }

    public void Validate()
    {
        if (TotalUnitsRequired <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TotalUnitsRequired), "Total units must be > 0.");
        }

        Numeric.ThrowIfNotFinite(UnitPrice, nameof(UnitPrice));
        if (UnitPrice < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(UnitPrice), "Unit price must be >= 0.");
        }

        Numeric.ThrowIfNotFinite(TotalContractValue, nameof(TotalContractValue));
        if (TotalContractValue < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(TotalContractValue), "Total contract value must be >= 0.");
        }
    }

    public int RegisterDeliveredUnits(int units, SimulationDate month)
    {
        if (Status != GovernmentDeliveryContractStatus.Active)
        {
            return 0;
        }

        if (units <= 0)
        {
            return 0;
        }

        var deliveredNow = System.Math.Min(units, RemainingUnits);
        if (deliveredNow <= 0)
        {
            return 0;
        }

        DeliveredUnits += deliveredNow;
        if (DeliveredUnits >= TotalUnitsRequired)
        {
            DeliveredUnits = TotalUnitsRequired;
            Status = GovernmentDeliveryContractStatus.Completed;
            CompletedOn = month;
        }

        return deliveredNow;
    }

    public void MarkEnded(SimulationDate month)
    {
        if (Status != GovernmentDeliveryContractStatus.Active)
        {
            return;
        }

        Status = GovernmentDeliveryContractStatus.Ended;
        CompletedOn = month;
    }

    public void MarkCompletionBonusGranted() => CompletionBonusGranted = true;
}
