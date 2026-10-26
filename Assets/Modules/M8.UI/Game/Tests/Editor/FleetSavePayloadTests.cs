using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using WrightTycoon.Core.Company;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Game.Launch;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Balance;

namespace WrightTycoon.Game.Tests;

public sealed class FleetSavePayloadTests
{
    [Test]
    public void ActualPayloadRetainsMaintenanceSalesCostsWithoutRebooking()
    {
        var ledger = new WrightTycoon.Core.Accounting.MaintenanceFinancialLedger();
        var report = new FleetMonthReport(7, 100, 1, 1, 0, 0, 0, 0, 0, 0, 200)
        {
            ProductionCost = 80,
            MaintenanceByDesign = new Dictionary<string, FleetMaintenanceResult> { ["saved.type"] = new(200, 80) },
        };
        ledger.RegisterMonth(7, report);
        var payload = Activator.CreateInstance(PayloadType, nonPublic: true)!;
        PayloadType.GetProperty("MaintenanceFinancialSnapshot")!.SetValue(payload, ledger.CreateSnapshot());
        var restoredPayload = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(payload), PayloadType)!;
        var restored = new WrightTycoon.Core.Accounting.MaintenanceFinancialLedger();
        restored.RestoreSnapshot((WrightTycoon.Core.Accounting.MaintenanceFinancialSnapshot)PayloadType.GetProperty("MaintenanceFinancialSnapshot")!.GetValue(restoredPayload)!);
        restored.RegisterMonth(7, report);
        Assert.That(restored.Revenue, Is.EqualTo(200));
        Assert.That(restored.ProductionCost, Is.EqualTo(80));
        Assert.That(restored.GetTotals("saved.type").Profit, Is.EqualTo(120));
        Assert.That(restored.FirstTrackedMonth, Is.EqualTo(7));
        Assert.That(restored.LastProcessedMonth, Is.EqualTo(7));
    }

    [Test]
    [Category("Extended")]
    public void EngineLifecycleParametersSurviveActualPayloadAndOldSavesGetCompatibleDefaults()
    {
        var payload = Activator.CreateInstance(PayloadType, nonPublic: true)!;
        PayloadType.GetProperty("EngineParameters")!.SetValue(payload, new WrightTycoon.Core.Engines.EngineParameters
            { ServiceIntervalHours = 200, DurabilityLevel = 7 });
        var restored = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(payload), PayloadType)!;
        var parameters = (WrightTycoon.Core.Engines.EngineParameters)PayloadType.GetProperty("EngineParameters")!.GetValue(restored)!;
        Assert.That(parameters.ServiceIntervalHours, Is.EqualTo(200));
        Assert.That(parameters.DurabilityLevel, Is.EqualTo(7));
        var legacy = JsonConvert.DeserializeObject("{\"EngineParameters\":{}}", PayloadType)!;
        var old = (WrightTycoon.Core.Engines.EngineParameters)PayloadType.GetProperty("EngineParameters")!.GetValue(legacy)!;
        Assert.That(old.ServiceIntervalHours, Is.EqualTo(500));
        Assert.That(old.DurabilityLevel, Is.Zero);
    }

    [Test]
    public void PartsProgrammeStockAndReserveTargetsSurviveActualPayloadJsonRoundTrip()
    {
        var offer = Offer(500, 120);
        var fleet = new FleetRegistry(1234);
        fleet.RegisterDelivery("parts.delivery", "type.parts", "Parts", "contract.parts", 2, new SimulationDate(1960, 1),
            new AircraftLifecycleSpecification(AircraftCategory.LightMultiRolePiston, 1.12d, 0, 500, 20000, 120),
            new OperatingProfile("piston", 100, 2), offer, 18000);
        var payload = Activator.CreateInstance(PayloadType, nonPublic: true)!;
        PayloadType.GetProperty("FleetSnapshot")!.SetValue(payload, fleet.CreateSnapshot());
        PayloadType.GetProperty("AirframeSparePartStockTargets")!.SetValue(payload,
            new System.Collections.Generic.Dictionary<string, int> { ["type.parts"] = 37 });
        var factory = new WrightTycoon.Core.Factories.FactoryStateSnapshot();
        factory.SupportInventory.Add(new WrightTycoon.Core.Factories.FactorySupportInventorySnapshot
            { DesignId = "type.parts", AirframeSpareParts = 7 });
        PayloadType.GetProperty("FactoryStateSnapshot")!.SetValue(payload, factory);
        var restoredPayload = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(payload), PayloadType)!;
        var savedFleet = (FleetRegistrySnapshot)PayloadType.GetProperty("FleetSnapshot")!.GetValue(restoredPayload)!;
        Assert.That(savedFleet.Cohorts[0].Offer, Is.EqualTo(offer));
        // Previously delivered aircraft keep their agreed life, not the new universal design baseline.
        var restoredFleet = new FleetRegistry(1234);
        restoredFleet.RestoreSnapshot(savedFleet);
        Assert.That(restoredFleet.CreateSnapshot().Cohorts[0].Specification.ExpectedLifeHours, Is.EqualTo(20000d));
        Assert.That(restoredFleet.CreateSnapshot().Cohorts[0].Specification.Reliability, Is.EqualTo(1.12d));
        Assert.That(savedFleet.Cohorts[0].Offer.MaintenanceModelVersion, Is.EqualTo(2));
        Assert.That(savedFleet.Cohorts[0].Offer.SparePartsPerService, Is.EqualTo(3));
        var savedFactory = (WrightTycoon.Core.Factories.FactoryStateSnapshot)PayloadType.GetProperty("FactoryStateSnapshot")!.GetValue(restoredPayload)!;
        Assert.That(savedFactory.SupportInventory[0].AirframeSpareParts, Is.EqualTo(7));
        var targets = (System.Collections.Generic.Dictionary<string, int>)PayloadType.GetProperty("AirframeSparePartStockTargets")!.GetValue(restoredPayload)!;
        Assert.That(targets["type.parts"], Is.EqualTo(37));
        var json = JsonConvert.SerializeObject(payload);
        foreach (var removed in new[] { "ServiceKits", "SpareEngines", "RepairableEngines", "AircraftSpareParts", "AircraftSparePartStockTargets" })
            Assert.That(json, Does.Not.Contain(removed));
        Assert.That(JsonConvert.DeserializeObject<WrightTycoon.Core.Contracts.ContractRequirementsSnapshot>("{}")!.MaintenanceModelVersion, Is.EqualTo(2));
    }

    private static ServiceOfferSnapshot Offer(int interval, int months) => new(interval, months, 300d, 0d, 0d)
    {
        SparePartsPerService = 3, SparePartUnitPrice = 100d, AirframePartsGroupId = "type.parts",
        EngineCount = 1, EngineMaintenance = new EngineMaintenanceSpecification("engine.exact", "engine.parts", .8d, 500, 4000, 3, 10d, 1000d),
    };

    private static Type PayloadType => typeof(WrightTycoonLaunchRunner).GetNestedType("RunnerGameSavePayload", BindingFlags.NonPublic)!;

    [Test]
    public void EmptyPayloadHasNoInventedFleetOrGuarantees()
    {
        var payload = JsonConvert.DeserializeObject("{}", PayloadType)!;
        Assert.That(PayloadType.GetProperty("FleetSnapshot")!.GetValue(payload), Is.Null);
        Assert.That(PayloadType.GetProperty("ReputationSnapshot")!.GetValue(payload), Is.Null);
        Assert.That(PayloadType.GetProperty("TypeConstructionDurabilityLevel")!.GetValue(payload), Is.EqualTo(0));
        Assert.That(PayloadType.GetProperty("TypeServiceIntervalHours")!.GetValue(payload), Is.EqualTo(1000));
        Assert.That(PayloadType.GetProperty("TypeGuaranteedSupportMonths")!.GetValue(payload), Is.EqualTo(120));
        Assert.That(PayloadType.GetProperty("EngineManufacturingSnapshot")!.GetValue(payload), Is.Null);
        Assert.That(PayloadType.GetProperty("EnginePartsGroups")!.GetValue(payload), Is.Null);
        Assert.That(PayloadType.GetProperty("AirframePartsGroups")!.GetValue(payload), Is.Null);
        Assert.That(PayloadType.GetProperty("SeparateEngineProduction")!.GetValue(payload), Is.False);
        Assert.That(PayloadType.GetProperty("AirframeSparePartStockTargets")!.GetValue(payload), Is.Empty);
        Assert.That(JsonConvert.DeserializeObject<FactoryStateSnapshot>("{}")!.UsesExternalEngineSupply, Is.False);
        var legacyCohort = JsonConvert.DeserializeObject<FleetCohortSnapshot>("{}")!;
        Assert.That(legacyCohort.EngineHoursSinceReplacement, Is.Zero);
        Assert.That(legacyCohort.CompletedEngineServiceCycles, Is.Zero);
        var legacyOffer = JsonConvert.DeserializeObject<ServiceOfferSnapshot>("{}")!;
        Assert.That(legacyOffer.EngineMaintenance, Is.Null);
        Assert.That(legacyOffer.MaintenanceModelVersion, Is.EqualTo(2));
    }

    [Test]
    public void ActualPayload_RoundTripsMotorPlantGroupsPhysicalStocksAndFrozenModelTwoEngineAges()
    {
        var engineTerms = new EngineMaintenanceSpecification("engine.variant", "engine.anchor", .87d, 500, 4_000, 2, 13d, 5_000d);
        var offer = new ServiceOfferSnapshot(500, 120, 300d, 0d, 0d)
        {
            MaintenanceModelVersion = 2, SparePartsPerService = 3, SparePartUnitPrice = 100d,
            AirframePartsGroupId = "airframe.anchor", EngineMaintenance = engineTerms, EngineCount = 2,
        };
        var sourceFleet = new FleetRegistry(173);
        sourceFleet.RegisterDelivery("engine.saved.delivery", "airframe.variant", "Saved engines", "engine.saved.contract", 1,
            new SimulationDate(1960, 1), new AircraftLifecycleSpecification(AircraftCategory.LightMultiRolePiston, .8d, 2, 500, 20_000, 120),
            new OperatingProfile("piston", 100, 2), offer, 18_000, 2);
        var fleetSnapshot = sourceFleet.CreateSnapshot();
        fleetSnapshot = fleetSnapshot with { Cohorts = fleetSnapshot.Cohorts.Select(c => c with
        { FlightHours = 1_250d, CompletedServiceCycles = 2, EngineHoursSinceReplacement = 750d, CompletedEngineServiceCycles = 1 }).ToArray() };
        var engineGroups = new SparePartsGroupRegistry();
        engineGroups.Resolve("engine.anchor", "engine.family", (_, _) => 0d);
        engineGroups.Resolve("engine.variant", "engine.family", (_, _) => 5d);
        var airframeGroups = new SparePartsGroupRegistry();
        airframeGroups.Resolve("airframe.anchor", "airframe.family", (_, _) => 0d);
        airframeGroups.Resolve("airframe.variant", "airframe.family", (_, _) => 8d);
        var motorSnapshot = new EngineManufacturingSnapshot { NextSiteSequence = 2, LastProcessedMonth = 21 };
        motorSnapshot.Sites.Add(new EngineManufacturingSite
        { SiteId = "engine-plant.001", EngineDesignId = "engine.variant", BuildingSize = FactoryBuildingSize.Medium,
            AutomationTier = FactoryAutomationTier.MechanizedCells, MonthlyEngineTarget = 9, PartReserve = 17, EquipmentCost = 80_000,
            PartsAllocationPercent = 73 });
        motorSnapshot.EngineStock["engine.variant"] = 6;
        motorSnapshot.PartStock["engine.anchor"] = 12;
        motorSnapshot.WorkOrders.Add(new EngineManufacturingWorkSnapshot
        { EngineDesignId = "engine.variant", PartsGroupId = "engine.anchor", RemainingUnits = 3, Priority = 1,
            SiteId = "engine-plant.001", CompletedManDays = 25d, UnitManDays = 78d, UnitMaterialCost = 1_000d });
        var factorySnapshot = new FactoryStateSnapshot { UsesExternalEngineSupply = true };
        factorySnapshot.AircraftStockTargets["airframe.anchor"] = 27;
        factorySnapshot.AircraftStockTargets["zero-limit"] = 0;
        factorySnapshot.Factories.Add(new FactorySiteSnapshot { FactoryId = "factory.001", ConstructionMonthsRemaining = 1,
            CapacitySchemaVersion = 1, PartsAllocationPercent = 35 });
        factorySnapshot.SupportInventory.Add(new FactorySupportInventorySnapshot
        { DesignId = "airframe.anchor", AirframeSpareParts = 7 });
        var payload = Activator.CreateInstance(PayloadType, nonPublic: true)!;
        void Set(string name, object value) => PayloadType.GetProperty(name)!.SetValue(payload, value);
        Set("EngineManufacturingSnapshot", motorSnapshot);
        Set("EnginePartsGroups", engineGroups.CreateSnapshot()); Set("AirframePartsGroups", airframeGroups.CreateSnapshot());
        Set("SeparateEngineProduction", true); Set("FactoryStateSnapshot", factorySnapshot); Set("FleetSnapshot", fleetSnapshot);
        Set("AirframeSparePartStockTargets", new Dictionary<string, int> { ["airframe.anchor"] = int.MaxValue });
        var restoredPayload = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(payload), PayloadType)!;
        object Get(string name) => PayloadType.GetProperty(name)!.GetValue(restoredPayload)!;
        Assert.That(Get("SeparateEngineProduction"), Is.True);
        Assert.That(((Dictionary<string, int>)Get("AirframeSparePartStockTargets"))["airframe.anchor"], Is.EqualTo(int.MaxValue));
        var restoredMotors = new EngineManufacturingNetwork(); restoredMotors.RestoreSnapshot((EngineManufacturingSnapshot)Get("EngineManufacturingSnapshot"));
        Assert.That(restoredMotors.GetEngineUnits("engine.variant"), Is.EqualTo(6));
        Assert.That(restoredMotors.GetPartUnits("engine.anchor"), Is.EqualTo(12));
        Assert.That(restoredMotors.Sites[0].MonthlyEngineTarget, Is.EqualTo(9));
        Assert.That(restoredMotors.Sites[0].PartReserve, Is.EqualTo(17));
        Assert.That(restoredMotors.Sites[0].PartsAllocationPercent, Is.EqualTo(73));
        Assert.That(restoredMotors.Sites[0].AutomationTier, Is.EqualTo(FactoryAutomationTier.MechanizedCells));
        var savedWork = restoredMotors.CreateSnapshot().WorkOrders.Single();
        Assert.That(savedWork.CompletedManDays, Is.EqualTo(25d)); Assert.That(savedWork.UnitManDays, Is.EqualTo(78d));
        Assert.That(restoredMotors.CreateSnapshot().LastProcessedMonth, Is.EqualTo(21));
        var restoredEngineGroups = new SparePartsGroupRegistry(); restoredEngineGroups.RestoreSnapshot((SparePartsGroupRegistrySnapshot)Get("EnginePartsGroups"));
        var restoredAirframeGroups = new SparePartsGroupRegistry(); restoredAirframeGroups.RestoreSnapshot((SparePartsGroupRegistrySnapshot)Get("AirframePartsGroups"));
        Assert.That(restoredEngineGroups.GetGroupId("engine.variant"), Is.EqualTo("engine.anchor"));
        Assert.That(restoredAirframeGroups.GetGroupId("airframe.variant"), Is.EqualTo("airframe.anchor"));
        var savedFactory = (FactoryStateSnapshot)Get("FactoryStateSnapshot");
        var restoredAircraft = new AircraftProductionFactory(); restoredAircraft.RestoreSnapshot(savedFactory);
        Assert.That(restoredAircraft.Factories[0].PartsAllocationPercent, Is.EqualTo(35));
        Assert.That(restoredAircraft.GetAircraftStockTarget("airframe.anchor"), Is.EqualTo(27));
        Assert.That(restoredAircraft.GetAircraftStockTarget("zero-limit"), Is.Zero);
        Assert.That(restoredAircraft.GetAircraftStockTarget("legacy-unlimited"), Is.Null);
        Assert.That(JsonConvert.DeserializeObject<FactoryStateSnapshot>("{}")!.AircraftStockTargets, Is.Empty);
        Assert.That(JsonConvert.DeserializeObject<FactorySiteSnapshot>("{}")!.PartsAllocationPercent, Is.EqualTo(20));
        Assert.That(JsonConvert.DeserializeObject<EngineManufacturingSite>("{}")!.PartsAllocationPercent, Is.EqualTo(20));
        Assert.That(savedFactory.UsesExternalEngineSupply, Is.True);
        Assert.That(savedFactory.SupportInventory[0].AirframeSpareParts, Is.EqualTo(7));
        var restoredFleet = new FleetRegistry(1); restoredFleet.RestoreSnapshot((FleetRegistrySnapshot)Get("FleetSnapshot"));
        var cohort = restoredFleet.Cohorts.Single();
        Assert.That(cohort.Offer, Is.EqualTo(offer)); Assert.That(cohort.Offer.EngineMaintenance, Is.EqualTo(engineTerms));
        Assert.That(cohort.FlightHours, Is.EqualTo(1_250d)); Assert.That(cohort.EngineHoursSinceReplacement, Is.EqualTo(750d));
        Assert.That(cohort.CompletedEngineServiceCycles, Is.EqualTo(1)); Assert.That(cohort.CompletedServiceCycles, Is.EqualTo(2));
        Assert.That(restoredFleet.CreateSnapshot().AccidentRandomState, Is.EqualTo(fleetSnapshot.AccidentRandomState));
    }

    [Test]
    [Category("Extended")]
    public void GovernmentServiceTariffSnapshotRoundTripsAndLegacyDefaultsRemainUnpriced()
    {
        var snapshot = new WrightTycoon.Core.GovernmentContracts.GovernmentContractAircraftMetricsSnapshot
        { ServicePricePerEvent = 9876d };
        var json = JsonConvert.SerializeObject(snapshot);
        var restored = JsonConvert.DeserializeObject<WrightTycoon.Core.GovernmentContracts.GovernmentContractAircraftMetricsSnapshot>(json)!;
        Assert.That(restored.ServicePricePerEvent, Is.EqualTo(9876d));
        var legacy = JsonConvert.DeserializeObject<WrightTycoon.Core.GovernmentContracts.GovernmentContractAircraftMetricsSnapshot>("{}")!;
        Assert.That(legacy.ServicePricePerEvent, Is.Null);
    }

    [Test]
    public void ActualPayloadRoundTripRetainsFleetIdentityRandomAndFrozenTerms()
    {
        var fleet = new FleetRegistry(1234);
        fleet.RegisterDelivery("delivery.saved", "type.saved", "Saved", "contract.saved", 12, new SimulationDate(1960, 1),
            new AircraftLifecycleSpecification(AircraftCategory.LightMultiRolePiston, .8, 3, 500, 20000, 180),
            new OperatingProfile("piston", 100, 2), Offer(500, 180), 18000);
        var reputation = new CompanyReputation();
        reputation.ProcessMonth(10, new FleetMonthReport(10, 1000, 10, 10, 0, 0, 0, 0, 0, 0, 0) { ActiveAircraftAtStart = 10, WarrantyAircraft = 10, WarrantyServicedAircraft = 10 });
        var payload = Activator.CreateInstance(PayloadType, nonPublic: true)!;
        PayloadType.GetProperty("FleetSnapshot")!.SetValue(payload, fleet.CreateSnapshot());
        PayloadType.GetProperty("ReputationSnapshot")!.SetValue(payload, reputation.CreateSnapshot());
        var restoredPayload = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(payload), PayloadType)!;
        var restored = new FleetRegistry(999);
        restored.RestoreSnapshot((FleetRegistrySnapshot)PayloadType.GetProperty("FleetSnapshot")!.GetValue(restoredPayload)!);
        Assert.That(restored.Cohorts, Is.EqualTo(fleet.Cohorts));
        Assert.That(restored.CreateSnapshot().AccidentRandomState, Is.EqualTo(fleet.CreateSnapshot().AccidentRandomState));
        Assert.That(restored.RegisterDelivery("delivery.saved", "type.saved", "Saved", "contract.saved", 12,
            new SimulationDate(1960, 1), fleet.Cohorts[0].Specification, fleet.Cohorts[0].Profile,
            fleet.Cohorts[0].Offer, 18000), Is.False);
        var savedReputation = (CompanyReputationSnapshot)PayloadType.GetProperty("ReputationSnapshot")!.GetValue(restoredPayload)!;
        Assert.That(savedReputation.Score, Is.EqualTo(reputation.Score));
        Assert.That(savedReputation.History, Is.EqualTo(reputation.History));
    }
}
