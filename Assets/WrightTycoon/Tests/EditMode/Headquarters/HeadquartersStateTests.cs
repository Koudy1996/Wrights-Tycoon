using NUnit.Framework;
using WrightTycoon.Core.Headquarters;
using WrightTycoon.Core.Research;

namespace WrightTycoon.Core.Tests.Headquarters;

[TestFixture]
public sealed class HeadquartersStateTests
{
    [Test]
    public void CompanyResearch_ReachesThresholdsAndAutoPausesAtMaximum()
    {
        var headquarters = new HeadquartersState();
        headquarters.SetResearchFundingLevel(CompanyResearchFundingLevel.Standard);

        for (var month = 0; month < 12; month++)
        {
            var update = headquarters.AdvanceMonth();
            Assert.That(update.OperatingCost, Is.EqualTo(1_250_000d));
        }

        Assert.That(headquarters.CompanyResearchPoints, Is.EqualTo(24));
        Assert.That(headquarters.CompanyQualityLevel, Is.EqualTo(1));

        headquarters.SetResearchFundingLevel(CompanyResearchFundingLevel.Intensive);
        for (var month = 0; month < 912; month++)
        {
            headquarters.AdvanceMonth();
        }

        Assert.That(headquarters.CompanyResearchPoints, Is.EqualTo(2_760));
        Assert.That(headquarters.CompanyQualityLevel, Is.EqualTo(20));
        Assert.That(headquarters.ResearchFundingLevel, Is.EqualTo(CompanyResearchFundingLevel.Paused));
    }

    [Test]
    public void CompanyResearch_ReachesFivePercentAfterTwiceThePreviousTime()
    {
        var headquarters = new HeadquartersState();
        headquarters.SetResearchFundingLevel(CompanyResearchFundingLevel.Standard);

        for (var month = 0; month < 119; month++)
        {
            headquarters.AdvanceMonth();
        }

        Assert.That(headquarters.CompanyQualityLevel, Is.EqualTo(4));
        headquarters.AdvanceMonth();
        Assert.That(headquarters.CompanyResearchPoints, Is.EqualTo(240));
        Assert.That(headquarters.CompanyQualityLevel, Is.EqualTo(5));
    }

    [Test]
    public void LegacyCompanyResearchPoints_PreserveTheEarnedBonusOnMigration()
    {
        Assert.That(HeadquartersBalance.MigrateLegacyCompanyResearchPoints(12), Is.EqualTo(24));
        Assert.That(HeadquartersBalance.MigrateLegacyCompanyResearchPoints(120), Is.EqualTo(240));
        Assert.That(
            HeadquartersBalance.GetCompanyQualityLevel(
                HeadquartersBalance.MigrateLegacyCompanyResearchPoints(120)),
            Is.EqualTo(5));
    }

    [Test]
    public void CompanyReliability_IsGlobalBonusWithHardCap()
    {
        Assert.That(HeadquartersBalance.ApplyCompanyReliabilityBonus(0.80d, 3), Is.EqualTo(0.83d).Within(1e-9));
        Assert.That(HeadquartersBalance.ApplyCompanyReliabilityBonus(0.70d, 20), Is.EqualTo(0.90d).Within(1e-9));
        Assert.That(HeadquartersBalance.ApplyCompanyReliabilityBonus(0.98d, 5), Is.EqualTo(0.995d).Within(1e-9));
    }

    [Test]
    public void CommercialDepartment_UsesLevelCadenceAndPersistsSequence()
    {
        var headquarters = CompleteUpgrade(HeadquartersFacility.CommercialDepartment, targetLevel: 1);

        for (var month = 0; month < 3; month++)
        {
            Assert.That(headquarters.AdvanceMonth().GeneratedSalesContracts, Is.Zero);
        }

        Assert.That(headquarters.AdvanceMonth().GeneratedSalesContracts, Is.EqualTo(1));
        Assert.That(headquarters.ConsumeNextCommercialGeneratedSequence(), Is.EqualTo(0));

        var restored = new HeadquartersState();
        restored.RestoreSnapshot(headquarters.CreateSnapshot());
        Assert.That(restored.CommercialDepartmentLevel, Is.EqualTo(1));
        Assert.That(restored.ConsumeNextCommercialGeneratedSequence(), Is.EqualTo(1));
    }

