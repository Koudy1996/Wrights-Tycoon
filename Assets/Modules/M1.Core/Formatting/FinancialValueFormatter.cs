using System;
using System.Globalization;

namespace WrightTycoon.Core.Presentation;

public static class FinancialValueFormatter
{
    private static readonly CultureInfo NumberCulture = CreateNumberCulture();

    public static string FormatNumber(double value, string format = "0")
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString(BuildGroupedNumericFormat(format), NumberCulture);
    }

    public static string FormatCurrency(double value)
    {
        var rounded = System.Math.Round(value, 0, MidpointRounding.AwayFromZero);
        return "$" + FormatNumber(rounded);
    }

    public static string FormatInput(double value) => FormatNumber(value, "0.##");

    private static CultureInfo CreateNumberCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = " ";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSizes = new[] { 3 };
        return culture;
    }

    private static string BuildGroupedNumericFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return "#,##0.###";
        }

        if (format.StartsWith("N", StringComparison.OrdinalIgnoreCase) || format.Contains(','))
        {
            return format;
        }

        return $"#,##{format}";
    }
}
