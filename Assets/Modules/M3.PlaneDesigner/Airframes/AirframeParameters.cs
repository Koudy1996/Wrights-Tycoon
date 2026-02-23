using System;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.Airframes;

public sealed class AirframeParameters
{
    public int EngineCount { get; set; } = 1;

    public StructureType StructureType { get; set; } = StructureType.Truss;

    public MaterialType MaterialType { get; set; } = MaterialType.WoodFabric;

    public WingPosition WingPosition { get; set; } = WingPosition.High;

    public WingShape WingShape { get; set; } = WingShape.Straight;

    public double WingAreaSquareMeters { get; set; } = 28d;

    public TailType TailType { get; set; } = TailType.Classic;

    public FlapType FlapType { get; set; } = FlapType.Plain;

    public bool HasLeadingEdgeFlaps { get; set; }

    public bool HasBackupSystems { get; set; }

    public LandingGearType LandingGearType { get; set; } = LandingGearType.Fixed;

    public double EngineCowling { get; set; } = 0.5d;

    public double FuelAmountLiters { get; set; } = 800d;

    public int CrewCount { get; set; } = 2;

    public double AerodynamicCleanlinessPercent { get; set; }

    public RunwayType RunwayType { get; set; } = RunwayType.Medium;

    public AirframeParameters Clone() =>
        new()
        {
            EngineCount = EngineCount,
            StructureType = StructureType,
            MaterialType = MaterialType,
            WingPosition = WingPosition,
            WingShape = WingShape,
            WingAreaSquareMeters = WingAreaSquareMeters,
            TailType = TailType,
            FlapType = FlapType,
            HasLeadingEdgeFlaps = HasLeadingEdgeFlaps,
            HasBackupSystems = HasBackupSystems,
            LandingGearType = LandingGearType,
            EngineCowling = EngineCowling,
            FuelAmountLiters = FuelAmountLiters,
            CrewCount = CrewCount,
            AerodynamicCleanlinessPercent = AerodynamicCleanlinessPercent,
            RunwayType = RunwayType,
        };

    public void Validate()
    {
        if (EngineCount < 1 || EngineCount > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(EngineCount), "Engine count must be within [1, 6].");
        }

        if (WingAreaSquareMeters < 1d || WingAreaSquareMeters > 1000d)
        {
            throw new ArgumentOutOfRangeException(nameof(WingAreaSquareMeters), "Wing area must be within [1, 1000] m2.");
        }

        if (FuelAmountLiters < PlaneDesignerBalance.Airframe.FuelMinLiters
            || FuelAmountLiters > PlaneDesignerBalance.Airframe.FuelMaxLiters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FuelAmountLiters),
                $"Fuel amount must be within [{PlaneDesignerBalance.Airframe.FuelMinLiters}, {PlaneDesignerBalance.Airframe.FuelMaxLiters}] liters.");
        }

        if (CrewCount < PlaneDesignerBalance.Airframe.CrewMin || CrewCount > PlaneDesignerBalance.Airframe.CrewMax)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CrewCount),
                $"Crew count must be within [{PlaneDesignerBalance.Airframe.CrewMin}, {PlaneDesignerBalance.Airframe.CrewMax}].");
        }

        var clampedCowling = Numeric.Clamp(EngineCowling, 0.1d, 0.9d);
        if (!Numeric.AlmostEqual(clampedCowling, EngineCowling))
        {
            throw new ArgumentOutOfRangeException(nameof(EngineCowling), "Engine cowling slider must be within [0.1, 0.9].");
        }

        var clampedCleanliness = Numeric.Clamp(
            AerodynamicCleanlinessPercent,
            PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMinPercent,
            PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMaxPercent);
        if (!Numeric.AlmostEqual(clampedCleanliness, AerodynamicCleanlinessPercent))
        {
            throw new ArgumentOutOfRangeException(
                nameof(AerodynamicCleanlinessPercent),
                $"Aerodynamic cleanliness must be within [{PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMinPercent}, {PlaneDesignerBalance.Airframe.AerodynamicCleanlinessMaxPercent}] %.");
        }

        if (!Enum.IsDefined(typeof(RunwayType), RunwayType))
        {
            throw new ArgumentOutOfRangeException(nameof(RunwayType), "Unsupported runway type.");
        }
    }
}
