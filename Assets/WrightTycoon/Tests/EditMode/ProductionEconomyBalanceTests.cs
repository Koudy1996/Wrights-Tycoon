using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Balance;

namespace WrightTycoon.Core.Tests;

[TestFixture]
public sealed class ProductionEconomyBalanceTests
{
    [Test]
    public void ReferenceLightAircraft_CostsAboutFortyThousandDollars()
    {
        var airframeParts = ProductionEconomyBalance.CalculateAirframeBaseParts(15.6d)
            * ProductionEconomyBalance.Airframe.WoodPartsMultiplier
            * ProductionEconomyBalance.Airframe.TrussPartsMultiplier
            * AircraftConstructionStandardBalance.GetEffects(AircraftConstructionStandard.Ultralight).ProductionCostMultiplier;
        var airframeManDays = ProductionEconomyBalance.CalculateAirframeBaseManDays(15.6d)
            * ProductionEconomyBalance.Airframe.WoodManDaysMultiplier
            * ProductionEconomyBalance.Airframe.TrussManDaysMultiplier
            * AircraftConstructionStandardBalance.GetEffects(AircraftConstructionStandard.Ultralight).ProductionManDaysMultiplier;

        const int cylinders = 4;
        const double displacement = 2.4d;
        var cylinderVolume = displacement / cylinders;
        var engineParts = ProductionEconomyBalance.Engine.PistonBaseParts
            + (cylinders * ProductionEconomyBalance.Engine.PistonCylinderParts * System.Math.Pow(
                cylinderVolume / ProductionEconomyBalance.Engine.PistonReferenceCylinderVolumeLiters,
                ProductionEconomyBalance.Engine.PistonCylinderPartsExponent))
            + (displacement * ProductionEconomyBalance.Engine.PistonDisplacementPartsPerLiter);
        var engineManDays = ProductionEconomyBalance.Engine.PistonBaseManDays
            + (cylinders * ProductionEconomyBalance.Engine.PistonCylinderManDays * System.Math.Pow(
                cylinderVolume / ProductionEconomyBalance.Engine.PistonReferenceCylinderVolumeLiters,
                ProductionEconomyBalance.Engine.PistonCylinderManDaysExponent))
            + (displacement * ProductionEconomyBalance.Engine.PistonDisplacementManDaysPerLiter);
        var total = ProductionEconomyBalance.CalculateCompleteCost(airframeParts, airframeManDays)
            + ProductionEconomyBalance.CalculateCompleteCost(engineParts, engineManDays);

        Assert.That(airframeParts, Is.EqualTo(23000d).Within(50d));
        Assert.That(airframeManDays, Is.EqualTo(150d).Within(0.1d));
        Assert.That(engineManDays, Is.EqualTo(50d).Within(0.1d));
        Assert.That(total, Is.InRange(39500d, 40500d));
    }
}
