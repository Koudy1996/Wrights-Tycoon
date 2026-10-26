using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WrightTycoon.Core.Production;

namespace WrightTycoon.Core.Tests.Aircraft;

[TestFixture]
[Category("Extended")]
public sealed class DesignerTooltipTests
{
    private static readonly Type RunnerType = Type.GetType(
        "WrightTycoon.Game.Launch.WrightTycoonLaunchRunner, WrightTycoon.Game",
        throwOnError: true)!;

    [TestCase(0, 0, true)]
    [TestCase(0, 1, false)]
    [TestCase(1, 0, false)]
    [TestCase(1, 1, true)]
    public void AirframeStageHighlight_TracksTheActiveCard(int activeStage, int candidateStage, bool expected)
    {
        var method = RunnerType.GetMethod(
            "IsAirframeDesignerStageActive",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        Assert.That(method!.Invoke(null, new object[] { activeStage, candidateStage }), Is.EqualTo(expected));
    }

    [Test]
    public void ProductionBreakdownTooltip_ReconcilesPostDevelopmentAdjustmentToDisplayedTotal()
    {
        var breakdown = new ProductionBreakdown(new[]
        {
            new ProductionBreakdownItem("Airframe", 1_000d),
            new ProductionBreakdownItem("Engines", 500d),
        });
        var method = RunnerType.GetMethod(
            "BuildProductionBreakdownTooltip",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[]
            {
                typeof(string),
                typeof(string),
                typeof(ProductionBreakdown),
                typeof(double),
                typeof(string),
                typeof(string),
            },
            modifiers: null);

        Assert.That(method, Is.Not.Null);
        var tooltip = method!.Invoke(
            null,
            new object[] { "test.parts", "Parts", breakdown, 1_350d, "0", "Type production optimization" })!;
        var lines = ReadNeutralLines(tooltip);

        Assert.That(lines, Has.Some.EqualTo("Type production optimization: -150"));
        Assert.That(lines, Has.Some.EqualTo("Total: 1 350"));
    }

    [Test]
    public void CompleteCostTooltip_ContainsPartsLaborAndExactTotal()
    {
        var breakdown = new ProductionBreakdown(new[]
        {
            new ProductionBreakdownItem("Airframe", 1_000d),
            new ProductionBreakdownItem("Engines", 500d),
        });
        var method = RunnerType.GetMethod(
            "BuildCompleteProductionCostTooltip",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[]
            {
                typeof(string),
                typeof(string),
                typeof(ProductionBreakdown),
                typeof(double),
                typeof(double),
                typeof(double),
                typeof(string),
            },
            modifiers: null);

        Assert.That(method, Is.Not.Null);
        var tooltip = method!.Invoke(
            null,
            new object[] { "test.complete", "Complete", breakdown, 1_500d, 10d, 2_300d, "Adjustment" })!;
        var lines = ReadNeutralLines(tooltip);

        Assert.That(lines, Has.Some.EqualTo("Parts subtotal: 1 500"));
        Assert.That(lines.Any(line => line.Contains("Labor: 10,0 MD x 80 = 800", StringComparison.Ordinal)), Is.True);
        Assert.That(lines, Has.Some.EqualTo("Complete cost: 2 300"));
    }

    private static string[] ReadNeutralLines(object tooltip)
    {
        var property = tooltip.GetType().GetProperty("NeutralLines", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null);
        var values = (IEnumerable)property!.GetValue(tooltip)!;
        return values.Cast<object>().Select(value => value.ToString() ?? string.Empty).ToArray();
    }
}
