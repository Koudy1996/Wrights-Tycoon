using System;
using WrightTycoon.Core.Campaign;

namespace WrightTycoon.Core.Airframes;

/// <summary>Immutable approval-time indices, deliberately independent of later calendar/balance changes.</summary>
public sealed record AirframeTechnologySnapshot(
    int Year, int Month, double AirframeEfficiencyMultiplier,
    double AerodynamicsEfficiencyMultiplier, string Origin = "approval", int ModelVersion = 2)
{
    public static AirframeTechnologySnapshot FromModifiers(int year, int month,
        HistoricalCampaignTechnologyModifiers modifiers, string origin = "approval")
    {
        var clean = HistoricalCampaignTechnologyModifiers.Sanitize(modifiers);
        var snapshot = new AirframeTechnologySnapshot(year, month, clean.AirframeEfficiencyMultiplier,
            clean.AerodynamicsEfficiencyMultiplier, origin);
        snapshot.Validate();
        return snapshot;
    }

    public void Validate()
    {
        if (Year < 1 || Year > 9999 || Month < 1 || Month > 12)
            throw new ArgumentOutOfRangeException(nameof(Year), "Technology snapshot requires a valid approval month.");
        if (ModelVersion != 2) throw new ArgumentOutOfRangeException(nameof(ModelVersion));
        var clean = new HistoricalCampaignTechnologyModifiers(1d, AirframeEfficiencyMultiplier, AerodynamicsEfficiencyMultiplier, 1d);
        if (clean.AirframeEfficiencyMultiplier != AirframeEfficiencyMultiplier
            || clean.AerodynamicsEfficiencyMultiplier != AerodynamicsEfficiencyMultiplier)
            throw new ArgumentOutOfRangeException(nameof(AirframeEfficiencyMultiplier), "Snapshot indices must be finite and within existing technology bounds.");
    }
}

public static class AirframeTechnologyBalance
{
    public const double FuselageMassFamilyShare = .70d;
    public const double WingMassFamilyShare = .60d;
    public const double SystemsMassFamilyShare = .20d;
    public const double BodyDragFamilyShare = .50d;
    public const double ProfileDragFamilyShare = .75d;
    public const double LiftFamilyShare = .60d;
    public const double MaterialsFamilyShare = .60d;
    public const double WorkFamilyShare = .50d;
}

public sealed record AirframeTechnologyEffects(
    double FuselageMass, double WingMass, double SystemsMass, double BodyDrag,
    double ProfileDrag, double Lift, double Materials, double Work)
{
    public void Validate()
    {
        foreach (var value in new[] { FuselageMass, WingMass, SystemsMass, BodyDrag, ProfileDrag, Lift, Materials, Work })
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
                throw new ArgumentOutOfRangeException(nameof(AirframeTechnologyEffects), "Technology effects must be finite positive indices.");
    }

    public static AirframeTechnologyEffects Resolve(AirframeTechnologySnapshot family, AirframeTechnologySnapshot type)
    {
        if (family is null) throw new ArgumentNullException(nameof(family));
        if (type is null) throw new ArgumentNullException(nameof(type));
        family.Validate();
        type.Validate();
        double Structural(double share) => share * family.AirframeEfficiencyMultiplier
            + (1d - share) * type.AirframeEfficiencyMultiplier;
        double Aero(double share) => share * family.AerodynamicsEfficiencyMultiplier
            + (1d - share) * type.AerodynamicsEfficiencyMultiplier;
        return new(Structural(AirframeTechnologyBalance.FuselageMassFamilyShare),
            Structural(AirframeTechnologyBalance.WingMassFamilyShare),
            Structural(AirframeTechnologyBalance.SystemsMassFamilyShare),
            Aero(AirframeTechnologyBalance.BodyDragFamilyShare),
            Aero(AirframeTechnologyBalance.ProfileDragFamilyShare), Aero(AirframeTechnologyBalance.LiftFamilyShare),
            Structural(AirframeTechnologyBalance.MaterialsFamilyShare), Structural(AirframeTechnologyBalance.WorkFamilyShare));
    }
}
