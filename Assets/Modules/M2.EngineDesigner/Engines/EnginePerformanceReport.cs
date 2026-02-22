using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Units;

namespace WrightTycoon.Core.Engines;

public sealed class EnginePerformanceReport
{
    private readonly IReadOnlyList<EngineAltitudePerformance> _altitudePerformance;

    public EnginePerformanceReport(
        EngineOutputKind outputKind,
        IEnumerable<EngineAltitudePerformance> altitudePerformance,
        double reliability,
        Mass weight,
        double cost,
        double productionManDays,
        double engineLengthMeters = 0d,
        double engineFrontalAreaSquareMeters = 0d)
    {
        OutputKind = outputKind;
        _altitudePerformance = altitudePerformance?.ToArray() ?? throw new ArgumentNullException(nameof(altitudePerformance));
        Reliability = reliability;
        Weight = weight;
        Cost = cost;
        ProductionManDays = productionManDays;
        EngineLengthMeters = engineLengthMeters;
        EngineFrontalAreaSquareMeters = engineFrontalAreaSquareMeters;
    }

    public EngineOutputKind OutputKind { get; }

    public IReadOnlyList<EngineAltitudePerformance> AltitudePerformance => _altitudePerformance;

    public double Reliability { get; }

    public Mass Weight { get; }

    public double Cost { get; }

    public double ProductionManDays { get; }

    public double EngineLengthMeters { get; }

    public double EngineFrontalAreaSquareMeters { get; }

    public EngineAltitudePerformance GetPerformanceAtAltitude(double altitudeMeters)
    {
        for (var index = 0; index < _altitudePerformance.Count; index++)
        {
            var point = _altitudePerformance[index];
            if (System.Math.Abs(point.AltitudeMeters - altitudeMeters) < 1e-6)
            {
                return point;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(altitudeMeters), $"No performance point available for altitude {altitudeMeters}m.");
    }

    public double GetThrustNewtonsAtAltitude(double altitudeMeters)
    {
        var point = GetPerformanceAtAltitude(altitudeMeters);
        if (point.ThrustNewtons > 0d)
        {
            return point.ThrustNewtons;
        }

        if (OutputKind == EngineOutputKind.ThrustNewtons)
        {
            return point.Output;
        }

        return 0d;
    }
}
