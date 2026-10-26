using System;
using NUnit.Framework;
using WrightTycoon.Core.Aircraft;
using WrightTycoon.Core.Airframes;
using WrightTycoon.Core.Engines;
using WrightTycoon.Core.Fleet;
using WrightTycoon.Core.Tests.Factories;
using AircraftModel = WrightTycoon.Core.Aircraft.Aircraft;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
public sealed class MachineSimilarityTests
{
    private static AircraftModel Aircraft() => AircraftProductionFactoryTests.CreateCatalogItem("similarity", 100, 2, 50).Aircraft;

    [Test]
    public void ModularWingSimilarityTracksAllSlidersSymmetricallyIncludingNegativeOrientation()
    {
        var source = Aircraft();
        AircraftModel WithWing(double speed, double orientation, double efficiency)
        {
            var parameters = source.Airframe.Parameters.Clone();
            parameters.UseModularWingProfile = true;
            parameters.WingDesignSpeedKph = speed;
            parameters.WingPayloadAgility = orientation;
            parameters.WingProfileEfficiency = efficiency;
            return new AircraftModel(source.FamilyDefinition, source.TypeDefinition, source.Engine,
                new Airframe(source.Airframe.Definition, parameters, source.Airframe.Performance),
                source.Performance, source.Development);
        }
        var first = WithWing(200, -100, 0);
        var second = WithWing(1000, 100, 100);
        var forward = MachineSimilarityCalculator.AircraftDifference(first, second);
        Assert.That(forward.DifferencePercent, Is.GreaterThan(0).And.LessThanOrEqualTo(2));
        Assert.That(forward.Breakdown.Count, Is.EqualTo(3));
        Assert.That(MachineSimilarityCalculator.AircraftDifference(second, first).DifferencePercent,
            Is.EqualTo(forward.DifferencePercent));
        Assert.That(MachineSimilarityCalculator.AircraftDifference(first, first).DifferencePercent, Is.Zero);
    }

    private static Engine Engine(double displacement = 12, int cylinders = 12, EngineType type = EngineType.Radial)
    {
        var source = Aircraft().Engine;
        var parameters = source.Parameters.Clone();
        parameters.DisplacementLiters = displacement;
        parameters.CylinderCount = cylinders;
        return new Engine(new EngineDefinition { Id = "catalog", DisplayName = "Engine", EngineType = type }, parameters, source.Performance);
    }

    [Test]
    public void EngineConstructionSimilarityIsSymmetricAndTenPercentBoundaryIsInclusive()
    {
        var first = Engine(12); var second = Engine(16);
        var forward = MachineSimilarityCalculator.EngineDifference(first, second);
        var reverse = MachineSimilarityCalculator.EngineDifference(second, first);
        Assert.That(MachineSimilarityCalculator.EngineDifference(first, first).DifferencePercent, Is.Zero);
        Assert.That(forward.DifferencePercent, Is.EqualTo(10).Within(1e-12));
        Assert.That(reverse.DifferencePercent, Is.EqualTo(forward.DifferencePercent));
        Assert.That(forward.CanShareParts, Is.True);
        Assert.That(MachineSimilarityCalculator.EngineDifference(first, Engine(16.01)).CanShareParts, Is.False);
        Assert.That(forward.Breakdown.Count, Is.EqualTo(1));
    }

    [Test]
    public void ArchitectureCylinderCountOrExplicitFamilyMismatchAlwaysPreventsSharing()
    {
        var baseline = Engine();
        Assert.That(MachineSimilarityCalculator.EngineDifference(baseline, Engine(type: EngineType.Inline)).CanShareParts, Is.False);
        Assert.That(MachineSimilarityCalculator.EngineDifference(baseline, Engine(cylinders: 14)).DifferencePercent, Is.GreaterThan(10));
        Assert.That(MachineSimilarityCalculator.EngineDifference(baseline, baseline, sameFamily: false).CanShareParts, Is.False);
    }