    [Test]
    public void EngineTestCell_HasSameResearchEffectsAtHalfFacilityCost()
    {
        var windUpgrade = HeadquartersBalance.GetNextUpgrade(HeadquartersFacility.WindTunnel, 0);
        var cellUpgrade = HeadquartersBalance.GetNextUpgrade(HeadquartersFacility.EngineTestCell, 0);
        var wind = HeadquartersBalance.GetDevelopmentFacilityEffects(DevelopmentFacility.WindTunnel, 2);
        var cell = HeadquartersBalance.GetDevelopmentFacilityEffects(DevelopmentFacility.EngineTestCell, 2);

        Assert.That(cellUpgrade.CapitalCost, Is.EqualTo(windUpgrade.CapitalCost * 0.5d));
        Assert.That(cell.ResearchCostMultiplier, Is.EqualTo(wind.ResearchCostMultiplier));
        Assert.That(cell.ResearchTimeMultiplier, Is.EqualTo(wind.ResearchTimeMultiplier));
        Assert.That(cell.ProjectSlots, Is.EqualTo(wind.ProjectSlots));
        Assert.That(cell.MonthlyOperatingCost, Is.EqualTo(wind.MonthlyOperatingCost * 0.5d));
    }

    [Test]
    public void DevelopmentFacilities_AreStrictlyScopedAndApplyFrozenQuoteMultipliers()
    {
        var quote = new ResearchQuote(10_000_000d, 50, 0.5d);
        var wind = HeadquartersBalance.GetDevelopmentFacilityEffects(DevelopmentFacility.WindTunnel, 1);
        var cell = HeadquartersBalance.GetDevelopmentFacilityEffects(DevelopmentFacility.EngineTestCell, 1);

        var assistedFamily = DevelopmentFacilityResearchPolicy.Apply(
            DevelopmentFacility.WindTunnel,
            ResearchSubjectType.AircraftFamily,
            wind,
            quote);
        var assistedEngine = DevelopmentFacilityResearchPolicy.Apply(
            DevelopmentFacility.EngineTestCell,
            ResearchSubjectType.Engine,
            cell,
            quote);

        Assert.That(assistedFamily.ResearchCost, Is.EqualTo(9_200_000d));
        Assert.That(assistedFamily.ResearchTimeMonths, Is.EqualTo(44));
        Assert.That(assistedEngine, Is.EqualTo(assistedFamily));
        Assert.That(
            () => DevelopmentFacilityResearchPolicy.Apply(
                DevelopmentFacility.WindTunnel,
                ResearchSubjectType.Engine,
                wind,
                quote),
            Throws.InvalidOperationException);
        Assert.That(
            () => DevelopmentFacilityResearchPolicy.Apply(
                DevelopmentFacility.EngineTestCell,
                ResearchSubjectType.AircraftType,
                cell,
                quote),
            Throws.InvalidOperationException);
    }

    [Test]
    public void FacilityConstruction_TakesExactMonthsAndAllowsOnlyOneProject()
    {
        var headquarters = new HeadquartersState();
        Assert.That(
            headquarters.TryStartNextUpgrade(
                HeadquartersFacility.WindTunnel,
                500_000_000d,
                out var upgrade,
                out _),
            Is.True);
        Assert.That(
            headquarters.TryStartNextUpgrade(
                HeadquartersFacility.EngineTestCell,
                500_000_000d,
                out _,
                out _),
            Is.False);

        for (var month = 1; month < upgrade.ConstructionMonths; month++)
        {
            headquarters.AdvanceMonth();
            Assert.That(headquarters.WindTunnelLevel, Is.Zero);
        }

        var completion = headquarters.AdvanceMonth();
        Assert.That(headquarters.WindTunnelLevel, Is.EqualTo(1));
        Assert.That(completion.CompletedFacility, Is.EqualTo(HeadquartersFacility.WindTunnel));
        Assert.That(completion.OperatingCost, Is.Zero);
        Assert.That(headquarters.AdvanceMonth().OperatingCost, Is.EqualTo(150_000d));
    }

    [Test]
    public void DevelopmentFacility_SlotsPersistAndReleaseWhenResearchCompletes()
    {
        var headquarters = CompleteUpgrade(HeadquartersFacility.EngineTestCell, targetLevel: 1);
        Assert.That(headquarters.TryAssignDevelopmentFacility(DevelopmentFacility.EngineTestCell, "research.engine", out _), Is.True);
        Assert.That(headquarters.TryAssignDevelopmentFacility(DevelopmentFacility.EngineTestCell, "research.other", out _), Is.False);

        var restored = new HeadquartersState();
        restored.RestoreSnapshot(headquarters.CreateSnapshot());
        Assert.That(restored.CanAssignDevelopmentFacility(DevelopmentFacility.EngineTestCell), Is.False);

        restored.AdvanceMonth(new[] { "research.engine" });
        Assert.That(restored.CanAssignDevelopmentFacility(DevelopmentFacility.EngineTestCell), Is.True);
    }

    private static HeadquartersState CompleteUpgrade(HeadquartersFacility facility, int targetLevel)
    {
        var headquarters = new HeadquartersState();
        for (var level = 1; level <= targetLevel; level++)
        {
            Assert.That(headquarters.TryStartNextUpgrade(facility, 500_000_000d, out var upgrade, out _), Is.True);
            for (var month = 0; month < upgrade.ConstructionMonths; month++)
            {
                headquarters.AdvanceMonth();
            }
        }

        return headquarters;
    }
}
