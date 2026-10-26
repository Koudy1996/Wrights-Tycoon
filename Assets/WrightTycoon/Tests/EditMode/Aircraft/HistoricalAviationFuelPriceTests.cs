using NUnit.Framework;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Aircraft;

public sealed class HistoricalAviationFuelPriceTests
{
    [TestCase(1975, 7, false, .292)]
    [TestCase(1976, 1, false, .313)]
    [TestCase(1983, 1, true, 1.292)]
    [TestCase(1990, 10, false, 1.168)]
    [TestCase(2008, 7, false, 4.008)]
    [TestCase(2020, 4, false, .740)]
    [TestCase(2022, 3, true, 4.581)]
    [Category("Extended")]
    public void PublishedMonthlyRefinerObservationsMatchEiaAndConvertGallonToKilogram(int year, int month, bool avgas, double gallonPrice)
    {
        var result = HistoricalAviationFuelPrices.Get(new SimulationDate(year, month), avgas);
        Assert.That(result.DollarsPerGallon, Is.EqualTo(gallonPrice).Within(1e-10));
        var density = avgas ? HistoricalAviationFuelPrices.AvgasKilogramsPerLiter : HistoricalAviationFuelPrices.JetKilogramsPerLiter;
        Assert.That(result.DollarsPerKilogram, Is.EqualTo(gallonPrice / (3.785411784d * density)).Within(1e-10));
        Assert.That(result.IsEstimated, Is.False);
    }

    [TestCase(1946, 1, true)]
    [TestCase(1974, 12, false)]
    [TestCase(1997, 7, true)]
    [TestCase(2015, 1, true)]
    [TestCase(2022, 4, false)]
    [TestCase(2050, 1, false)]
    [Category("Extended")]
    public void EarlyMissingAndBeyondObservedHistoryAreClearlyMarkedEstimates(int year, int month, bool avgas)
    {
        var result = HistoricalAviationFuelPrices.Get(new SimulationDate(year, month), avgas);
        Assert.That(result.IsEstimated, Is.True);
        Assert.That(result.DollarsPerGallon, Is.GreaterThan(0));
        Assert.That(double.IsInfinity(result.DollarsPerKilogram), Is.False);
        Assert.That(result.Source, Is.Not.Empty);
    }

    [Test]
    public void PricesRefreshTogetherOnlyAtMonthChangeAndRestoreDeterministically()
    {
        var market = new MonthlyAviationFuelMarket();
        var january = new SimulationDate(2008, 1);
        var initial = market.Get(january, false);
        var avgas = market.Get(january, true);
        for (var i = 0; i < 100; i++) Assert.That(market.Get(january, false), Is.SameAs(initial));
        Assert.That(market.Get(january, true), Is.SameAs(avgas));
        Assert.That(market.Revision, Is.EqualTo(1));
        var february = market.Get(january.AddMonths(1), false);
        Assert.That(february.DollarsPerGallon, Is.Not.EqualTo(initial.DollarsPerGallon));
        Assert.That(market.Revision, Is.EqualTo(2));
        Assert.That(market.Get(january, false), Is.EqualTo(initial));
        Assert.That(market.Revision, Is.EqualTo(3));
    }

    [Test]
    public void DatasetHasPositiveFiniteQuotesAcrossEveryGameMonthForBothFuels()
    {
        for (var year = 1946; year <= 2030; year++)
        for (var month = 1; month <= 12; month++)
        foreach (var avgas in new[] { false, true })
        {
            var price = HistoricalAviationFuelPrices.Get(new SimulationDate(year, month), avgas);
            Assert.That(price.DollarsPerKilogram, Is.GreaterThan(0).And.LessThan(100));
        }
    }
}
