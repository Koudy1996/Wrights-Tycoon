using NUnit.Framework;
using WrightTycoon.Core.Presentation;

namespace WrightTycoon.Core.Tests.Accounting;

[TestFixture]
public sealed class FinancialValueFormatterTests
{
    [TestCase(0d, "0")]
    [TestCase(1_000d, "1 000")]
    [TestCase(-500_000_000d, "-500 000 000")]
    [TestCase(1_000_000_000_000d, "1 000 000 000 000")]
    public void FormatNumber_GroupsEveryThreeDigitsWithSpaces(double value, string expected)
    {
        Assert.That(FinancialValueFormatter.FormatNumber(value), Is.EqualTo(expected));
    }

    [Test]
    public void FormatInput_PreservesRelevantCentsAndUsesCommaDecimalSeparator()
    {
        Assert.That(FinancialValueFormatter.FormatInput(1_234_567.89d), Is.EqualTo("1 234 567,89"));
        Assert.That(FinancialValueFormatter.FormatInput(1_234_567d), Is.EqualTo("1 234 567"));
    }

    [Test]
    public void FormatCurrency_UsesTheSameGroupedNumberFormat()
    {
        Assert.That(FinancialValueFormatter.FormatCurrency(1_000_000_000_000d), Is.EqualTo("$1 000 000 000 000"));
    }
}
