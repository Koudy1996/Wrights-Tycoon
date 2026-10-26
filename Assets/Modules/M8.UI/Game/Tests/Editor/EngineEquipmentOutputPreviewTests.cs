using System.Reflection;
using NUnit.Framework;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

public sealed class EngineEquipmentOutputPreviewTests
{
    [Test]
    public void UnpurchasedEquipmentHasLiveOutputEstimateFromSelectedEngineAutomationAndWorkforce()
    {
        var plant = new EngineManufacturingSite { BuildingSize = FactoryBuildingSize.Medium, ConstructionMonthsRemaining = 18 };
        var engine = new EngineManufacturingCatalogItem("selected", "parts", 1000, 100);
        var method = typeof(WrightTycoonLaunchRunner).GetMethod("GetEnginePlantMonthlyCapacity", BindingFlags.Static | BindingFlags.NonPublic)!;
        double Estimate(EngineManufacturingCatalogItem item, FactoryAutomationTier tier) => (double)method.Invoke(null, new object[] { plant, item, tier })!;
        Assert.That(plant.AvailableManDays, Is.Zero);
        Assert.That(Estimate(engine, FactoryAutomationTier.HandBuilt), Is.EqualTo(504));
        Assert.That(Estimate(engine, FactoryAutomationTier.MechanizedCells),
            Is.EqualTo(System.Math.Floor(50400d / (100 * FactoryBalance.GetEngineProductionManDayMultiplier(FactoryAutomationTier.MechanizedCells)))));
        plant.WorkforceEmployeeLimit = 10;
        Assert.That(Estimate(engine, FactoryAutomationTier.HandBuilt), Is.EqualTo(2));
        Assert.That(Estimate(engine with { ManDays = 200 }, FactoryAutomationTier.HandBuilt), Is.EqualTo(1));
        plant.WorkforceEmployeeLimit = 0;
        Assert.That(Estimate(engine, FactoryAutomationTier.HandBuilt), Is.Zero);
        Assert.That(plant.EngineDesignId, Is.Empty);
        Assert.That(plant.EquipmentCost, Is.Zero, "Previewing capacity must not purchase equipment.");
    }
}
