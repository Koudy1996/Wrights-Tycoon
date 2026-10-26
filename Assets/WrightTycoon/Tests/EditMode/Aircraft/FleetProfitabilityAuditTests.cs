using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Core.Tests.Factories;

namespace WrightTycoon.Core.Tests.Aircraft;

/// <summary>Diagnostic audit, NOT an assertion that economic balance meets the target.
/// Source-linked factory/fleet/accounting run; normalized unchanged aircraft technology and neutral reputation.
/// HEAD sale markups and old factory staffing/output rules are explicitly reconstructed below.</summary>
[Category("Extended")]
public sealed class FleetProfitabilityAuditTests
{
    private static readonly SimulationDate Start = new(2000, 1);
    private const int CohortUnits = 20;

    private sealed class Supply : IFleetEngineServiceProvider
    {
        private readonly AircraftProductionFactory factory;
        private readonly EngineManufacturingNetwork motors;
        public Supply(AircraftProductionFactory factory, EngineManufacturingNetwork motors)
        { this.factory = factory; this.motors = motors; }
        public int CompleteAirframeServices(string groupId, int requestedServices, int partsPerService)
        {
            var count = System.Math.Min(requestedServices, factory.GetAirframeSparePartUnits(groupId) / partsPerService);
            return count > 0 && factory.TryConsumeAirframeSpareParts(groupId, checked(count * partsPerService)) ? count : 0;
        }
        public int CompleteEngineMaintenance(string engineDesignId, string groupId, int requestedAircraft,
            int engineCount, int partsPerEngine, bool replacement)
        {
            var perAircraft = checked(engineCount * (replacement ? 1 : partsPerEngine));
            var stock = replacement ? motors.GetEngineUnits(engineDesignId) : motors.GetPartUnits(groupId);
            var count = System.Math.Min(requestedAircraft, stock / perAircraft);
            if (count == 0) return 0;
            return (replacement ? motors.TryConsumeEngineUnits(engineDesignId, checked(count * perAircraft))
                : motors.TryConsumePartUnits(groupId, checked(count * perAircraft))) ? count : 0;
        }
    }

    private static EngineManufacturingNetwork MotorFactory(FactoryAutomationTier tier)
    {
        var motors = new EngineManufacturingNetwork();
        motors.RestoreSnapshot(new EngineManufacturingSnapshot
        {
            Sites = new List<EngineManufacturingSite> { new()
            { SiteId = "audit-motor-plant", EngineDesignId = "audit-engine", AutomationTier = tier } },
        });
        return motors;
    }

    private static EngineManufacturingMonthReport ProduceMotorSupport(EngineManufacturingNetwork motors,
        FleetRegistry fleet, FactoryAircraftTypeCatalogItem item, int month, SimulationDate date) =>
        motors.ProcessMonth(month, new Dictionary<string, EngineManufacturingCatalogItem>
            { ["audit-engine"] = new("audit-engine", "audit-engine-parts", item.Aircraft.Engine) },
            fleet.GetEngineServiceDemand(date).Select(d => new EngineManufacturingDemand(d.EngineDesignId,
                d.PartsGroupId, d.ReplacementEngines, d.EngineParts, d.Guaranteed)));

    private static FactoryAircraftTypeCatalogItem Item(int interval = 1000)
    {
        // Controlled M3 report: $150k airframe + $70k engines, 100 +20 MD. No physics/design advantage.
        var item = AircraftProductionFactoryTests.CreateCatalogItem("audit", 100d, 2, 20d);
        var a = item.Aircraft;
        var type = new WrightTycoon.Core.Aircraft.AircraftTypeDefinition
        { Id = a.TypeDefinition.Id, FamilyId = a.TypeDefinition.FamilyId, DisplayName = "Audit", ServiceIntervalHours = interval, GuaranteedSupportMonths = 120 };
        return new FactoryAircraftTypeCatalogItem(item.DesignId, item.DisplayName,
            new WrightTycoon.Core.Aircraft.Aircraft(a.FamilyDefinition, type, a.Engine, a.Airframe, a.Performance, a.Development));
    }

    private static AircraftProductionFactory Factory(FactoryAircraftTypeCatalogItem item, FactoryAutomationTier tier, bool legacyCapacity)
    {
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork();
        var site = factory.Factories[0];
        Assert.That(factory.TryPurchaseAndInstallEquipment(site.FactoryId, item.DesignId, tier,
            item.Aircraft.Performance.TotalProductionManDays, double.MaxValue, out _, out _), Is.True);
        for (var m = 0; m < FactoryBalance.GetInitialInstallationMonths(tier); m++) factory.AdvanceMonth();
        if (legacyCapacity)
        {
            var snapshot = factory.CreateSnapshot();
            snapshot.Factories[0].CapacitySchemaVersion = 1;
            snapshot.Factories[0].LegacyCapacityMd = FactoryBalance.GetMaximumMonthlyOutput(tier)
                * item.Aircraft.Performance.TotalProductionManDays * FactoryBalance.GetProductionManDayMultiplier(tier);
            factory.RestoreSnapshot(snapshot);
        }
        return factory;
    }

