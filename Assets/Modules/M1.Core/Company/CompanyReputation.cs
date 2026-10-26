using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Company;

public sealed record ReputationChange(int MonthIndex, double Score, double Change, string Reason);
public sealed record AccidentReputationObservation(int MonthIndex, double ExpectedLosses, int ActualLosses);

public sealed class CompanyReputationSnapshot
{
    public double Score { get; set; } = 50d;
    public int LastProcessedMonth { get; set; } = -1;
    public List<ReputationChange> History { get; set; } = new();
    public List<AccidentReputationObservation> AccidentHistory { get; set; } = new();
}

/// <summary>Commercial trust from unique warranty-aircraft maintenance and exposure-adjusted safety.</summary>
public sealed class CompanyReputation
{
    private readonly List<ReputationChange> _history = new();
    private readonly List<AccidentReputationObservation> _accidentHistory = new();
    public double Score { get; private set; } = 50d;
    public int LastProcessedMonth { get; private set; } = -1;
    public double PriceMultiplier => 0.85d + (Score * 0.003d);
    public IReadOnlyList<ReputationChange> History => _history.AsReadOnly();

    public void ProcessMonth(int monthIndex, FleetMonthReport report)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));
        Numeric.ThrowIfNotFinite(report.AircraftHours, nameof(report.AircraftHours));
        Numeric.ThrowIfNotFinite(report.ExpectedAccidentLosses, nameof(report.ExpectedAccidentLosses));
        if (monthIndex < 0 || report.AircraftHours < 0 || report.ExpectedAccidentLosses < 0 || report.AccidentLosses < 0
            || report.ActiveAircraftAtStart < 0 || report.WarrantyAircraft > report.ActiveAircraftAtStart || report.WarrantyAircraft < 0 || report.WarrantyServicedAircraft < 0 || report.WarrantyMissedAircraft < 0
            || (long)report.WarrantyServicedAircraft + report.WarrantyMissedAircraft > report.WarrantyAircraft)
            throw new ArgumentOutOfRangeException(nameof(report));
        if (monthIndex <= LastProcessedMonth) return;
        LastProcessedMonth = monthIndex;
        // No maintenance event means no warranty reward. Failure overrides success for each aircraft.
        var serviceChange = report.ActiveAircraftAtStart == 0 ? 0d
            : (.25d * report.WarrantyServicedAircraft - 3d * report.WarrantyMissedAircraft) / report.ActiveAircraftAtStart;
        _accidentHistory.RemoveAll(o => o.MonthIndex < monthIndex - 23);
        _accidentHistory.Add(new AccidentReputationObservation(monthIndex, report.ExpectedAccidentLosses, report.AccidentLosses));
        var expected = _accidentHistory.Sum(o => o.ExpectedLosses);
        var actual = _accidentHistory.Sum(o => (double)o.ActualLosses);
        // A 24-month window and five baseline pseudo-losses smooth rare-event noise. Positive evidence
        // must accumulate actual exposure; idle months and lucky tiny fleets cannot farm safety rewards.
        var comparison = Numeric.Clamp((expected - actual) / (expected + 5d), -1d, 1d);
        var evidence = System.Math.Min(1d, expected / 5d)
            * report.ExpectedAccidentLosses / (report.ExpectedAccidentLosses + .25d);
        var safetyChange = .5d * comparison * evidence;
        Add(monthIndex, serviceChange + safetyChange,
            $"Warranty fleet {report.WarrantyAircraft}; serviced {report.WarrantyServicedAircraft}; missed {report.WarrantyMissedAircraft}; "
            + $"accidents {report.AccidentLosses}; baseline {report.ExpectedAccidentLosses:0.###}; safety change {safetyChange:+0.###;-0.###;0}.");
    }

    private void Add(int monthIndex, double change, string reason)
    {
        var previous = Score;
        Score = Numeric.Clamp(Score + change, 0d, 100d);
        _history.Add(new ReputationChange(monthIndex, Score, Score - previous, reason));
        if (_history.Count > 600) _history.RemoveAt(0);
    }

    public CompanyReputationSnapshot CreateSnapshot() => new()
    {
        Score = Score, LastProcessedMonth = LastProcessedMonth, History = _history.ToList(),
        AccidentHistory = _accidentHistory.ToList(),
    };

    public void RestoreSnapshot(CompanyReputationSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        Numeric.ThrowIfNotFinite(snapshot.Score, nameof(snapshot.Score));
        var observations = snapshot.AccidentHistory ?? new List<AccidentReputationObservation>();
        foreach (var observation in observations)
        {
            Numeric.ThrowIfNotFinite(observation.ExpectedLosses, nameof(observation.ExpectedLosses));
            if (observation.ExpectedLosses < 0 || observation.ActualLosses < 0 || observation.MonthIndex < 0)
                throw new ArgumentException("Invalid accident reputation history.", nameof(snapshot));
        }
        Score = Numeric.Clamp(snapshot.Score, 0d, 100d);
        LastProcessedMonth = snapshot.LastProcessedMonth;
        _history.Clear();
        _history.AddRange((snapshot.History ?? new List<ReputationChange>()).TakeLast(600));
        _accidentHistory.Clear();
        _accidentHistory.AddRange(observations.Where(o => o.MonthIndex >= LastProcessedMonth - 23 && o.MonthIndex <= LastProcessedMonth));
    }
}