    [Test]
    public void AircraftOperatingEquipmentCombinedContributesAtMostThreePercent()
    {
        var baseline = Aircraft();
        var parameters = baseline.Airframe.Parameters.Clone();
        parameters.CrewCount = 10; parameters.HasBackupSystems = true;
        var equippedType = new AircraftTypeDefinition
        {
            Id = "equipped", FamilyId = baseline.FamilyDefinition.Id, DisplayName = "Equipped",
            NavigationEquipment = AircraftNavigationEquipment.AdvancedNavigationSuite,
            HasPassengerCabin = true, HasCargoSpace = true, HasSpecialEquipment = true,
            HasCombatEquipment = true, WeaponPayloadKilograms = 1000,
        };
        var equipped = new AircraftModel(baseline.FamilyDefinition, equippedType, baseline.Engine,
            new Airframe(baseline.Airframe.Definition, parameters, baseline.Airframe.Performance),
            baseline.Performance, baseline.Development);
        var difference = MachineSimilarityCalculator.AircraftDifference(baseline, equipped);
        Assert.That(difference.DifferencePercent, Is.InRange(0d, 3d));
        Assert.That(difference.CanShareParts, Is.True);
        Assert.That(MachineSimilarityCalculator.AircraftDifference(equipped, baseline).DifferencePercent,
            Is.EqualTo(difference.DifferencePercent));
    }

    [Test]
    public void AircraftGeometryDominatesAndDerivedPerformanceDoesNotAlterConstructionScore()
    {
        var baseline = Aircraft();
        var changedPerformance = baseline.Performance with { OverallReliability = .2, TotalProductionCost = 10000000 };
        var changed = new AircraftModel(baseline.FamilyDefinition, baseline.TypeDefinition, baseline.Engine,
            baseline.Airframe, changedPerformance, baseline.Development);
        Assert.That(MachineSimilarityCalculator.AircraftDifference(baseline, changed).DifferencePercent, Is.Zero);
        var parameters = baseline.Airframe.Parameters.Clone(); parameters.WingAreaSquareMeters *= 2;
        var geometry = new AircraftModel(baseline.FamilyDefinition, baseline.TypeDefinition, baseline.Engine,
            new Airframe(baseline.Airframe.Definition, parameters, baseline.Airframe.Performance), baseline.Performance, baseline.Development);
        Assert.That(MachineSimilarityCalculator.AircraftDifference(baseline, geometry).DifferencePercent, Is.EqualTo(15));
        Assert.That(MachineSimilarityCalculator.AircraftDifference(baseline, geometry).CanShareParts, Is.False);
        Assert.That(MachineSimilarityCalculator.AircraftDifference(baseline,
            AircraftProductionFactoryTests.CreateCatalogItem("other-family", 100, 2, 50).Aircraft).CanShareParts, Is.False);
        parameters = baseline.Airframe.Parameters.Clone(); parameters.EngineCount++;
        var architecture = new AircraftModel(baseline.FamilyDefinition, baseline.TypeDefinition, baseline.Engine,
            new Airframe(baseline.Airframe.Definition, parameters, baseline.Airframe.Performance), baseline.Performance, baseline.Development);
        Assert.That(MachineSimilarityCalculator.AircraftDifference(baseline, architecture).DifferencePercent, Is.GreaterThan(10));
    }

