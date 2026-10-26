using System;
using System.Reflection;
using NUnit.Framework;

namespace WrightTycoon.Core.Tests.Accounting;

[TestFixture]
[Category("Extended")]
public sealed class FinanceTimelineChartTests
{
    [TestCase(0d, "0")]
    [TestCase(950d, "950")]
    [TestCase(1_500d, "1 500")]
    [TestCase(500_000_000d, "500 000 000")]
    [TestCase(-500_000_000d, "-500 000 000")]
    [TestCase(1_250_000_000d, "1 250 000 000")]
    [TestCase(2_000_000_000_000d, "2 000 000 000 000")]
    public void FormatFinanceAxisValue_UsesFullGroupedValues(double value, string expected)
    {
        var runnerType = Type.GetType(
            "WrightTycoon.Game.Launch.WrightTycoonLaunchRunner, WrightTycoon.Game",
            throwOnError: true)!;
        var method = runnerType.GetMethod(
            "FormatFinanceAxisValue",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        Assert.That(method!.Invoke(null, new object[] { value }), Is.EqualTo(expected));
    }
}
