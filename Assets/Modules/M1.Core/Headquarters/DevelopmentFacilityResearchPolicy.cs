using System;
using WrightTycoon.Core.Research;

namespace WrightTycoon.Core.Headquarters;

public static class DevelopmentFacilityResearchPolicy
{
    public static bool Supports(DevelopmentFacility facility, ResearchSubjectType subjectType) =>
        facility switch
        {
            DevelopmentFacility.WindTunnel =>
                subjectType == ResearchSubjectType.AircraftFamily
                || subjectType == ResearchSubjectType.AircraftType,
            DevelopmentFacility.EngineTestCell => subjectType == ResearchSubjectType.Engine,
            _ => false,
        };

    public static ResearchQuote Apply(
        DevelopmentFacility facility,
        ResearchSubjectType subjectType,
        DevelopmentFacilityEffects effects,
        ResearchQuote quote)
    {
        if (!Supports(facility, subjectType))
        {
            throw new InvalidOperationException("The selected development facility cannot process this research subject.");
        }

        if (effects.Level <= 0)
        {
            throw new InvalidOperationException("The selected development facility has not been constructed.");
        }

        var adjusted = new ResearchQuote(
            System.Math.Round(quote.ResearchCost * effects.ResearchCostMultiplier, 2, MidpointRounding.AwayFromZero),
            System.Math.Max(1, (int)System.Math.Round(
                quote.ResearchTimeMonths * effects.ResearchTimeMultiplier,
                MidpointRounding.AwayFromZero)),
            quote.ComplexityNormalized);
        adjusted.Validate();
        return adjusted;
    }
}