    private static double SalePrice(FactoryAircraftTypeCatalogItem item) =>
        item.Aircraft.Performance.CompleteProductionCost * (1d + ContractBalance.SmallOrderContractMarkup)
            * ContractBalance.ContractPriceMarginImprovementMultiplier;

    private static void Register(FleetRegistry fleet, FactoryAircraftTypeCatalogItem item, int units, SimulationDate date,
        string delivery, double life) => fleet.RegisterDelivery(delivery, item.DesignId, "Audit", "contract." + delivery, units, date,
            new AircraftLifecycleSpecification(AircraftCategory.LightMultiRolePiston, .88, 0,
                item.Aircraft.TypeDefinition.ServiceIntervalHours, 30000, 120),
            new OperatingProfile("audit:piston", 100, 2), LifecycleCostCalculator.CreateEngineLifecycleServiceOffer(item.Aircraft, .88, "audit-engine", item.DesignId, "audit-engine-parts"), life, 2);

    [Test]
    public void MatchedFleetLifetimeAudit_ReportsProfitDriftWithoutClaimingNeutrality()
    {
        TestContext.WriteLine("AUDIT_LIFETIME|life|interval|tier|continueSupport|seed|oldContribution|newContribution|serviceRevenue|productionCost|motorOverheadCost|equipmentCost|months|services|groundedUnitMonths|newEngines|engineParts|within10pct");
        var cases = 0;
        var neutral = 0;
        foreach (var life in new[] { 4000d, 10000d, 20000d })
        foreach (var interval in LifecycleBalance.ServiceIntervalsHours)
        foreach (var tier in new[] { FactoryAutomationTier.HandBuilt, FactoryAutomationTier.SophisticatedLine })
        foreach (var continued in new[] { false, true })
        foreach (var seed in new ulong[] { 1, 17, 89 })
        {
            var item = Item(interval);
            var factory = Factory(item, tier, true);
            var motors = MotorFactory(tier);
            var fleet = new FleetRegistry(seed);
            Register(fleet, item, CohortUnits, Start, "batch", life);
            var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
            var accounting = new AccountingSystem(1_000_000_000);
            var revenue = 0d; var costs = 0d; var motorOverhead = 0d; var equipment = 0d;
            var services = 0; var grounded = 0; var newEngines = 0; var engineParts = 0; var months = 0;
            var horizon = (int)System.Math.Ceiling((life + 30000d) * .5d * 1.1d / 100) + 24;
            for (var m = 1; m <= horizon; m++)
            {
                var date = Start.AddMonths(m);
                if (!continued && m == 120) Assert.That(fleet.TryEndSupport(item.DesignId, date, false, out _), Is.True);
                var support = factory.ProduceSupport(m, catalog, fleet.GetServiceDemand(date)
                    .Select(d => new FactorySupportRequest(d.DesignId, d.AirframeSpareParts, d.Guaranteed)));
                // Close the aircraft-production phase even with zero targets: it runs voluntary support queues.
                var production = factory.SimulateMonth(catalog, m, date);
                var motorReport = ProduceMotorSupport(motors, fleet, item, m, date);
                var report = fleet.ProcessMonth(date, new Supply(factory, motors));
                accounting.RegisterFleetService(m, date, report.Revenue,
                    support.TotalExpenses + production.TotalActiveEquipmentExpenses + motorReport.TotalExpenses, "service." + m);
                revenue += report.Revenue; costs += support.MaterialExpenses + support.LaborExpenses + motorReport.MaterialExpenses + motorReport.LaborExpenses;
                equipment += support.EquipmentExpenses + production.TotalActiveEquipmentExpenses; motorOverhead += motorReport.MaintenanceExpenses + motorReport.EquipmentExpenses;
                services += report.CompletedServices; grounded += report.GroundedUnits;
                newEngines += motorReport.CompletedEngines; engineParts += motorReport.CompletedParts; months = m;
                Assert.That(support.UsedManDays, Is.LessThanOrEqualTo(factory.Factories[0].AvailableManDays + .000001));
                Assert.That(fleet.Cohorts.Sum(c => c.OriginalCount), Is.EqualTo(CohortUnits));
                Assert.That(fleet.Cohorts.All(c => c.OriginalCount == c.ActiveCount + c.AccidentLosses + c.RetiredCount), Is.True);
                if (fleet.ActiveUnits == 0) break;
            }
            Assert.That(accounting.Cash - 1_000_000_000, Is.EqualTo(revenue - costs - equipment - motorOverhead).Within(.01));
            var p = item.Aircraft.Performance;
            var mult = FactoryBalance.GetProductionManDayMultiplier(tier);
            var oldBuild = CohortUnits * p.TotalProductionCost + (System.Math.Ceiling(CohortUnits * p.AirframeProductionManDays * mult / 21)
                + System.Math.Ceiling(CohortUnits * p.EnginesProductionManDays * mult / 21)) * FactoryBalance.PlaneStaffMonthlyCostPerEmployee;
            var newBuild = CohortUnits * p.TotalProductionCost + System.Math.Ceiling(CohortUnits * p.TotalProductionManDays * mult / 21)
                * FactoryBalance.PlaneStaffMonthlyCostPerEmployee;
            var oldContribution = SalePrice(item) * CohortUnits - oldBuild;
            var newContribution = SalePrice(item) * CohortUnits - newBuild + revenue - costs - equipment - motorOverhead;
            var within = System.Math.Abs(newContribution / oldContribution - 1) <= .10;
            cases++; if (within) neutral++;
            TestContext.WriteLine(FormattableString.Invariant($"AUDIT_LIFETIME|{life}|{interval}|{tier}|{continued}|{seed}|{oldContribution:F2}|{newContribution:F2}|{revenue:F2}|{costs:F2}|{motorOverhead:F2}|{equipment:F2}|{months}|{services}|{grounded}|{newEngines}|{engineParts}|{within}"));
        }
        TestContext.WriteLine($"AUDIT_LIFETIME_SUMMARY|cases={cases}|within10pct={neutral}|outside10pct={cases-neutral}");
        Assert.That(cases, Is.EqualTo(180));
    }

