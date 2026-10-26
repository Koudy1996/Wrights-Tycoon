using System;
using System.Reflection;
using System.Collections;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.Research;
using WrightTycoon.Core.Simulation;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

public sealed class TechnologyFreezeUiTests
{
    [Test]
    public void StandaloneFamilyRetainsReferenceAndRequotesForFutureBookingWithoutSavedTypes()
    {
        var fixtureType = typeof(TechnologyFreezeRuntimeTests).GetNestedType("Fixture", BindingFlags.NonPublic)!;
        var fixture = Activator.CreateInstance(fixtureType, true)!;
        var runner = (WrightTycoonLaunchRunner)fixtureType.GetField("Runner")!.GetValue(fixture)!;
        var initial = (WrightTycoon.Core.Aircraft.Aircraft)fixtureType.GetField("Initial")!.GetValue(fixture)!;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            ((IList)typeof(WrightTycoonLaunchRunner).GetField("_savedAircraftDesigns", flags)!.GetValue(runner)!).Clear();
            var familyType = typeof(WrightTycoonLaunchRunner).GetNestedType("SavedFamilyDesign", BindingFlags.NonPublic)!;
            var constructor = familyType.GetConstructors().Single();
            var args = constructor.GetParameters().Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue
                : parameter.ParameterType == typeof(string) ? "freeze.family"
                : parameter.ParameterType == typeof(double) ? (object)24d
                : parameter.ParameterType == typeof(int) ? 1 : Activator.CreateInstance(parameter.ParameterType)).ToArray();
            var saved = constructor.Invoke(args);
            familyType.GetMethod("SetResearchReference")!.Invoke(saved, new object[] { initial });
            ((IList)typeof(WrightTycoonLaunchRunner).GetField("_savedFamilies", flags)!.GetValue(runner)!).Add(saved);
            var method = typeof(WrightTycoonLaunchRunner).GetMethod("BuildStandaloneFamilyResearchAircraft", flags)!;
            var first = (WrightTycoon.Core.Aircraft.Aircraft)method.Invoke(runner, new[] { saved })!;
            Assert.That(first.FamilyDefinition.TechnologySnapshot!.Year, Is.EqualTo(1960));
            Assert.That(first.FamilyDefinition.TechnologySnapshot!.Month, Is.EqualTo(2));
            typeof(WrightTycoonLaunchRunner).GetField("_currentDate", flags)!.SetValue(runner, new SimulationDate(1970, 4));
            var future = (WrightTycoon.Core.Aircraft.Aircraft)method.Invoke(runner, new[] { saved })!;
            Assert.That(future.FamilyDefinition.TechnologySnapshot!.Year, Is.EqualTo(1970));
            Assert.That(future.TypeDefinition.TechnologySnapshot!.Month, Is.EqualTo(5));
            Assert.That(future.Engine.Performance.Cost, Is.EqualTo(first.Engine.Performance.Cost));
            Assert.That(future.FamilyDefinition.ResearchModifiers.ProductionOptimizationPoints, Is.Zero);
            var dtoType = typeof(WrightTycoonLaunchRunner).GetNestedType("SavedFamilyDesignState", BindingFlags.NonPublic)!;
            var dto = Activator.CreateInstance(dtoType, true)!;
            dtoType.GetProperty("ReferenceFamilyDefinition")!.SetValue(dto, initial.FamilyDefinition);
            dtoType.GetProperty("ReferenceTypeDefinition")!.SetValue(dto, initial.TypeDefinition);
            var restored = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(dto), dtoType)!;
            var restoredFamily = (AircraftFamilyDefinition)dtoType.GetProperty("ReferenceFamilyDefinition")!.GetValue(restored)!;
            Assert.That(restoredFamily.InstalledEngineTechnologyModifiers, Is.EqualTo(initial.FamilyDefinition.InstalledEngineTechnologyModifiers));
            Assert.That(((AircraftTypeDefinition)dtoType.GetProperty("ReferenceTypeDefinition")!.GetValue(restored)!).Id,
                Is.EqualTo(initial.TypeDefinition.Id));
            var board = new ResearchBoard();
            board.UpsertProposal(ResearchSubjectType.AircraftFamily, "freeze.family", "Family", new ResearchQuote(100, 1, .5));
            var quoteArgs = new object?[] { board.GetAllProjects().Single(), new TypeResearchModifierPoints(), null, null, null };
            Assert.That(typeof(WrightTycoonLaunchRunner).GetMethod("TryBuildResearchQuote", flags)!.Invoke(runner, quoteArgs), Is.True);
        }
        finally { ((IDisposable)fixture).Dispose(); }
    }

    [Test]
    public void ActualTypeApprovalFreezesBookedMonthAndDerivedVersionInheritsSnapshot()
    {
        var fixtureType = typeof(TechnologyFreezeRuntimeTests).GetNestedType("Fixture", BindingFlags.NonPublic)!;
        var fixture = Activator.CreateInstance(fixtureType, true)!;
        var runner = (WrightTycoonLaunchRunner)fixtureType.GetField("Runner")!.GetValue(fixture)!;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var source = fixtureType.GetProperty("Saved")!.GetValue(fixture)!;
            var catalog = (FactoryAircraftTypeCatalogItem)source.GetType().GetProperty("CatalogItem")!.GetValue(source)!;
            var savedType = source.GetType();
            var descendant = savedType.GetConstructors().Single().Invoke(new object?[]
            {
                "Derived freeze type", new FactoryAircraftTypeCatalogItem("freeze.derived", "Derived", catalog.Aircraft),
                "freeze.engine", "freeze.family", "freeze.derived", "", 1946, 1960, 1946, "freeze.type",
                1950, 1950, 3, false,
            });
            var list = (IList)typeof(WrightTycoonLaunchRunner).GetField("_savedAircraftDesigns", flags)!.GetValue(runner)!;
            list.Add(descendant);
            var board = new ResearchBoard();
            board.UpsertProposal(ResearchSubjectType.AircraftType, "freeze.type", "Freeze type", new ResearchQuote(100, 1, .5));
            var project = board.GetAllProjects().Single();
            var booked = new SimulationDate(1960, 1).NextMonth();
            typeof(WrightTycoonLaunchRunner).GetMethod("ApplyApprovedYearForApprovedProject", flags)!.Invoke(runner,
                new object[] { project, booked });
            var approved = (FactoryAircraftTypeCatalogItem)list[0]!.GetType().GetProperty("CatalogItem")!.GetValue(list[0])!;
            var derived = (FactoryAircraftTypeCatalogItem)list[1]!.GetType().GetProperty("CatalogItem")!.GetValue(list[1])!;
            Assert.That(approved.Aircraft.TypeDefinition.TechnologySnapshot!.Year, Is.EqualTo(1960));
            Assert.That(approved.Aircraft.TypeDefinition.TechnologySnapshot!.Month, Is.EqualTo(2));
            Assert.That(derived.Aircraft.TypeDefinition.TechnologySnapshot, Is.EqualTo(approved.Aircraft.TypeDefinition.TechnologySnapshot));
            Assert.That(derived.Aircraft.TypeDefinition.TechnologyModelVersion, Is.EqualTo(2));
            Assert.That(approved.Aircraft.FamilyDefinition.TechnologySnapshot!.Year, Is.EqualTo(1946));
        }
        finally { ((IDisposable)fixture).Dispose(); }
    }

    [Test]
    [Category("Extended")]
    public void FamilyApprovalFreezesBookedMonthAndUnknownLegacyDateIsNotInvented()
    {
        var owner = new GameObject("FamilyApprovalFreezeTest");
        owner.SetActive(false);
        var runner = owner.AddComponent<WrightTycoonLaunchRunner>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            typeof(WrightTycoonLaunchRunner).GetField("_currentDate", flags)!.SetValue(runner, new SimulationDate(1946, 1));
            var familyType = typeof(WrightTycoonLaunchRunner).GetNestedType("SavedFamilyDesign", BindingFlags.NonPublic)!;
            var constructor = familyType.GetConstructors().Single();
            var args = constructor.GetParameters().Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue
                : parameter.ParameterType == typeof(string) ? "freeze.family"
                : parameter.ParameterType == typeof(double) ? (object)24d
                : parameter.ParameterType == typeof(int) ? 1 : Activator.CreateInstance(parameter.ParameterType)).ToArray();
            var family = constructor.Invoke(args);
            var list = (IList)typeof(WrightTycoonLaunchRunner).GetField("_savedFamilies", flags)!.GetValue(runner)!;
            list.Add(family);
            var resolve = typeof(WrightTycoonLaunchRunner).GetMethod("ResolveFamilyTechnology", flags)!;
            Assert.That(resolve.Invoke(runner, new object[] { "freeze.family" }), Is.Null,
                "A saved family without a research-board approval date must stay legacy.");
            var booked = new SimulationDate(1946, 1).NextMonth();
            typeof(WrightTycoonLaunchRunner).GetMethod("FreezeFamilyTechnology", flags)!.Invoke(runner,
                new object[] { "freeze.family", booked });
            var snapshot = (AirframeTechnologySnapshot)familyType.GetProperty("TechnologySnapshot")!.GetValue(family)!;
            Assert.That(snapshot.Year, Is.EqualTo(1946));
            Assert.That(snapshot.Month, Is.EqualTo(2));
            Assert.That(snapshot.Origin, Is.EqualTo("approval"));
            typeof(WrightTycoonLaunchRunner).GetField("_currentDate", flags)!.SetValue(runner, new SimulationDate(2000, 1));
            Assert.That(resolve.Invoke(runner, new object[] { "freeze.family" }), Is.EqualTo(snapshot));
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [TestCase(14, Category = "Extended")]
    [TestCase(15, Category = "Extended")]
    [TestCase(16)]
    public void SaveVersionAcceptsExplicitMigrationAndCurrentVersion(int version)
    {
        var method = typeof(WrightTycoonLaunchRunner).GetMethod("ValidateSavePayloadVersion", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.DoesNotThrow(() => method.Invoke(null, new object[] { version }));
    }

    [TestCase(13)]
    [TestCase(17)]
    [Category("Extended")]
    public void UnsupportedSaveVersionsRemainRejected(int version)
    {
        var method = typeof(WrightTycoonLaunchRunner).GetMethod("ValidateSavePayloadVersion", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.That(Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { version }))!.InnerException,
            Is.InstanceOf<InvalidOperationException>());
    }

    [Test]
    [Category("Extended")]
    public void ResearchCopyPreservesFrozenTypeContext()
    {
        var snapshot = new AirframeTechnologySnapshot(1952, 3, 1.3, 1.2);
        var source = new AircraftTypeDefinition { TechnologySnapshot = snapshot, TechnologyModelVersion = 2 };
        var method = typeof(WrightTycoonLaunchRunner).GetMethod("WithTypeResearchModifiers", BindingFlags.Static | BindingFlags.NonPublic)!;
        var copy = (AircraftTypeDefinition)method.Invoke(null, new object[] { source, new TypeResearchModifierPoints { ReliabilityPoints = 10 } })!;
        Assert.That(copy.TechnologySnapshot, Is.EqualTo(snapshot));
        Assert.That(copy.TechnologyModelVersion, Is.EqualTo(2));
        Assert.That(copy.ResearchModifiers.ReliabilityPoints, Is.EqualTo(10));
    }

    [Test]
    public void FamilySaveDtoRoundTripsHistoricalValuesAndOrigin()
    {
        var dtoType = typeof(WrightTycoonLaunchRunner).GetNestedType("SavedFamilyDesignState", BindingFlags.NonPublic)!;
        var dto = Activator.CreateInstance(dtoType, true)!;
        var snapshot = new AirframeTechnologySnapshot(1946, 2, .95, .9, "research-board-migration");
        dtoType.GetProperty("TechnologySnapshot")!.SetValue(dto, snapshot);
        var restored = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(dto), dtoType)!;
        Assert.That(dtoType.GetProperty("TechnologySnapshot")!.GetValue(restored), Is.EqualTo(snapshot));
    }

    [TestCase(1280)]
    [TestCase(1920)]
    public void TechnologySummaryRendersThroughProductionTooltipOverlay(int width)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Technology summary IMGUI requires graphics.");
        // Reuse the independent runtime fixture, without touching saves or initializing the game scene.
        var fixtureType = typeof(TechnologyFreezeRuntimeTests).GetNestedType("Fixture", BindingFlags.NonPublic)!;
        var fixture = Activator.CreateInstance(fixtureType, true)!;
        var runner = (WrightTycoonLaunchRunner)fixtureType.GetField("Runner")!.GetValue(fixture)!;
        var aircraft = fixtureType.GetField("Initial")!.GetValue(fixture)!;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(WrightTycoonLaunchRunner).GetField("_aircraftPreview", flags)!.SetValue(runner, aircraft);
        var draw = typeof(WrightTycoonLaunchRunner).GetMethod("DrawTechnologyFreezeSummary", flags)!;
        var window = ScriptableObject.CreateInstance<FactoryPlannerGuiTestWindow>();
        window.position = new Rect(10, 10, width, 900);
        window.Draw = () => draw.Invoke(runner, Array.Empty<object>());
        try
        {
            window.ShowUtility();
            window.SendEvent(new Event { type = EventType.Layout });
            window.SendEvent(new Event { type = EventType.Repaint });
            Assert.That(window.Error, Is.Null, window.Error?.ToString());
            Assert.That(window.DrawCount, Is.GreaterThan(0));
        }
        finally
        {
            window.Draw = null;
            window.Close();
            ((IDisposable)fixture).Dispose();
        }
    }
}
