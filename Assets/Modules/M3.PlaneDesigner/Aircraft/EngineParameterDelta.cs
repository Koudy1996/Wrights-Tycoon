using WrightTycoon.Core.Engines;

namespace WrightTycoon.Core.Aircraft;

public sealed class EngineParameterDelta
{
    public double DisplacementLitersDelta { get; init; }

    public double CompressionRatioDelta { get; init; }

    public BoostType? BoostTypeOverride { get; init; }

    public double OptimizationAltitudeMetersDelta { get; init; }

    public double TurbineDiameterMetersDelta { get; init; }

    public double BypassRatioDelta { get; init; }

    public double CoolingEfficiencyDelta { get; init; }
}
