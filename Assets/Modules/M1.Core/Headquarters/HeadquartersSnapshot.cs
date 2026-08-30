using System.Collections.Generic;

namespace WrightTycoon.Core.Headquarters;

public sealed class HeadquartersSnapshot
{
    public CompanyResearchFundingLevel ResearchFundingLevel { get; set; }

    public int CompanyResearchPoints { get; set; }

    public int CommercialDepartmentLevel { get; set; }

    public double CommercialSalesProgress { get; set; }

    public int CommercialGeneratedSequence { get; set; }

    public int WindTunnelLevel { get; set; }

    public int EngineTestCellLevel { get; set; }

    public HeadquartersConstructionSnapshot? ActiveConstruction { get; set; }

    public List<string> WindTunnelResearchIds { get; set; } = new();

    public List<string> EngineTestCellResearchIds { get; set; } = new();
}

public sealed class HeadquartersConstructionSnapshot
{
    public HeadquartersFacility Facility { get; set; }

    public int TargetLevel { get; set; }

    public int RemainingMonths { get; set; }
}
