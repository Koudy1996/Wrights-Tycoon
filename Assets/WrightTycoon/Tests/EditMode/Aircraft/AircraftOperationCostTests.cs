using System;
using System.Collections.Generic;
using NUnit.Framework;
using WrightTycoon.Core.Contracts;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.GovernmentContracts;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class AircraftOperationCostTests
{
    [TestCase(499d, 0)]
    [TestCase(500d, 0)]
    [TestCase(1000d, 1)]
    [TestCase(1000.001d, 2)]
    [TestCase(1200d, 2)]
    [TestCase(1500d, 2)]
    [Category("Extended")]
    public void RetirementPrecedesMaintenanceAtExactLifetime(double life, int expectedCycles)
    {
        var quote = AircraftOperationCostCalculator.Calculate(100000, life, 20, 2, 500, 3, 100);
        Assert.That(quote.MaintenanceCycles, Is.EqualTo(expectedCycles));
        Assert.That(quote.SparePartsPerHour, Is.EqualTo(expectedCycles * 300d / life).Within(1e-10));
    }

    [Test]
    public void OperationCostUsesPurchaseDepreciationConsumedPartsAndFuelOnly()
    {
        var quote = AircraftOperationCostCalculator.Calculate(120000, 1200, 20, 2, 500, 3, 100);
        Assert.That(quote.DepreciationPerHour, Is.EqualTo(100));
        Assert.That(quote.SparePartsPerHour, Is.EqualTo(.5));
        Assert.That(quote.FuelPerHour, Is.EqualTo(40));
        Assert.That(quote.TotalPerHour, Is.EqualTo(140.5));
        Assert.That(quote.MaintenanceCycles, Is.EqualTo(2));
    }

    [Test]
    public void NoServiceBeforeRetirementStillAmortizesPurchasePriceAndBurnsFuel()
    {
        var quote = AircraftOperationCostCalculator.Calculate(100000, 100, 10, .9, 200, 11, 1000);
        Assert.That(quote.MaintenanceCycles, Is.Zero);
        Assert.That(quote.SparePartsPerHour, Is.Zero);
        Assert.That(quote.TotalPerHour, Is.EqualTo(1009));
    }

    [Test]
    [Category("Extended")]
    public void LegacyEightThousandHourOfferIsValidButIsNotSelectable()
    {
        CollectionAssert.AreEqual(new[] { 200, 500, 1000, 2000, 4000 }, LifecycleBalance.ServiceIntervalsHours);
        Assert.DoesNotThrow(() => LifecycleCalculator.ValidateServiceInterval(8000));
        var quote = AircraftOperationCostCalculator.Calculate(100000, 16000, 0, 0, 8000, 2, 100);
        Assert.That(quote.MaintenanceCycles, Is.EqualTo(1));
        Assert.That(quote.SparePartsPerHour, Is.EqualTo(.0125));
    }

    [Test]
    public void BothContractGeneratorsUseOnlyCurrentSelectableIntervals()
    {
        var civil = new ContractGenerator(seed: 3721);
        var government = new GovernmentContractGenerator(new SystemGovernmentContractRandomProvider(3721));
        var observedCivil = new HashSet<int>();
        var observedGovernment = new HashSet<int>();
        for (var month = 0; month < 100; month++)
        {
            foreach (var contract in civil.GenerateMonthlyContracts(month))
            {
                var interval = contract.Requirements.MaximumServiceIntervalHours;
                CollectionAssert.Contains(LifecycleBalance.ServiceIntervalsHours, interval);
                observedCivil.Add(interval);
            }
            var stateInterval = government.Generate(new SimulationDate(2000, 1).AddMonths(month), month)
                .Requirements.MaximumServiceIntervalHours;
            CollectionAssert.Contains(LifecycleBalance.ServiceIntervalsHours, stateInterval);
            observedGovernment.Add(stateInterval);
        }
        CollectionAssert.DoesNotContain(observedCivil, 8000);
        CollectionAssert.DoesNotContain(observedGovernment, 8000);
        CollectionAssert.Contains(observedCivil, 200);
        CollectionAssert.Contains(observedGovernment, 200);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [Category("Extended")]
    public void EveryMonetaryOrHourInputRejectsNonFiniteAndNegativeValues(int argument)
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d })
        {
            var values = new[] { 120000d, 1200d, 20d, 2d, 100d };
            values[argument] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => AircraftOperationCostCalculator.Calculate(
                values[0], values[1], values[2], values[3], 500, 3, values[4]));
        }
    }

    [Test]
    [Category("Extended")]
    public void ZeroLifeInvalidIntervalZeroQuantityAndOverflowAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AircraftOperationCostCalculator.Calculate(1, 0, 0, 0, 500, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AircraftOperationCostCalculator.Calculate(1, 1000, 0, 0, 1500, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AircraftOperationCostCalculator.Calculate(1, 1000, 0, 0, 500, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AircraftOperationCostCalculator.Calculate(1, double.MaxValue, 0, 0, 500, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AircraftOperationCostCalculator.Calculate(1, 1000, double.MaxValue, 2, 500, 1, 0));
        Assert.That(AircraftOperationCostCalculator.Calculate(0, 1000, 0, 0, 500, 1, 0).TotalPerHour, Is.Zero);
    }
}
