using System;
using WrightTycoon.Core.Categories;

namespace WrightTycoon.Core.Fleet;

public enum AccidentExposureUnit { FlightHours100000, Flights1000000 }

public sealed record AccidentRate(double Rate, AccidentExposureUnit Unit, double HullLossFraction,
    double TechnicalFraction, double ReferenceReliability, string Evidence, bool IsEstimate);

/// <summary>Historical evidence is deliberately distinguished from game severity/profile assumptions.
/// Rates represent single-aircraft losses; combat is excluded. See Docs/FleetAccidentResearch.md.</summary>
public static class FleetAccidentCatalog
{
    // Game balance: halve accident frequency while preserving the historical reference rates.
    private const double AccidentFrequencyMultiplier = .5d;

    // Verified BTS table 2-14 counts/exposure; sparse reference years, not complete decades.
    // Footnote-a exclusions are reflected where listed. Small differences from printed rates are documented.
    // Entries are reference years, never described as complete decade averages.
    private static readonly int[] GaYears = { 1946, 1950, 1960, 1965, 1970, 1975, 1980, 1985, 1990, 2000, 2010, 2023 };
    private static readonly double[] GaRates = { 55, 45, 479300d / 13121d, 519600d / 16733d,
        471200d / 26030d, 399500d / 28799d, 359000d / 36402d, 272800d / 28322d,
        223800d / 28510d, 183000d / 27838d, 5.5, 3.86 };
    private static readonly int[] JetYears = { 1960, 1970, 1980, 1990, 2000, 2010, 2020 };
    private static readonly double[] JetRates = { 8, 3, 1.8, 1.2, .75, .35, .30 };

    public static AccidentRate Get(Simulation.SimulationDate date, AircraftCategory category, string profileId)
    {
        var explicitPiston = profileId != null && profileId.IndexOf("piston", StringComparison.OrdinalIgnoreCase) >= 0;
        var jet = !explicitPiston && category is (AircraftCategory.RegionalJet or AircraftCategory.MediumTransportNarrowBody
            or AircraftCategory.HeavyTransportWideBody or AircraftCategory.HeavyCargo or AircraftCategory.LongRangeCargo);
        var year = date.Year + (date.Month - 1d) / 12d;
        if (jet && date.Year >= 1960)
            return new AccidentRate(Interpolate(JetYears, JetRates, year), AccidentExposureUnit.Flights1000000,
                1d, .25d, LifecycleBalance.ReferenceReliability(category),
                "Game hull-loss trend estimate; Boeing 1959–2024 scope informs trend only, not calendar-decade counts.", true);

        var multiplier = category switch
        {
            AircraftCategory.MediumTransportNarrowBody or AircraftCategory.HeavyTransportWideBody
                or AircraftCategory.MediumCargo or AircraftCategory.HeavyCargo or AircraftCategory.LongRangeCargo => .25d,
            AircraftCategory.TurbopropRegional => .35d,
            AircraftCategory.BusinessVipJet => .45d,
            AircraftCategory.SpecialCivil => 1.3d,
            AircraftCategory.MilitaryTactical => 1.5d,
            AircraftCategory.MilitaryStrategicSupport or AircraftCategory.Bomber => .6d,
            _ => 1d,
        };
        if (profileId != null && profileId.IndexOf("training", StringComparison.OrdinalIgnoreCase) >= 0) multiplier *= 1.2d;
        return new AccidentRate(Interpolate(GaYears, GaRates, year) * multiplier,
            AccidentExposureUnit.FlightHours100000, .25d, .25d, LifecycleBalance.ReferenceReliability(category),
            "BTS 2005 table 2-14 reference-year counts/hours; interpolated. Pre-1960/2010 and profile/severity factors are game estimates; no combat losses.", true);
    }

    public static double LossIntensity(AccidentRate rate, double hours, double averageFlightHours,
        double reliability, double wear, double hoursToContractLife, bool technical)
    {
        var exposure = rate.Unit == AccidentExposureUnit.FlightHours100000 ? hours / 100000d : hours / averageFlightHours / 1000000d;
        var intensity = exposure * rate.Rate * rate.HullLossFraction * AccidentFrequencyMultiplier;
        if (!technical) return intensity * (1d - rate.TechnicalFraction);
        // Balance assumptions, not historical causal estimates. Nominal service already affects frozen life.
        var reliabilityFactor = System.Math.Max(.25d, System.Math.Min(4d, System.Math.Pow(rate.ReferenceReliability / System.Math.Max(.05d, reliability), 2d)));
        var excessWear = System.Math.Max(0d, wear - 1d);
        var excessLife = System.Math.Max(0d, hoursToContractLife - 1d);
        return intensity * rate.TechnicalFraction * reliabilityFactor * (1d + 8d * excessWear * excessWear + 8d * excessLife * excessLife);
    }

    /// <summary>Game-adjusted historical baseline: reference reliability and no excess wear or life.</summary>
    public static double BaselineLossProbability(AccidentRate rate, double hours, double averageFlightHours)
    {
        var mu = LossIntensity(rate, hours, averageFlightHours, rate.ReferenceReliability, 0d, 0d, true)
            + LossIntensity(rate, hours, averageFlightHours, rate.ReferenceReliability, 0d, 0d, false);
        return 1d - System.Math.Exp(-mu);
    }

    /// <summary>Illustrative exposure at a fixed current rate, not a fleet survival forecast.</summary>
    public static double ReferenceLossProbability(Simulation.SimulationDate date, AircraftCategory category,
        string profileId, double hours, double averageFlightHours, double reliability, double wear = 0d,
        double hoursToContractLife = 0d)
    {
        LifecycleCalculator.RequireFiniteNonNegative(hours, nameof(hours));
        LifecycleCalculator.RequireFiniteNonNegative(averageFlightHours, nameof(averageFlightHours));
        if (averageFlightHours <= 0d) throw new ArgumentOutOfRangeException(nameof(averageFlightHours));
        LifecycleCalculator.ValidateReliability(reliability);
        LifecycleCalculator.RequireFiniteNonNegative(wear, nameof(wear));
        LifecycleCalculator.RequireFiniteNonNegative(hoursToContractLife, nameof(hoursToContractLife));
        var rate = Get(date, category, profileId);
        var intensity = LossIntensity(rate, hours, averageFlightHours, reliability, wear, hoursToContractLife, true)
            + LossIntensity(rate, hours, averageFlightHours, reliability, wear, hoursToContractLife, false);
        return 1d - System.Math.Exp(-intensity);
    }

    private static double Interpolate(int[] years, double[] rates, double year)
    {
        if (year <= years[0]) return rates[0];
        for (var i = 1; i < years.Length; i++)
            if (year < years[i]) return System.Math.Exp(System.Math.Log(rates[i - 1]) + (System.Math.Log(rates[i]) - System.Math.Log(rates[i - 1])) * (year - years[i - 1]) / (years[i] - years[i - 1]));
        return rates[rates.Length - 1];
    }
}
