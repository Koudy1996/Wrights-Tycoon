using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Research;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

public sealed class MonthlyCompletionSummaryTests
{
    [Test]
    public void SameMonthEventsAreAggregatedAndCannotReopenAfterDismissal()
    {
        var summary = new MonthlyCompletionSummary();
        summary.Publish(10, new[] { "Engine completed", "Type completed", "Family completed", "Factory completed" });
        summary.Publish(10, new[] { "Engine completed" });
        Assert.That(summary.Entries.Count, Is.EqualTo(4));
        Assert.That(summary.HasPending, Is.True);
        summary.Dismiss();
        summary.Publish(10, new[] { "Another factory completed" });
        Assert.That(summary.HasPending, Is.False);
        Assert.That(summary.Entries.Count, Is.EqualTo(5));
        summary.Publish(11, Array.Empty<string>());
        Assert.That(summary.HasPending, Is.False);
        summary.Publish(12, new[] { "New engine" });
        Assert.That(summary.HasPending, Is.True);
        Assert.That(summary.Entries.Count, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PendingAndAcknowledgedStateSurviveActualGamePayload(bool dismiss)
    {
        var summary = new MonthlyCompletionSummary();
        summary.Publish(18, new[] { "Engine", "Aircraft factory" });
        if (dismiss) summary.Dismiss();
        var payloadType = typeof(WrightTycoonLaunchRunner).GetNestedType("RunnerGameSavePayload", BindingFlags.NonPublic)!;
        var payload = Activator.CreateInstance(payloadType, nonPublic: true)!;
        payloadType.GetProperty("MonthlyCompletionSummary")!.SetValue(payload, summary.CreateSnapshot());
        var loaded = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(payload), payloadType)!;
        var restored = new MonthlyCompletionSummary();
        restored.RestoreSnapshot((MonthlyCompletionSummarySnapshot)payloadType.GetProperty("MonthlyCompletionSummary")!.GetValue(loaded)!);
        Assert.That(restored.HasPending, Is.EqualTo(!dismiss));
        Assert.That(restored.Entries, Is.EqualTo(summary.Entries));
        restored.Publish(18, new[] { "Engine" });
        Assert.That(restored.HasPending, Is.EqualTo(!dismiss));
        restored.RestoreSnapshot(null);
        Assert.That(restored.HasPending, Is.False);
    }

    [Test]
    public void MonthAdvanceCollectsAllThreeResearchKindsAndBothFactoryKindsIntoOneNotification()
    {
        var go = new GameObject("MonthlyCompletionIntegration"); go.SetActive(false);
        try
        {
            var runner = go.AddComponent<WrightTycoonLaunchRunner>();
            var board = new ResearchBoard();
            foreach (var subject in new[] { ResearchSubjectType.Engine, ResearchSubjectType.AircraftFamily, ResearchSubjectType.AircraftType })
            {
                board.UpsertProposal(subject, subject.ToString(), subject.ToString(), new ResearchQuote(100, 1, .5));
                board.TryApproveProposal(subject, subject.ToString(), 20, out _, out _);
            }
            board.UpsertProposal(ResearchSubjectType.Engine, "another-engine", "Engine", new ResearchQuote(100, 1, .5));
            board.TryApproveProposal(ResearchSubjectType.Engine, "another-engine", 20, out _, out _);
            var aircraft = new AircraftProductionFactory();
            var engines = new EngineManufacturingNetwork();
            aircraft.TryStartFactoryConstruction(double.MaxValue, out _, out _);
            engines.TryConstruct(double.MaxValue, out _, out _);
            for (var i = 0; i < 17; i++) { aircraft.AdvanceMonth(); engines.AdvanceMonth(); }
            Set(runner, "_researchBoard", board); Set(runner, "_productionFactory", aircraft);
            Set(runner, "_engineManufacturing", engines); Set(runner, "_currentDate", new SimulationDate(1947, 8));
            Set(runner, "_elapsedMonths", 19);
            Invoke(runner, "AdvanceToNextMonth");
            var summary = (MonthlyCompletionSummary)Get(runner, "_monthlyCompletions")!;
            Assert.That(summary.HasPending, Is.True);
            Assert.That(summary.MonthIndex, Is.EqualTo(20));
            Assert.That(summary.Entries.Count, Is.EqualTo(6), "Distinct engines with the same display name must both appear.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void AircraftAndEngineConstructionFinishingTogetherProduceOneCombinedSummary()
    {
        var go = new GameObject("MonthlyCompletionTests"); go.SetActive(false);
        try
        {
            var runner = go.AddComponent<WrightTycoonLaunchRunner>();
            var aircraft = new AircraftProductionFactory();
            var engines = new EngineManufacturingNetwork();
            aircraft.TryStartFactoryConstruction(double.MaxValue, out _, out _);
            engines.TryConstruct(double.MaxValue, out _, out _);
            Set(runner, "_productionFactory", aircraft); Set(runner, "_engineManufacturing", engines);
            for (var i = 0; i < 17; i++) { aircraft.AdvanceMonth(); engines.AdvanceMonth(); }
            var before = Invoke(runner, "CaptureFactoryConstructionStates");
            aircraft.AdvanceMonth(); engines.AdvanceMonth();
            var events = new List<string>();
            Invoke(runner, "CollectCompletedFactoryConstruction", before!, events);
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0], Does.Contain("Aircraft factory completed"));
            Assert.That(events[1], Does.Contain("Engine factory completed"));
            before = Invoke(runner, "CaptureFactoryConstructionStates");
            events.Clear();
            Invoke(runner, "CollectCompletedFactoryConstruction", before!, events);
            Assert.That(events, Is.Empty, "Completed buildings must not be announced again.");
            var summary = (MonthlyCompletionSummary)Get(runner, "_monthlyCompletions")!;
            summary.Publish(18, new[] { "Completed" });
            Set(runner, "_elapsedMonths", 18);
            Invoke(runner, "HandleNextMonthButtonPressed");
            Assert.That(Get(runner, "_elapsedMonths"), Is.EqualTo(18), "Pending summary blocks another month.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    internal static object? Invoke(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    internal static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);
    internal static object? Get(object target, string field) => target.GetType()
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target);
}
