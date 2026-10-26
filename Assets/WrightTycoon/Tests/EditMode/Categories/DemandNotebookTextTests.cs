using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Categories;

[TestFixture]
[Category("Extended")]
public sealed class DemandNotebookTextTests
{
    [TestCase(AircraftCategory.LightSportAndRecreational, "Ultralight construction")]
    [TestCase(AircraftCategory.MilitaryTactical, "Army-grade construction")]
    [TestCase(AircraftCategory.MilitaryStrategicSupport, "Army-grade construction")]
    [TestCase(AircraftCategory.Bomber, "Army-grade construction")]
    [TestCase(AircraftCategory.RegionalJet, "Civil or army-grade construction")]
    public void FormatDemandConstructionRequirement_UsesSpecificAllowedConstructionText(
        AircraftCategory category,
        string expected)
    {
        var runnerType = RequireRunnerType();
        var method = runnerType.GetMethod(
            "FormatDemandConstructionRequirement",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        Assert.That(method!.Invoke(null, new object[] { category }), Is.EqualTo(expected));
    }

    [Test]
    public void BuildDemandNotebookContent_OmitsCurrentAndForecastYearLines()
    {
        var runnerType = RequireRunnerType();
        var runner = FormatterServices.GetUninitializedObject(runnerType);
        var currentDateField = runnerType.GetField("_currentDate", BindingFlags.NonPublic | BindingFlags.Instance);
        var method = runnerType.GetMethod(
            "BuildDemandNotebookContent",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(currentDateField, Is.Not.Null);
        Assert.That(method, Is.Not.Null);

        currentDateField!.SetValue(runner, SimulationDate.WrightTycoonStart);
        var content = (string)method!.Invoke(runner, new object[] { AircraftCategory.RegionalJet })!;

        Assert.That(content, Does.Contain("Demand Type: Regional Jet"));
        Assert.That(content, Does.Not.Contain("Current Year:"));
        Assert.That(content, Does.Not.Contain("Forecast Year:"));
    }

    [Test]
    public void MilitarySupportRoleRequirement_ListsCombatAndCargoModules()
    {
        var description = AircraftCategoryRequirementBalance.GetRoleDescription(
            AircraftCategory.MilitaryStrategicSupport);

        Assert.That(description, Is.EqualTo("Combat equipment or cargo space"));
    }

    private static Type RequireRunnerType() =>
        Type.GetType("WrightTycoon.Game.Launch.WrightTycoonLaunchRunner, WrightTycoon.Game", throwOnError: true)!;
}
