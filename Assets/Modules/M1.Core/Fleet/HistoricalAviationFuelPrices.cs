using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Fleet;

public sealed record AviationFuelPrice(int Year, int Month, double DollarsPerGallon, double DollarsPerKilogram,
    bool IsEstimated, string Source);

/// <summary>Offline nominal-USD history. Observed refiner data and explicitly marked model proxies, never a live price feed.</summary>
public static partial class HistoricalAviationFuelPrices
{
    public const double LitersPerUsGallon = 3.785411784d;
    public const double JetKilogramsPerLiter = .8d;
    public const double AvgasKilogramsPerLiter = .72d;
    private static readonly SortedDictionary<int, double> JetRetail;
    private static readonly SortedDictionary<int, double> AvgasRetail;
    private static readonly SortedDictionary<int, double> JetSpot;
    private static readonly SortedDictionary<int, double> CrudeAnnual;

    static HistoricalAviationFuelPrices()
    {
        JetRetail = ParseMonthly(JetRetailRows);
        AvgasRetail = ParseMonthly(AvgasRetailRows);
        JetSpot = ParseMonthly(JetSpotRows);
        CrudeAnnual = ParseAnnual(CrudeAnnualRows);
    }

    public static AviationFuelPrice Get(SimulationDate date, bool aviationGasoline)
    {
        var key = date.Year * 12 + date.Month - 1;
        var jet = GetJet(key);
        var price = jet.price;
        var estimated = jet.estimated;
        var source = jet.source;
        if (aviationGasoline)
        {
            if (AvgasRetail.TryGetValue(key, out var observed))
            { price = observed; estimated = false; source = "EIA aviation gasoline refiner end-user price"; }
            else
            {
                var anchor = PreviousOrFirst(AvgasRetail, key);
                price = jet.price * anchor.Value / GetJet(anchor.Key).price;
                estimated = true;
                source = "Model avgas estimate linked to EIA jet history; missing/early avgas observation";
            }
        }
        return new AviationFuelPrice(date.Year, date.Month, price,
            price / (LitersPerUsGallon * (aviationGasoline ? AvgasKilogramsPerLiter : JetKilogramsPerLiter)), estimated, source);
    }

    private static (double price, bool estimated, string source) GetJet(int key)
    {
        if (JetRetail.TryGetValue(key, out var observed))
            return (observed, false, "EIA kerosene jet refiner end-user price");
        var first = JetRetail.First();
        if (key < first.Key)
        {
            // Fixed refining premium calibrated at first observed jet month, plus interpolated annual crude input.
            // This is a game proxy, NOT measured aviation fuel before July1975.
            var year = key / 12;
            var fraction = (key % 12) / 12d;
            double Annual(int y) => CrudeAnnual.TryGetValue(y, out var value) ? value : PreviousOrFirst(CrudeAnnual, y).Value;
            var crude = Annual(year) + (Annual(year + 1) - Annual(year)) * fraction;
            var premium = first.Value - Annual(1975) / 42d;
            return (System.Math.Max(.001d, crude / 42d + premium), true, "Model jet estimate: EIA annual crude + fixed refining premium (pre-July1975)");
        }
        var last = JetRetail.Last();
        var spot = PreviousOrFirst(JetSpot, key);
        var bridge = PreviousOrFirst(JetSpot, last.Key);
        var linkedPrice = spot.Value * last.Value / bridge.Value;
        return (linkedPrice, true, key > JetSpot.Last().Key
            ? "EIA spot-linked jet estimate, held at last available month"
            : "EIA jet spot-linked estimate after refiner series ended (March2022)");
    }

    private static KeyValuePair<int, double> PreviousOrFirst(SortedDictionary<int, double> values, int key)
    {
        var result = values.First();
        foreach (var entry in values)
        { if (entry.Key > key) break; result = entry; }
        return result;
    }

    private static SortedDictionary<int, double> ParseMonthly(IEnumerable<string> rows)
    {
        var result = new SortedDictionary<int, double>();
        foreach (var row in rows)
        {
            var cells = row.Split(',');
            var year = int.Parse(cells[0], CultureInfo.InvariantCulture);
            for (var month = 1; month <= 12 && month < cells.Length; month++)
                if (double.TryParse(cells[month], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0d)
                    result.Add(year * 12 + month - 1, value);
        }
        return result;
    }

    private static SortedDictionary<int, double> ParseAnnual(IEnumerable<string> rows)
    {
        var result = new SortedDictionary<int, double>();
        foreach (var row in rows)
        {
            var cells = row.Split(',');
            var decade = int.Parse(cells[0].Substring(0, 4), CultureInfo.InvariantCulture);
            for (var i = 1; i < cells.Length; i++)
                if (double.TryParse(cells[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0d)
                    result.Add(decade + i - 1, value);
        }
        return result;
    }
}

/// <summary>Both fuel prices refresh together only when the simulation month changes, including save/date restoration.</summary>
public sealed class MonthlyAviationFuelMarket
{
    private int _monthKey = -1;
    private AviationFuelPrice? _jet;
    private AviationFuelPrice? _avgas;
    public int Revision { get; private set; }

    public AviationFuelPrice Get(SimulationDate date, bool aviationGasoline)
    {
        var key = date.Year * 12 + date.Month - 1;
        if (key != _monthKey)
        {
            _jet = HistoricalAviationFuelPrices.Get(date, false);
            _avgas = HistoricalAviationFuelPrices.Get(date, true);
            _monthKey = key;
            Revision++;
        }
        return aviationGasoline ? _avgas! : _jet!;
    }
}