    [Test]
    public void FactoryCalendarAudit_ReportsThroughputAndFullOperatingCashSeparately()
    {
        TestContext.WriteLine("AUDIT_CALENDAR|tier|legacyCapacity|service|oldSold|newSold|oldOperatingCash|newOperatingCash|cashAfterCapital|serviceRevenue|serviceCosts|groundedUnitMonths|aircraftMD|supportMD");
        foreach (FactoryAutomationTier tier in Enum.GetValues(typeof(FactoryAutomationTier)))
        foreach (var legacy in new[] { false, true })
        foreach (var servicing in new[] { false, true })
        {
            var item = Item();
            var factory = Factory(item, tier, legacy);
            var motors = MotorFactory(tier);
            var site = factory.Factories[0];
            var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
            var oldTarget = FactoryBalance.GetMaximumMonthlyOutput(tier);
            Assert.That(factory.TrySetMonthlyProductionTargetUnits(site.FactoryId,
                System.Math.Min(oldTarget, factory.GetMaximumMonthlyOutput(site.FactoryId)), out _, out _), Is.True);
            var accounting = new AccountingSystem(1_000_000_000);
            var calculator = new FactoryAccountingCalculator();
            var equipment = factory.Equipment[0];
            var capital = FactoryBalance.FactoryConstructionCost + equipment.PurchaseCost;
            accounting.RegisterCapitalExpense(0, Start, capital, "audit construction and equipment");
            var fleet = new FleetRegistry(89);
            var oldCash = 0d; var oldCarry = 0d; var sales = 0; var oldSales = 0;
            var serviceRevenue = 0d; var serviceCosts = 0d; var grounded = 0; var aircraftMd = 0d; var supportMd = 0d;
            for (var m = 0; m < 120; m++)
            {
                var date = Start.AddMonths(m);
                var support = factory.ProduceSupport(m, catalog, fleet.GetServiceDemand(date)
                    .Select(d => new FactorySupportRequest(d.DesignId, d.AirframeSpareParts, d.Guaranteed)));
                var produced = factory.SimulateMonth(catalog, m, date);
                var n = produced.TotalAircraftProduced;
                if (n > 0)
                {
                    Assert.That(factory.TryWithdrawFromWarehouse(item.DesignId, n, out _), Is.True);
                    accounting.RegisterContractIncome(m, date, SalePrice(item) * n, new Dictionary<string, InventoryValueEntry>());
                    if (servicing) Register(fleet, item, n, date, "batch." + m, 10000);
                }
                var motorReport = ProduceMotorSupport(motors, fleet, item, m, date);
                var report = fleet.ProcessMonth(date, new Supply(factory, motors));
                var monthServiceCost = support.TotalExpenses + motorReport.TotalExpenses;
                accounting.RegisterFleetService(m, date, report.Revenue, monthServiceCost, "service." + m);
                accounting.RegisterMonthlyFactoryCosts(m, date, calculator.CalculateMonthlyCosts(produced, catalog,
                    produced.TotalFactoryMaintenanceExpenses, produced.TotalActiveEquipmentExpenses), new Dictionary<string, InventoryValueEntry>());
                sales += n; serviceRevenue += report.Revenue; serviceCosts += monthServiceCost; grounded += report.GroundedUnits;
                aircraftMd += produced.TotalUsedManDays; supportMd += support.UsedManDays;
                Assert.That(produced.TotalUsedManDays + support.UsedManDays, Is.LessThanOrEqualTo(site.AvailableManDays + .000001));

                // HEAD production: piece cap; rounded airframe labor with free employee-MD carry; separate unconstrained engine labor.
                var p = item.Aircraft.Performance;
                var mult = FactoryBalance.GetProductionManDayMultiplier(tier);
                var airMd = p.AirframeProductionManDays * mult;
                var planeEmployees = System.Math.Ceiling(System.Math.Max(0, oldTarget * airMd - oldCarry) / 21);
                var available = oldCarry + planeEmployees * 21;
                var oldProduced = System.Math.Min(oldTarget, (int)System.Math.Floor((available + .000001) / airMd));
                oldCarry = available - oldProduced * airMd;
                var engineEmployees = System.Math.Ceiling(oldProduced * p.EnginesProductionManDays * mult / 21);
                var oldCosts = oldProduced * p.TotalProductionCost + (planeEmployees + engineEmployees) * FactoryBalance.PlaneStaffMonthlyCostPerEmployee
                    + FactoryBalance.FactoryMonthlyMaintenance + equipment.PurchaseCost * FactoryBalance.GetMonthlyActiveEquipmentCostShare(tier);
                oldCash += oldProduced * SalePrice(item) - oldCosts;
                oldSales += oldProduced;
                factory.AdvanceMonth();
            }
            var actualCash = accounting.Cash - 1_000_000_000;
            var monthlyHistory = accounting.GetHistory();
            Assert.That(actualCash, Is.EqualTo(monthlyHistory.Sum(s => s.Income - s.Costs.TotalCosts) - capital).Within(.1));
            TestContext.WriteLine(FormattableString.Invariant($"AUDIT_CALENDAR|{tier}|{legacy}|{servicing}|{oldSales}|{sales}|{oldCash:F2}|{actualCash+capital:F2}|{actualCash:F2}|{serviceRevenue:F2}|{serviceCosts:F2}|{grounded}|{aircraftMd:F2}|{supportMd:F2}"));
        }
    }

