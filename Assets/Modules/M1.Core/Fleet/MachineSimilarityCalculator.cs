using System;
using System.Collections.Generic;
using WrightTycoon.Core.Engines;
using AircraftModel = WrightTycoon.Core.Aircraft.Aircraft;

namespace WrightTycoon.Core.Fleet;

public sealed record MachineDifferenceComponent(string Parameter, double WeightedDifferencePercent);
public sealed record MachineSimilarityResult(double DifferencePercent, bool CanShareParts,
    IReadOnlyList<MachineDifferenceComponent> Breakdown, IReadOnlyList<string> Reasons);

/// <summary>Symmetric construction similarity. Calculated reliability, power, speed and cost are deliberately excluded.</summary>
public static class MachineSimilarityCalculator
{
    public const double MaximumSharedPartsDifferencePercent = 10d;

    public static MachineSimilarityResult EngineDifference(Engine left, Engine right, bool sameFamily = true)
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
        var a = left.Parameters; var b = right.Parameters;
        var reasons = new List<string>();
        if (!sameFamily) reasons.Add("Different engine family lineage");
        if (left.Definition.EngineType != right.Definition.EngineType) reasons.Add("Different engine architecture");
        if (a.CylinderCount != b.CylinderCount) reasons.Add("Different cylinder count");
        var jet = left.Definition.EngineType is EngineType.Turbojet or EngineType.Turbofan;
        var score = new Score();
        score.Numeric(jet ? "Turbine diameter" : "Displacement", jet ? a.TurbineDiameterMeters : a.DisplacementLiters,
            jet ? b.TurbineDiameterMeters : b.DisplacementLiters, 40);
        score.Numeric(jet ? "Bypass ratio" : "Compression ratio", jet ? a.BypassRatio : a.CompressionRatio,
            jet ? b.BypassRatio : b.CompressionRatio, 12);
        score.Numeric("Maximum RPM", a.MaximumSpeed, b.MaximumSpeed, 10);
        score.Numeric("Cooling", a.CoolingEfficiency, b.CoolingEfficiency, 8);
        score.Discrete("Boost", a.BoostType, b.BoostType, 8);
        if (jet) score.Discrete("Afterburner", a.HasAfterburner, b.HasAfterburner, 5);
        else score.Discrete("Injection", a.PistonInjectionType, b.PistonInjectionType, 5);
        score.Numeric("Precision engineering", a.PrecisionEngineeringLevel, b.PrecisionEngineeringLevel, 7);
        score.Numeric("Construction durability", a.DurabilityLevel, b.DurabilityLevel, 8);
        score.Numeric("Efficiency optimization", a.EfficiencyOptimizationsPercent, b.EfficiencyOptimizationsPercent, 6);
        score.Numeric("Optimization altitude", a.OptimizationAltitudeMeters, b.OptimizationAltitudeMeters, 4);
        return score.Finish(reasons);
    }

    public static MachineSimilarityResult AircraftDifference(AircraftModel left, AircraftModel right, bool sameFamily = true)
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
        var a = left.Airframe.Parameters; var b = right.Airframe.Parameters;
        var ta = left.TypeDefinition; var tb = right.TypeDefinition;
        var reasons = new List<string>();
        if (!sameFamily || !string.Equals(left.FamilyDefinition.Id, right.FamilyDefinition.Id, StringComparison.Ordinal))
            reasons.Add("Different aircraft family");
        if (a.StructureType != b.StructureType) reasons.Add("Different airframe structure");
        if (a.MaterialType != b.MaterialType) reasons.Add("Different structural material");
        if (a.EngineCount != b.EngineCount) reasons.Add("Different engine count");
        var score = new Score();
        score.Numeric("Wing area", a.WingAreaSquareMeters, b.WingAreaSquareMeters, 30);
        score.Numeric("Wing aspect ratio", a.WingAspectRatio, b.WingAspectRatio, 15);
        score.Numeric("Fuel capacity", a.FuelAmountLiters, b.FuelAmountLiters, 12);
        score.Numeric("Engine cowling", a.EngineCowling, b.EngineCowling, 8);
        score.Numeric("Aerodynamic cleanliness", a.AerodynamicCleanlinessPercent, b.AerodynamicCleanlinessPercent, 8);
        score.Numeric("Precision engineering", a.PrecisionEngineeringLevel, b.PrecisionEngineeringLevel, 5);
        score.Numeric("Construction durability", ta.ConstructionDurabilityLevel, tb.ConstructionDurabilityLevel, 7);
        score.Discrete("Wing position", a.WingPosition, b.WingPosition, 4);
        score.Discrete("Wing shape", a.WingShape, b.WingShape, 3);
        if (a.UseModularWingProfile && b.UseModularWingProfile)
        {
            score.Numeric("Wing design speed", a.WingDesignSpeedKph, b.WingDesignSpeedKph, 1);
            score.Numeric("Wing load orientation", a.WingPayloadAgility + 100d, b.WingPayloadAgility + 100d, .5);
            score.Numeric("Wing profile efficiency", a.WingProfileEfficiency, b.WingProfileEfficiency, .5);
        }
        else
        {
            score.Discrete("Wing profile model", a.UseModularWingProfile, b.UseModularWingProfile, 2);
            score.Discrete("Wing profile", a.WingProfile, b.WingProfile, 2);
        }
        score.Discrete("Tail", a.TailType, b.TailType, 1);
        score.Discrete("Flaps", a.FlapType, b.FlapType, .5);
        score.Discrete("Leading-edge flaps", a.HasLeadingEdgeFlaps, b.HasLeadingEdgeFlaps, .5);
        score.Discrete("Landing gear", a.LandingGearType, b.LandingGearType, 1);
        // All operating equipment combined contributes at most three percentage points.
        score.Discrete("Navigation", ta.NavigationEquipment, tb.NavigationEquipment, .5);
        score.Discrete("Passenger cabin", ta.HasPassengerCabin, tb.HasPassengerCabin, .4);
        score.Discrete("Cargo space", ta.HasCargoSpace, tb.HasCargoSpace, .4);
        score.Discrete("Special equipment", ta.HasSpecialEquipment, tb.HasSpecialEquipment, .4);
        score.Discrete("Combat equipment", ta.HasCombatEquipment, tb.HasCombatEquipment, .4);
        score.Numeric("Weapon payload", ta.WeaponPayloadKilograms, tb.WeaponPayloadKilograms, .4);
        score.Numeric("Crew", a.CrewCount, b.CrewCount, .3);
        score.Discrete("Backup systems", a.HasBackupSystems, b.HasBackupSystems, .2);
        return score.Finish(reasons);
    }

    private sealed class Score
    {
        private readonly List<MachineDifferenceComponent> components = new();
        private double total;
        public void Numeric(string name, double a, double b, double weight)
        {
            LifecycleCalculator.RequireFiniteNonNegative(a, name);
            LifecycleCalculator.RequireFiniteNonNegative(b, name);
            Add(name, System.Math.Max(a, b) == 0d ? 0d : System.Math.Abs(a - b) / System.Math.Max(a, b) * weight);
        }
        public void Discrete<T>(string name, T a, T b, double weight) => Add(name, EqualityComparer<T>.Default.Equals(a, b) ? 0d : weight);
        private void Add(string name, double amount)
        {
            total += amount;
            if (amount > 0d) components.Add(new MachineDifferenceComponent(name, amount));
        }
        public MachineSimilarityResult Finish(List<string> reasons)
        {
            var difference = reasons.Count > 0 ? 100d : System.Math.Min(100d, total);
            if (difference > MaximumSharedPartsDifferencePercent && reasons.Count == 0) reasons.Add("Construction difference exceeds ten percent");
            return new MachineSimilarityResult(difference, difference <= MaximumSharedPartsDifferencePercent,
                components.AsReadOnly(), reasons.AsReadOnly());
        }
    }
}
