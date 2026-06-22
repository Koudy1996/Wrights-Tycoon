using System;
using System.Collections.Generic;
using System.Linq;

namespace WrightTycoon.Core.Production;

public readonly record struct ProductionBreakdownItem(string Label, double Amount);

public sealed class ProductionBreakdown
{
    private readonly IReadOnlyList<ProductionBreakdownItem> _items;

    public ProductionBreakdown(IEnumerable<ProductionBreakdownItem> items)
    {
        _items = items?.Where(item => System.Math.Abs(item.Amount) > 0.0000001d).ToArray()
            ?? throw new ArgumentNullException(nameof(items));
        Total = _items.Sum(item => item.Amount);
    }

    public IReadOnlyList<ProductionBreakdownItem> Items => _items;

    public double Total { get; }

    public static ProductionBreakdown FromTotal(string label, double total) =>
        new(new[] { new ProductionBreakdownItem(label, total) });
}
