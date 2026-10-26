using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Engines;

public sealed class EngineParameters
{
    public double DisplacementLiters { get; set; } = 12d;

    public int CylinderCount { get; set; } = 12;

    public double CompressionRatio { get; set; } = 12d;

    public BoostType BoostType { get; set; } = BoostType.None;

    public PistonInjectionType PistonInjectionType { get; set; } = PistonInjectionType.DirectInjection;

    public double OptimizationAltitudeMeters { get; set; } = 1500d;

    public double TurbineDiameterMeters { get; set; } = 1.2d;

    public double BypassRatio { get; set; } = 0.2d;

    // Interpreted as RPM for gameplay balancing.
    public double MaximumSpeed { get; set; } = 2500d;

    public double CoolingEfficiency { get; set; } = 0.75d;

    public int PrecisionEngineeringLevel { get; set; }

    public double EfficiencyOptimizationsPercent { get; set; }

    public bool HasAfterburner { get; set; }

    public int ServiceIntervalHours { get; set; } = 500;

    public int DurabilityLevel { get; set; }

    public EngineParameters Clone() =>
        new()
        {
            DisplacementLiters = DisplacementLiters,
            CylinderCount = CylinderCount,
            CompressionRatio = CompressionRatio,
            BoostType = BoostType,
            PistonInjectionType = PistonInjectionType,
            OptimizationAltitudeMeters = OptimizationAltitudeMeters,
            TurbineDiameterMeters = TurbineDiameterMeters,
            BypassRatio = BypassRatio,
            MaximumSpeed = MaximumSpeed,
            CoolingEfficiency = CoolingEfficiency,
            PrecisionEngineeringLevel = PrecisionEngineeringLevel,
            EfficiencyOptimizationsPercent = EfficiencyOptimizationsPercent,
            HasAfterburner = HasAfterburner,
            ServiceIntervalHours = ServiceIntervalHours,
            DurabilityLevel = DurabilityLevel,
        };

    public void Validate()
    {
        Fleet.EngineMaintenancePolicy.ValidateSelectableServiceInterval(ServiceIntervalHours);
        if (DurabilityLevel < 0 || DurabilityLevel > 10)
            throw new System.ArgumentOutOfRangeException(nameof(DurabilityLevel));
        Numeric.ThrowIfNegative(DisplacementLiters, nameof(DisplacementLiters));
        if (CylinderCount < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(CylinderCount), "Cylinder count must be >= 1.");
        }

        Numeric.ThrowIfNegative(CompressionRatio, nameof(CompressionRatio));
        Numeric.ThrowIfNegative(OptimizationAltitudeMeters, nameof(OptimizationAltitudeMeters));
        Numeric.ThrowIfNegative(TurbineDiameterMeters, nameof(TurbineDiameterMeters));
        Numeric.ThrowIfNegative(BypassRatio, nameof(BypassRatio));
        Numeric.ThrowIfNegative(MaximumSpeed, nameof(MaximumSpeed));
        if (!System.Enum.IsDefined(typeof(PistonInjectionType), PistonInjectionType))
        {
            throw new System.ArgumentOutOfRangeException(nameof(PistonInjectionType), "Unsupported piston injection type.");
        }

        var cooling = Numeric.Clamp(CoolingEfficiency, 0d, 1d);
        if (!Numeric.AlmostEqual(cooling, CoolingEfficiency))
        {
            throw new System.ArgumentOutOfRangeException(nameof(CoolingEfficiency), "Cooling efficiency must be within [0, 1].");
        }

        if (PrecisionEngineeringLevel < PrecisionEngineeringBalance.MinimumLevel
            || PrecisionEngineeringLevel > PrecisionEngineeringBalance.MaximumLevel)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(PrecisionEngineeringLevel),
                $"Precision engineering level must be within [{PrecisionEngineeringBalance.MinimumLevel}, {PrecisionEngineeringBalance.MaximumLevel}].");
        }

        var optimization = Numeric.Clamp(
            EfficiencyOptimizationsPercent,
            EngineDesignerBalance.Engine.EfficiencyOptimizationMinPercent,
            EngineDesignerBalance.Engine.EfficiencyOptimizationMaxPercent);
        if (!Numeric.AlmostEqual(optimization, EfficiencyOptimizationsPercent))
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(EfficiencyOptimizationsPercent),
                $"Efficiency optimizations must be within [{EngineDesignerBalance.Engine.EfficiencyOptimizationMinPercent}, {EngineDesignerBalance.Engine.EfficiencyOptimizationMaxPercent}] %.");
        }
    }
}
