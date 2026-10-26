using System;
using System.Linq;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Accounting;
using System.Collections.Generic;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private FleetRegistry _fleet = new();
    private MaintenanceFinancialLedger _maintenanceFinance = new();
    private FleetMonthReport? _lastFleetReport;
    private SimulationDate _fleetRecordStartDate = SimulationDate.WrightTycoonStart;

    private void RegisterCivilFleetDelivery(Contract contract, FactoryAircraftTypeCatalogItem item, int count)
    {
        var r = contract.Requirements;
        var spec = AircraftLifecycleSpecification.FromAircraft(item.Aircraft, _headquarters.CompanyQualityLevel);
        var saved = _savedAircraftDesigns.FirstOrDefault(d => string.Equals(d.CatalogItem.DesignId, item.DesignId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Delivered aircraft identity is unavailable for engine maintenance.");
        var serviceOffer = BuildCombinedMaintenanceOffer(saved, spec.Reliability) with
        {
            GuaranteedSupportMonths = r.LifecycleModelVersion > 0 ? r.MinimumSupportMonths : spec.GuaranteedSupportMonths,
        };
        RegisterFleetDelivery($"civil:{contract.Id}:{item.DesignId}", item, contract.Id, count,
            r.LifecycleModelVersion > 0 ? r.MinimumExpectedLifeHours : item.Aircraft.Performance.ExpectedLifeHours,
            new OperatingProfile(r.OperatingProfileId, r.UtilizationHoursPerMonth, r.AverageFlightHours), spec, serviceOffer);
    }

    private void RegisterGovernmentFleetDelivery(GovernmentDeliveryContract contract, int count, int cumulativeDelivered)
    {
        var saved = _savedAircraftDesigns.FirstOrDefault(d => d.CatalogItem.DesignId == contract.AircraftDesignId);
        if (saved is null) throw new InvalidOperationException("Delivered aircraft design is unavailable for fleet registration.");
        var r = contract.LifecycleRequirements;
        var spec = AircraftLifecycleSpecification.FromAircraft(saved.CatalogItem.Aircraft, _headquarters.CompanyQualityLevel);
        var metrics = contract.LifecycleAircraftMetrics;
        if (metrics.HasValue && r is not null && r.LifecycleModelVersion > 0)
            spec = spec with { Reliability = metrics.Value.Reliability, ExpectedLifeHours = metrics.Value.ExpectedLifeHours,
                ServiceIntervalHours = metrics.Value.ServiceIntervalHours, GuaranteedSupportMonths = metrics.Value.GuaranteedSupportMonths };
        RegisterFleetDelivery($"state:{contract.DeliveryId}:{cumulativeDelivered}", saved.CatalogItem,
            contract.GovernmentContractId, count,
            r is not null && r.LifecycleModelVersion > 0 ? r.MinimumExpectedLifeHours : spec.ExpectedLifeHours,
            new OperatingProfile(r?.OperatingProfileId ?? "military", r?.UtilizationHoursPerMonth ?? 100d, r?.AverageFlightHours ?? 2d),
            spec, contract.ServiceOffer);
    }

    private void RegisterFleetDelivery(string deliveryId, FactoryAircraftTypeCatalogItem item, string contractId, int count,
        double lifeHours, OperatingProfile profile, AircraftLifecycleSpecification? frozen = null, ServiceOfferSnapshot? offer = null)
    {
        var spec = frozen ?? AircraftLifecycleSpecification.FromAircraft(item.Aircraft, _headquarters.CompanyQualityLevel);
        if (!IsJetFamily(item.Aircraft.Engine.Definition.EngineType))
            profile = profile with { Id = profile.Id + ":piston" };
        var effectiveOffer = offer ?? (BuildCombinedMaintenanceOffer(
            _savedAircraftDesigns.FirstOrDefault(d => d.CatalogItem.DesignId == item.DesignId)
                ?? throw new InvalidOperationException("Delivered aircraft identity is unavailable for engine maintenance."), spec.Reliability)
            with { ServiceIntervalHours = spec.ServiceIntervalHours, GuaranteedSupportMonths = spec.GuaranteedSupportMonths });
        effectiveOffer = RepriceMaintenanceOffer(effectiveOffer);
        spec = spec with { GuaranteedSupportMonths = effectiveOffer.GuaranteedSupportMonths };
        _fleet.RegisterDelivery(deliveryId, item.DesignId, item.DisplayName, contractId, count, _currentDate,
            spec, profile, effectiveOffer, lifeHours,
            item.Aircraft.Airframe.Parameters.EngineCount);
        _forecastCacheKey = string.Empty;
    }

    private sealed class FactoryFleetServiceProvider : IFleetEngineServiceProvider, IFleetMaintenanceCostProvider
    {
        public double LastConsumptionCost { get; private set; }
        private readonly AircraftProductionFactory _factory;
        private readonly EngineManufacturingNetwork? _engineManufacturing;
        private readonly Action<string, int>? _registerReplacementSale;
        public FactoryFleetServiceProvider(AircraftProductionFactory factory) : this(factory, null) { }
        public FactoryFleetServiceProvider(AircraftProductionFactory factory, EngineManufacturingNetwork? engineManufacturing,
            Action<string, int>? registerReplacementSale = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _engineManufacturing = engineManufacturing;
            _registerReplacementSale = registerReplacementSale;
        }
        public int CompleteAirframeServices(string groupId, int requestedAircraft, int partsPerAircraft)
        {
            LastConsumptionCost = 0d;
            if (requestedAircraft < 0) throw new ArgumentOutOfRangeException(nameof(requestedAircraft));
            if (partsPerAircraft <= 0) throw new ArgumentOutOfRangeException(nameof(partsPerAircraft));
            var count = System.Math.Min(requestedAircraft, _factory.GetAirframeSparePartUnits(groupId) / partsPerAircraft);
            if (count <= 0 || !_factory.TryConsumeAirframeSpareParts(groupId, checked(count * partsPerAircraft), out var cost)) return 0;
            LastConsumptionCost = cost;
            return count;
        }
        public int CompleteEngineMaintenance(string engineDesignId, string groupId, int requestedAircraft,
            int engineCount, int partsPerEngine, bool replacement)
        {
            LastConsumptionCost = 0d;
            if (requestedAircraft < 0) throw new ArgumentOutOfRangeException(nameof(requestedAircraft));
            if (engineCount <= 0) throw new ArgumentOutOfRangeException(nameof(engineCount));
            if (!replacement && partsPerEngine <= 0) throw new ArgumentOutOfRangeException(nameof(partsPerEngine));
            if (_engineManufacturing is null) return 0;
            var unitsPerAircraft = replacement ? engineCount : checked(engineCount * partsPerEngine);
            // Whole motors are keyed by the frozen exact version, even when two versions share routine parts.
            var available = replacement ? _engineManufacturing.GetEngineUnits(engineDesignId) : _engineManufacturing.GetPartUnits(groupId);
            var count = System.Math.Min(requestedAircraft, available / unitsPerAircraft);
            if (count == 0) return 0;
            var units = checked(count * unitsPerAircraft);
            double cost;
            var consumed = replacement ? _engineManufacturing.TryConsumeEngineUnits(engineDesignId, units, out cost)
                : _engineManufacturing.TryConsumePartUnits(groupId, units, out cost);
            if (consumed) LastConsumptionCost = cost;
            if (consumed && replacement) _registerReplacementSale?.Invoke(engineDesignId, units);
            return consumed ? count : 0;
        }

    }

    private void ProcessFleetMonth(FactorySupportProductionReport support)
    {
        if (_productionFactory is null) return;
        _productionFactory.InitializeInventoryCostBasis(BuildProductionCatalog(includeOnlyResearched: false));
        _engineManufacturing.InitializeInventoryCostBasis(BuildEngineManufacturingCatalog());
        _lastFleetReport = _fleet.ProcessMonth(_currentDate, new FactoryFleetServiceProvider(_productionFactory, _engineManufacturing,
            _machineStatistics.RegisterEngineSale));
        _accountingSystem?.RegisterFleetService(_elapsedMonths, _currentDate, _lastFleetReport.Revenue,
            support.TotalExpenses, $"fleet-month:{_currentDate}");
        _accountingSystem?.UpdateInventorySnapshot(_elapsedMonths, _currentDate,
            CalculateInventoryWithSupport(_productionFactory.WarehouseUnitsByDesign,
                BuildProductionCatalog(includeOnlyResearched: false)));
        _maintenanceFinance.RegisterMonth(_elapsedMonths, _lastFleetReport);
        _reputation.ProcessMonth(_elapsedMonths, _lastFleetReport);
        _forecastCacheKey = string.Empty;
    }

    private IReadOnlyDictionary<string, InventoryValueEntry> CalculateInventoryWithSupport(
        IReadOnlyDictionary<string, int> aircraftStock, IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog)
    {
        var inventory = _factoryAccountingCalculator.CalculateInventoryValueByDesign(aircraftStock, catalog);
        var combined = (_productionFactory is null ? inventory
            : FleetInventoryValuation.AddSupport(inventory, _productionFactory.CreateSnapshot(), catalog))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var entry in _engineManufacturing.GetInventoryValueEntries(BuildEngineManufacturingCatalog()))
            combined.Add(entry.Key, entry.Value);
        return combined;
    }
}