    [Test]
    public void PriceAudit_ReportsBothChannelsWithoutInventingAServiceDiscount()
    {
        var civil = new ContractPricingCalculator();
        ContractRequirements Civil(int version) => new() { RequiredCategory = AircraftCategory.RegionalJet, MinimumReliability = .8,
            LifecycleModelVersion = version, MinimumExpectedLifeHours = 10000, MinimumSupportMonths = 120,
            MaximumServiceIntervalHours = 1000, MaximumOperatingCostPerUnit = 1e12, ProcurementPreference = ProcurementPreference.WholeLifeCost };
        GovernmentContractRequirements State(int version) => new() { RequiredAircraftType = AircraftCategory.RegionalJet,
            MinimumMaximumSpeedMetersPerSecond = 100, MinimumRangeKilometers = 100, MinimumReliability = .8,
            MinimumCrewSize = 1, MaximumRequiredRunwayLengthMeters = 5000, LifecycleModelVersion = version,
            MinimumExpectedLifeHours = 10000, MinimumSupportMonths = 120, MaximumServiceIntervalHours = 1000,
            MaximumOperatingCostPerUnit = 1e12, ProcurementPreference = ProcurementPreference.WholeLifeCost };
        var state = new GovernmentContractPricingService();
        TestContext.WriteLine(FormattableString.Invariant($"AUDIT_PRICE|civil|{civil.CalculateQuote(Civil(0),20).UnitPrice:F2}|{civil.CalculateQuote(Civil(1),20).UnitPrice:F2}"));
        TestContext.WriteLine(FormattableString.Invariant($"AUDIT_PRICE|state|{state.CalculateQuote(State(0),20).UnitPrice:F2}|{state.CalculateQuote(State(1),20).UnitPrice:F2}|completionBonus={GovernmentContractBalance.DeliveryCompletionBonusShare}"));
    }
}
