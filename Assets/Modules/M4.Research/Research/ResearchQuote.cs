using System;

namespace WrightTycoon.Core.Research;

public readonly record struct ResearchQuote(
    double ResearchCost,
    int ResearchTimeMonths,
    double ComplexityNormalized)
{
    public void Validate()
    {
        if (double.IsNaN(ResearchCost) || double.IsInfinity(ResearchCost) || ResearchCost < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(ResearchCost), "Research cost must be finite and >= 0.");
        }

        if (ResearchTimeMonths < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ResearchTimeMonths), "Research time must be >= 1 month.");
        }

        if (double.IsNaN(ComplexityNormalized)
            || double.IsInfinity(ComplexityNormalized)
            || ComplexityNormalized < 0d
            || ComplexityNormalized > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(ComplexityNormalized), "Complexity must be in [0, 1].");
        }
    }
}
