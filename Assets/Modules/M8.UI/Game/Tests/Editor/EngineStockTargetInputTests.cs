using System.Reflection;
using NUnit.Framework;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

[Category("Extended")]
public sealed class EngineStockTargetInputTests
{
    [TestCase("0", true, 0)]
    [TestCase("25000", true, 25000)]
    [TestCase("150 000", true, 150000)]
    [TestCase("1\u00a0000", true, 1000)]
    [TestCase("2147483647", true, int.MaxValue)]
    [TestCase("", false, 0)]
    [TestCase("-1", false, 0)]
    [TestCase("1.5", false, 0)]
    [TestCase("foo", false, 0)]
    [TestCase("2147483648", false, 0)]
    public void StockTargetsAcceptWholePlayerValuesWithoutOldUiCaps(string text, bool valid, int expected)
    {
        var method = typeof(WrightTycoonLaunchRunner).GetMethod("TryParseEngineStockTarget", BindingFlags.Static | BindingFlags.NonPublic)!;
        object[] arguments = { text, 0 };
        Assert.That(method.Invoke(null, arguments), Is.EqualTo(valid));
        if (valid) Assert.That(arguments[1], Is.EqualTo(expected));
    }
}
