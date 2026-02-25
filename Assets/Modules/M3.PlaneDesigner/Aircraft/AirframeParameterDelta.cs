using WrightTycoon.Core.Airframes;

namespace WrightTycoon.Core.Aircraft;

public sealed class AirframeParameterDelta
{
    public int? EngineCountOverride { get; init; }

    public double WingAreaSquareMetersDelta { get; init; }

    public double FuelAmountLitersDelta { get; init; }

    public int CrewCountDelta { get; init; }

    public WingPosition? WingPositionOverride { get; init; }

    public WingShape? WingShapeOverride { get; init; }

    public WingProfileType? WingProfileOverride { get; init; }

    public TailType? TailTypeOverride { get; init; }

    public FlapType? FlapTypeOverride { get; init; }

    public bool? HasLeadingEdgeFlapsOverride { get; init; }

    public bool? HasBackupSystemsOverride { get; init; }

    public LandingGearType? LandingGearTypeOverride { get; init; }

    public double EngineCowlingDelta { get; init; }

    public double AerodynamicCleanlinessPercentDelta { get; init; }

    public RunwayType? RunwayTypeOverride { get; init; }
}
