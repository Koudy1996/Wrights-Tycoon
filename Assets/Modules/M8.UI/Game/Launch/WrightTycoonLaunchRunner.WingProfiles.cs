using System;
using UnityEngine;
using WrightTycoon.Core.Airframes;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private static void ValidateSavePayloadVersion(int version)
    {
        if (version != SavePayloadVersion && version != 15 && version != 14)
            throw new InvalidOperationException("This save is incompatible with the modular wing-profile update. Start a new game.");
    }

    private static double SnapWingProfileValue(double value, double min, double max, double step)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return min;
        return Math.Clamp(min + Math.Round((Math.Clamp(value, min, max) - min) / step,
            MidpointRounding.AwayFromZero) * step, min, max);
    }

    private static string GetWingProfileDisplayName(AirframeParameters parameters) =>
        parameters.UseModularWingProfile
            ? WingProfileCalculator.Calculate(parameters).Name
            : HumanizeUiToken(parameters.WingProfile.ToString());

    private void DrawModularWingProfileControls()
    {
        _baseAirframeParameters.UseModularWingProfile = true;
        _baseAirframeParameters.WingDesignSpeedKph = DrawSlider(
            "Wing Design Speed at 0 m (km/h)", _baseAirframeParameters.WingDesignSpeedKph,
            100d, 2000d, "0", useLinearScale: true, step: 50d);
        _baseAirframeParameters.WingPayloadAgility = DrawSlider(
            "Payload (-100) / Agility (+100)", _baseAirframeParameters.WingPayloadAgility,
            -100d, 100d, "0", useLinearScale: true, step: 5d);
        _baseAirframeParameters.WingProfileEfficiency = DrawSlider(
            "Profile Efficiency (higher cost / better performance)", _baseAirframeParameters.WingProfileEfficiency,
            0d, 100d, "0", useLinearScale: true, step: 5d);

        var profile = WingProfileCalculator.Calculate(_baseAirframeParameters);
        GUILayout.Label($"Wing Profile: {profile.Name}", GUI.skin.box);
        GUILayout.Label("Modified game profile; designation identifies its real-world reference, not exact geometry.");
        GUILayout.Label($"CLmax {FormatUiNumber(profile.MaximumLiftCoefficient, "0.00")} | " +
            $"Profile Cd {FormatUiNumber(profile.ProfileDragCoefficient, "0.0000")} | " +
            $"Design load {FormatUiNumber(profile.DesignLoadFactor, "0.0")} g");
        GUILayout.Label($"Wing only — mass x{FormatUiNumber(profile.WingMassMultiplier, "0.00")}; " +
            $"materials x{FormatUiNumber(profile.WingCostMultiplier, "0.00")}; " +
            $"work x{FormatUiNumber(profile.WingWorkMultiplier, "0.00")}");
        GUILayout.Label("Design speed is a sea-level reference, not a speed limit. Flying faster increases wing-profile drag.");
    }
}
