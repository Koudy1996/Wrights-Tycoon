using UnityEngine;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Balance;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private void DrawLandingGearDetails(LandingGearReport? gear, LandingGearType type)
    {
        if (gear is null) return;
        var (partsRate, workRate) = type switch
        {
            LandingGearType.SemiRetractable => (ProductionEconomyBalance.Airframe.SemiRetractableGearPartsRate,
                ProductionEconomyBalance.Airframe.SemiRetractableGearManDaysRate),
            LandingGearType.Retractable => (ProductionEconomyBalance.Airframe.RetractableGearPartsRate,
                ProductionEconomyBalance.Airframe.RetractableGearManDaysRate),
            _ => (0d, 0d),
        };
        DrawMetric("Landing Gear Weight (kg)", gear.MassKilograms, "0.0");
        GUILayout.Label($"Compared with fixed: +{FormatUiNumber(gear.ExtraMassOverFixedKilograms, "0.0")} kg");
        GUILayout.Label($"Gear drag area: {FormatUiNumber(gear.CruiseDragAreaSquareMeters, "0.000")} m² cruise / {FormatUiNumber(gear.ExtendedDragAreaSquareMeters, "0.000")} m² extended");
        GUILayout.Label($"Gear surcharge: materials +{FormatUiNumber(100d * partsRate, "0")}%; work +{FormatUiNumber(100d * workRate, "0")}%, relative to the airframe addon base. Fixed gear is included in base pricing.");
    }
}
