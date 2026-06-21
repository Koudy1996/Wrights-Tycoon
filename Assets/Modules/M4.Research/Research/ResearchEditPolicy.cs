namespace WrightTycoon.Core.Research;

public static class ResearchEditPolicy
{
    public static bool CanEdit(ResearchStatus? status, bool hasApprovalMetadata = false) =>
        status.HasValue
            ? status.Value == ResearchStatus.Proposed
            : !hasApprovalMetadata;
}
