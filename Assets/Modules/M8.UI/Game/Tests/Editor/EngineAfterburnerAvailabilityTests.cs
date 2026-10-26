using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

public sealed class EngineAfterburnerAvailabilityTests
{
    [TestCase(1949, 12, true)]
    [TestCase(1950, 1, false)]
    public void PreviewAndSaveRespectTheCalendarUnlock(int year, int month, bool locked)
    {
        var go = new GameObject("AfterburnerCalendarTest"); go.SetActive(false);
        try
        {
            var runner = go.AddComponent<WrightTycoonLaunchRunner>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var runnerType = typeof(WrightTycoonLaunchRunner);
            runnerType.GetField("_currentDate", flags)!.SetValue(runner, new SimulationDate(year, month));
            var parameters = (EngineParameters)runnerType.GetField("_engineParameters", flags)!.GetValue(runner)!;
            parameters.HasAfterburner = true;
            foreach (var method in new[] { "RebuildEnginePreview", "SaveCurrentEngine" })
            {
                runnerType.GetMethod(method, flags)!.Invoke(runner, null);
                var error = (string)runnerType.GetField("_enginePreviewError", flags)!.GetValue(runner)!;
                Assert.That(error.Contains("Afterburner is available from 1950"), Is.EqualTo(locked), method);
            }
        }
        finally { Object.DestroyImmediate(go); }
    }
}