    [Test]
    public void FamilyPolicyMergesHistoricalSplitGroupsAndPreservesOriginalAnchorAndAliases()
    {
        var registry = new SparePartsGroupRegistry();
        registry.Resolve("original", "family", (_, _) => 100);
        registry.Resolve("different-version", "family", (_, _) => 100);
        registry.Resolve("unrelated", "other-family", (_, _) => 0);
        registry.MergeFamilyGroups();
        Assert.That(registry.ResolveFamily("radically-new-version", "family"), Is.EqualTo("original"));
        Assert.That(registry.GetGroupId("different-version"), Is.EqualTo("original"));
        Assert.That(registry.GetGroupId("unrelated"), Is.EqualTo("unrelated"));
        var restored = new SparePartsGroupRegistry();
        restored.RestoreSnapshot(registry.CreateSnapshot());
        restored.MergeFamilyGroups();
        Assert.That(restored.MemberIds("original"), Is.EqualTo(new[] { "original", "different-version", "radically-new-version" }));
        Assert.That(restored.CreateSnapshot().Groups.Length, Is.EqualTo(2));
    }

    [Test]
    public void RegistryChecksEveryMemberAndDoesNotAllowTransitiveDrift()
    {
        var registry = new SparePartsGroupRegistry();
        double Difference(string a, string b) => a == b ? 0 : a == "a" && b == "c" || a == "c" && b == "a" ? 18 : 9;
        Assert.That(registry.Resolve("a", "family", Difference), Is.EqualTo("a"));
        Assert.That(registry.Resolve("b", "family", Difference), Is.EqualTo("a"));
        Assert.That(registry.Resolve("c", "family", Difference), Is.EqualTo("c"));
        Assert.That(registry.MemberIds("a"), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(registry.GetGroupId("c"), Is.EqualTo("c"));
        Assert.That(registry.GetGroupId("unknown"), Is.Null);
        Assert.That(registry.MemberIds("unknown"), Is.Empty);
    }

    [Test]
    [Category("Extended")]
    public void RegistryRejectsIncompatibleNonAnchorMemberEvenWhenAnchorMatches()
    {
        var registry = new SparePartsGroupRegistry();
        double Difference(string a, string b) => a == b ? 0 : a == "b" && b == "c" || a == "c" && b == "b" ? 11 : 5;
        registry.Resolve("a", "family", Difference); registry.Resolve("b", "family", Difference);
        Assert.That(registry.Resolve("c", "family", Difference), Is.EqualTo("c"));
    }

    [Test]
    public void RegistryBoundaryFamilyAndSnapshotPreserveFirstRegistrationAnchor()
    {
        var registry = new SparePartsGroupRegistry();
        Assert.That(registry.Resolve("z", "family", (_, _) => 10), Is.EqualTo("z"));
        Assert.That(registry.Resolve("a", "family", (_, _) => 10), Is.EqualTo("z"));
        Assert.That(registry.Resolve("other", "different-family", (_, _) => 0), Is.EqualTo("other"));
        var snapshot = registry.CreateSnapshot();
        var restored = new SparePartsGroupRegistry(); restored.RestoreSnapshot(snapshot);
        snapshot.Groups[0].MemberIds[1] = "mutated";
        Assert.That(restored.GetGroupId("a"), Is.EqualTo("z"));
        Assert.That(restored.Resolve("next", "family", (_, _) => 10), Is.EqualTo("z"));
        Assert.That(restored.MemberIds("z"), Is.EqualTo(new[] { "z", "a", "next" }));
        Assert.Throws<ArgumentException>(() => restored.Resolve("a", "different-family", (_, _) => 0));
    }

    [Test]
    public void RegistryValidatesSnapshotsAndDifferenceWithoutMutatingExistingMembership()
    {
        var registry = new SparePartsGroupRegistry(); registry.Resolve("a", "family", (_, _) => 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.Resolve("b", "family", (_, _) => double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.Resolve("b", "family", (_, _) => 101));
        Assert.Throws<ArgumentException>(() => registry.RestoreSnapshot(new SparePartsGroupRegistrySnapshot(new[]
        {
            new SparePartsGroupSnapshot("a", "family", new[] { "a", "b" }),
            new SparePartsGroupSnapshot("c", "family", new[] { "c", "b" }),
        })));
        Assert.That(registry.MemberIds("a"), Is.EqualTo(new[] { "a" }));
    }
}
