using System.Collections.Generic;
using NUnit.Framework;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Factories;

[TestFixture]
public sealed class FactoryLifecycleProductionTests
{
    private static IEnumerable<TestCaseData> BuildingAndAutomationCases()
    {
        foreach (FactoryBuildingSize size in System.Enum.GetValues(typeof(FactoryBuildingSize)))
        foreach (FactoryAutomationTier tier in System.Enum.GetValues(typeof(FactoryAutomationTier)))
            yield return new TestCaseData(size, tier);
    }

    [TestCaseSource(nameof(BuildingAndAutomationCases))]
    [Category("Extended")]
    public void AllAircraftSizesAndAutomationTiersUseReducedCapacityWithoutChangingUpkeep(
        FactoryBuildingSize size, FactoryAutomationTier tier)
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("capacity", 100d, 1, 20d);
        var factory = Factory(item, tier);
        var snapshot = factory.CreateSnapshot();
        snapshot.Factories[0].BuildingSize = (int)size;
        factory.RestoreSnapshot(snapshot);
        var site = factory.Factories[0];
        factory.TrySetMonthlyProductionTargetUnits(site.FactoryId, 1_000_000, out _, out _);
        var report = factory.SimulateMonth(new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item },
            0, SimulationDate.WrightTycoonStart);
        var capacity = FactoryBalance.GetBuildingCapacityManDays(size);
        var unitWork = 120d * FactoryBalance.GetProductionManDayMultiplier(tier);
        Assert.That(report.TotalUsedManDays, Is.EqualTo(capacity).Within(.000001d));
        Assert.That(report.TotalAircraftProduced, Is.EqualTo((int)System.Math.Floor(capacity / unitWork)));
        Assert.That(report.TotalFactoryMaintenanceExpenses, Is.EqualTo(FactoryBalance.GetBuildingMonthlyMaintenance(size)));
    }

    [TestCase(FactoryProductionPriority.GuaranteedSupportFirst, 0)]
    [TestCase(FactoryProductionPriority.GuaranteedSupportFirst, 100)]
    [TestCase(FactoryProductionPriority.GuaranteedSupportFirst, 500)]
    [TestCase(FactoryProductionPriority.GuaranteedSupportFirst, 9000)]
    [TestCase(FactoryProductionPriority.AircraftFirst, 0)]
    [TestCase(FactoryProductionPriority.AircraftFirst, 100)]
    [TestCase(FactoryProductionPriority.AircraftFirst, 500)]
    [TestCase(FactoryProductionPriority.AircraftFirst, 9000)]
    [TestCase(FactoryProductionPriority.AllSupportFirst, 0)]
    [TestCase(FactoryProductionPriority.AllSupportFirst, 100)]
    [TestCase(FactoryProductionPriority.AllSupportFirst, 500)]
    [TestCase(FactoryProductionPriority.AllSupportFirst, 9000)]
    [Category("Extended")]
    public void UnusedPartsShare_IsLentOnlyToAircraftAfterOptionalDemandIsServed(FactoryProductionPriority priority, int parts)
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("spill", 100, 1, 20);
        var factory = Factory(item); var site = factory.Factories[0];
        factory.ProductionPriority = priority; factory.SetPartsAllocationPercent(site.FactoryId, 100);
        factory.TrySetMonthlyProductionTargetUnits(site.FactoryId, 1000, out _, out _);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var requests = new[] { new FactorySupportRequest(item.DesignId, parts, false) };
        factory.ProduceSupport(0, catalog, requests);
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(factory.CreateSnapshot());
        restored.ProduceSupport(0, catalog, requests);
        var aircraft = restored.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        var supportUsed = restored.GetMonthlyAllocatedSupportManDays(site.FactoryId);
        Assert.That(supportUsed, Is.EqualTo(System.Math.Min(parts, site.BuildingCapacityManDays)));
        Assert.That(aircraft.TotalUsedManDays, Is.EqualTo(site.BuildingCapacityManDays - supportUsed));
        Assert.That(restored.GetMonthlyAllocatedManDays(site.FactoryId), Is.EqualTo(site.BuildingCapacityManDays));
        Assert.That(restored.GetAirframeSparePartUnits(item.DesignId), Is.EqualTo(System.Math.Min(parts, site.BuildingCapacityManDays)));
        Assert.That(() => restored.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart), Throws.InvalidOperationException);
    }

    [Test]
    public void ExactDesignStockLimit_IsSharedAcrossSitesAndResumesAfterWithdrawal()
    {
        var first = AircraftProductionFactoryTests.CreateCatalogItem("limit", 100, 1, 20);
        var other = AircraftProductionFactoryTests.CreateCatalogItem("other", 100, 1, 20);
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork(3);
        for (var index = 0; index < 3; index++)
        {
            var item = index == 2 ? other : first; var site = factory.Factories[index];
            factory.TryPurchaseAndInstallEquipment(site.FactoryId, item.DesignId, FactoryAutomationTier.HandBuilt,
                120, double.MaxValue, out _, out _);
            factory.TrySetMonthlyProductionTargetUnits(site.FactoryId, 10, out _, out _);
        }
        for (var month = 0; month < FactoryBalance.GetInitialInstallationMonths(FactoryAutomationTier.HandBuilt); month++) factory.AdvanceMonth();
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [first.DesignId] = first, [other.DesignId] = other };
        Assert.That(factory.GetAircraftStockTarget(first.DesignId), Is.Null);
        factory.SetAircraftStockTarget(first.DesignId, 15);
        factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(factory.GetWarehouseUnits(first.DesignId), Is.EqualTo(15));
        Assert.That(factory.GetWarehouseUnits(other.DesignId), Is.EqualTo(10));
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(factory.CreateSnapshot());
        Assert.That(restored.GetAircraftStockTarget(first.DesignId), Is.EqualTo(15));
        restored.SimulateMonth(catalog, 1, SimulationDate.WrightTycoonStart.NextMonth());
        Assert.That(restored.GetWarehouseUnits(first.DesignId), Is.EqualTo(15));
        restored.TryWithdrawFromWarehouse(first.DesignId, 3, out _);
        restored.SimulateMonth(catalog, 2, SimulationDate.WrightTycoonStart.NextMonth().NextMonth());
        Assert.That(restored.GetWarehouseUnits(first.DesignId), Is.EqualTo(15));
        restored.SetAircraftStockTarget(first.DesignId, 0);
        restored.SimulateMonth(catalog, 3, SimulationDate.WrightTycoonStart);
        Assert.That(restored.GetWarehouseUnits(first.DesignId), Is.EqualTo(15));
        restored.ClearAircraftStockTarget(first.DesignId);
        restored.SimulateMonth(catalog, 4, SimulationDate.WrightTycoonStart);
        Assert.That(restored.GetWarehouseUnits(first.DesignId), Is.EqualTo(35));
        Assert.That(() => restored.SetAircraftStockTarget(first.DesignId, -1), Throws.TypeOf<System.ArgumentOutOfRangeException>());
    }

    [Test]
    [Category("Extended")]
    public void ZeroStockTarget_PreservesPaidWorkAndLegacyAndMaximumTargetsRemainSafe()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("paid", 9000, 1, 1000);
        var factory = Factory(item); var site = factory.Factories[0];
        factory.SetWorkforceEmployeeLimit(site.FactoryId, 400); // Stable paid-WIP fixture, not a physical capacity assertion.
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        factory.TrySetMonthlyProductionTargetUnits(site.FactoryId, 1, out _, out _);
        factory.SetAircraftStockTarget(item.DesignId, int.MaxValue);
        factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        factory.SetAircraftStockTarget(item.DesignId, 0);
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(factory.CreateSnapshot());
        Assert.That(restored.SimulateMonth(catalog, 1, SimulationDate.WrightTycoonStart).TotalUsedManDays, Is.Zero);
        Assert.That(restored.Lines[0].CarryOverManDays, Is.EqualTo(8400));
        restored.SetAircraftStockTarget(item.DesignId, 1);
        Assert.That(restored.SimulateMonth(catalog, 2, SimulationDate.WrightTycoonStart).TotalUsedManDays, Is.EqualTo(1600));
        Assert.That(restored.GetWarehouseUnits(item.DesignId), Is.EqualTo(1));
        var legacy = restored.CreateSnapshot(); legacy.AircraftStockTargets = null!;
        restored.RestoreSnapshot(legacy);
        Assert.That(restored.GetAircraftStockTarget(item.DesignId), Is.Null);
    }

    private static AircraftProductionFactory Factory(FactoryAircraftTypeCatalogItem item, FactoryAutomationTier tier = FactoryAutomationTier.HandBuilt)
    {
        var factory = AircraftProductionFactoryTests.BuildOperationalFactoryNetwork();
        factory.TryPurchaseAndInstallEquipment(factory.Factories[0].FactoryId, item.DesignId, tier,
            item.Aircraft.Performance.TotalProductionManDays, double.MaxValue, out _, out _);
        for (var month = 0; month < FactoryBalance.GetInitialInstallationMonths(tier); month++) factory.AdvanceMonth();
        return factory;
    }

    [Test]
    public void CombinedAirframeAndEngineWork_CannotExceedPhysicalBuildingBudget()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("large", 10_000d, 2, 10_000d);
        var factory = Factory(item);
        factory.TrySetMonthlyProductionTargetUnits(factory.Factories[0].FactoryId, 100, out _, out _);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var first = factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(first.TotalAircraftProduced, Is.Zero);
        Assert.That(first.TotalUsedManDays, Is.EqualTo(12_600d));
        Assert.That(factory.Lines[0].CarryOverManDays, Is.EqualTo(12_600d));
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(factory.CreateSnapshot());
        var second = restored.SimulateMonth(catalog, 1, SimulationDate.WrightTycoonStart.NextMonth());
        Assert.That(second.TotalAircraftProduced, Is.EqualTo(1));
        Assert.That(second.TotalUsedManDays, Is.EqualTo(7_400d));
        Assert.That(restored.Lines[0].CarryOverManDays, Is.Zero);
    }

    [Test]
    public void GuaranteedService_ReservesSameMdBeforeNewAircraftAndCannotDuplicateOnReplan()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("support", 2_000d, 1, 100d);
        var factory = Factory(item);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        factory.TrySetMonthlyProductionTargetUnits(factory.Factories[0].FactoryId, 1, out _, out _);
        var requests = new[] { new FactorySupportRequest(item.DesignId, 10) };
        var support = factory.ProduceSupport(0, catalog, requests);
        Assert.That(support.UsedManDays, Is.EqualTo(200d));
        factory.ProduceSupport(0, catalog, requests);
        Assert.That(factory.GetAirframeSparePartUnits(item.DesignId), Is.EqualTo(10));
        var aircraft = factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(aircraft.TotalAircraftProduced, Is.EqualTo(1));
        Assert.That(aircraft.TotalUsedManDays + support.UsedManDays, Is.EqualTo(2_300d));
        Assert.That(factory.TryConsumeAirframeSpareParts(item.DesignId, 10), Is.True);
        Assert.That(factory.TryConsumeAirframeSpareParts(item.DesignId, 1), Is.False);
    }

    [Test]
    public void StaffLimitAndAutomation_ApplyOnceAndUnusedMdAreNeverBanked()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("staff", 100d, 1, 110d);
        var factory = Factory(item, FactoryAutomationTier.SophisticatedLine);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        factory.SetWorkforceEmployeeLimit(factory.Factories[0].FactoryId, 5);
        factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        factory.TrySetMonthlyProductionTargetUnits(factory.Factories[0].FactoryId, 100, out _, out _);
        var report = factory.SimulateMonth(catalog, 1, SimulationDate.WrightTycoonStart.NextMonth());
        Assert.That(report.TotalAircraftProduced, Is.EqualTo(1));
        Assert.That(report.TotalUsedManDays, Is.EqualTo(105d));
        Assert.That(factory.Lines[0].CarryOverManDays, Is.Zero);
    }

    [Test]
    [Category("Extended")]
    public void LegacyMigration_ReducesOldCapacityExactlyOnceWithoutDiscardingPaidWork()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("legacy", 500d, 1, 100d);
        var factory = Factory(item);
        var snapshot = factory.CreateSnapshot();
        snapshot.Factories[0].CapacitySchemaVersion = 0;
        snapshot.Factories[0].Line!.MonthlyProductionTargetUnits = 100;
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(snapshot);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var report = restored.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(report.TotalAircraftProduced, Is.EqualTo(93));
        Assert.That(restored.Factories[0].LegacyCapacityMd, Is.EqualTo(56_250d));
        var roundTrip = new AircraftProductionFactory(); roundTrip.RestoreSnapshot(restored.CreateSnapshot());
        Assert.That(roundTrip.Factories[0].LegacyCapacityMd, Is.EqualTo(56_250d));
        Assert.That(roundTrip.Factories[0].NeedsLegacyCapacityMigration, Is.False);
    }

    [TestCase(1)]
    [TestCase(25)]
    [TestCase(100)]
    public void AllocationPercent_PendingPartsRetainTheirStrictReservedShare(int percent)
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("split", 100, 1, 20);
        var factory = Factory(item); var site = factory.Factories[0];
        factory.SetPartsAllocationPercent(site.FactoryId, percent);
        factory.TrySetMonthlyProductionTargetUnits(site.FactoryId, 1000, out _, out _);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var requests = new[] { new FactorySupportRequest(item.DesignId, 100_000) };
        var support = factory.ProduceSupport(0, catalog, requests);
        var aircraft = factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        var capacity = site.AvailableManDays;
        Assert.That(support.UsedManDays, Is.EqualTo(capacity * percent / 100d).Within(.000001));
        Assert.That(aircraft.TotalUsedManDays, Is.EqualTo(capacity * (100 - percent) / 100d).Within(.000001));
        Assert.That(factory.GetMonthlyAllocatedSupportManDays(site.FactoryId), Is.EqualTo(support.UsedManDays));
        factory.ProduceSupport(0, catalog, requests);
        Assert.That(factory.GetMonthlyAllocatedManDays(site.FactoryId), Is.EqualTo(capacity).Within(.000001));
        Assert.That(factory.GetMaximumMonthlyOutput(site.FactoryId), Is.EqualTo(105));
    }

    [Test]
    public void AirframeParts_BorrowAllIdleAircraftCapacity()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("parts", 100, 1, 20);
        var factory = Factory(item); var site = factory.Factories[0]; factory.SetPartsAllocationPercent(site.FactoryId, 25);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var request = new FactorySupportRequest(item.DesignId, 100_000);
        var support = factory.ProduceSupport(0, catalog, new[] { request });
        var aircraft = factory.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(support.UsedManDays, Is.EqualTo(site.AvailableManDays).Within(.000001));
        Assert.That(aircraft.TotalUsedManDays, Is.Zero);
        Assert.That(factory.GetMonthlyAllocatedManDays(site.FactoryId), Is.EqualTo(site.AvailableManDays).Within(.000001));
    }

    [Test]
    public void MidMonthSnapshotAndSliderChanges_CannotCreateExtraCapacityOrReinterpretPaidWork()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("frozen", 100, 1, 20);
        var factory = Factory(item); var site = factory.Factories[0];
        factory.SetPartsAllocationPercent(site.FactoryId, 25); factory.SetWorkforceEmployeeLimit(site.FactoryId, 200);
        factory.TrySetMonthlyProductionTargetUnits(site.FactoryId, 100, out _, out _);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        var requests = new[] { new FactorySupportRequest(item.DesignId, 100_000) };
        factory.ProduceSupport(0, catalog, requests);
        factory.SetPartsAllocationPercent(site.FactoryId, 100);
        var saved = factory.CreateSnapshot();
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(saved);
        Assert.That(restored.Factories[0].PartsAllocationPercent, Is.EqualTo(100));
        Assert.That(restored.GetMonthlyPartsCapacityManDays(site.FactoryId), Is.EqualTo(1050));
        Assert.That(restored.GetMonthlyAllocatedSupportManDays(site.FactoryId), Is.EqualTo(1050));
        restored.SetWorkforceEmployeeLimit(site.FactoryId, 400);
        var retry = restored.ProduceSupport(0, catalog, requests);
        Assert.That(retry.UsedManDays, Is.Zero);
        var aircraft = restored.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        Assert.That(aircraft.TotalUsedManDays, Is.EqualTo(3150));
        Assert.That(restored.GetMonthlyAllocatedManDays(site.FactoryId), Is.EqualTo(4200));
        var next = restored.ProduceSupport(1, catalog, requests);
        Assert.That(next.UsedManDays, Is.EqualTo(8400));
        Assert.That(restored.GetMonthlyProductionCapacityManDays(site.FactoryId), Is.Zero);
    }

    [TestCase(0, 60)]
    [TestCase(0, 1000)]
    [TestCase(1, 60)]
    [TestCase(1, 1000)]
    [TestCase(0, 0)]
    [Category("Extended")]
    public void AircraftStockTargets_ReleaseCapacityToPartsAndStopAtActualDemand(int aircraftTarget, int partsTarget)
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("reuse", 100, 1, 20);
        var factory = Factory(item); var site = factory.Factories[0];
        factory.SetWorkforceEmployeeLimit(site.FactoryId, 10);
        factory.SetPartsAllocationPercent(site.FactoryId, 25);
        factory.AutomaticStockReplenishment = true;
        factory.SetAircraftStockTarget(item.DesignId, aircraftTarget);
        var catalog = new Dictionary<string, FactoryAircraftTypeCatalogItem> { [item.DesignId] = item };
        factory.ProduceSupport(0, catalog, new[] { new FactorySupportRequest(item.DesignId, partsTarget) });
        var restored = factory;
        var report = restored.SimulateMonth(catalog, 0, SimulationDate.WrightTycoonStart);
        var aircraftWork = aircraftTarget * 120d;
        var partsWork = System.Math.Min(partsTarget, 210d - aircraftWork);
        Assert.That(report.TotalAircraftProduced, Is.EqualTo(aircraftTarget));
        Assert.That(report.TotalUsedManDays, Is.EqualTo(aircraftWork));
        Assert.That(report.SupportProductionManDays, Is.EqualTo(partsWork));
        Assert.That(restored.GetAirframeSparePartUnits(item.DesignId), Is.EqualTo((int)partsWork));
        Assert.That(restored.GetMonthlyAllocatedManDays(site.FactoryId), Is.EqualTo(aircraftWork + partsWork));
        Assert.That(restored.LastSupportReport.LaborExpenses, Is.EqualTo(partsWork * ProductionEconomyBalance.LaborCostPerManDay));
        Assert.That(restored.GetMonthlyAllocatedManDays(site.FactoryId), Is.LessThanOrEqualTo(210));
    }

    [Test]
    [Category("Extended")]
    public void AllocationSetterClampsAndLegacyMissingDataDefaultsToTwentyPercent()
    {
        var item = AircraftProductionFactoryTests.CreateCatalogItem("defaults", 100, 1, 20);
        var factory = Factory(item); var site = factory.Factories[0];
        Assert.That(site.PartsAllocationPercent, Is.EqualTo(20));
        factory.SetPartsAllocationPercent(site.FactoryId, -20); Assert.That(site.PartsAllocationPercent, Is.EqualTo(1));
        factory.SetPartsAllocationPercent(site.FactoryId, 120); Assert.That(site.PartsAllocationPercent, Is.EqualTo(100));
        var saved = factory.CreateSnapshot(); saved.Factories[0].PartsAllocationPercent = 0;
        var restored = new AircraftProductionFactory(); restored.RestoreSnapshot(saved);
        Assert.That(restored.Factories[0].PartsAllocationPercent, Is.EqualTo(20));
    }
}
