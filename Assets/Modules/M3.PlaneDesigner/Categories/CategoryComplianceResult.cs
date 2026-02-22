using System.Collections.Generic;

namespace WrightTycoon.Core.Categories;

public sealed record CategoryComplianceResult(
    bool IsCompliant,
    IReadOnlyList<string> UnmetRequirements);
