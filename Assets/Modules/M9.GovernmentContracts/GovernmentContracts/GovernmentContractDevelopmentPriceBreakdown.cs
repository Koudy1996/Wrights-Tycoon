using WrightTycoon.Core.Math;

namespace WrightTycoon.Core.GovernmentContracts;

public readonly record struct GovernmentContractDevelopmentPriceBreakdown(
    double EnginePrice,
    double FamilyPrice,
    double TypePrice)
{
    public double TotalPrice => EnginePrice + FamilyPrice + TypePrice;

    public static GovernmentContractDevelopmentPriceBreakdown FromTotal(double totalPrice) =>
        new(0d, 0d, System.Math.Max(0d, totalPrice));

    public void Validate()
    {
        Numeric.ThrowIfNotFinite(EnginePrice, nameof(EnginePrice));
        Numeric.ThrowIfNotFinite(FamilyPrice, nameof(FamilyPrice));
        Numeric.ThrowIfNotFinite(TypePrice, nameof(TypePrice));

        if (EnginePrice < 0d)
        {
            throw new System.ArgumentOutOfRangeException(nameof(EnginePrice), "Engine price must be >= 0.");
        }

        if (FamilyPrice < 0d)
        {
            throw new System.ArgumentOutOfRangeException(nameof(FamilyPrice), "Family price must be >= 0.");
        }

        if (TypePrice < 0d)
        {
            throw new System.ArgumentOutOfRangeException(nameof(TypePrice), "Type price must be >= 0.");
        }
    }
}
