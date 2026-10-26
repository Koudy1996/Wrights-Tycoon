using System;
using WrightTycoon.Core.Balance;

namespace WrightTycoon.Game.Launch;

public static class FactoryBuildingIcons
{
    public static string ResourceKey(FactoryBuildingSize size) => "WT04/FactoryBuildings/" + (size switch
    {
        FactoryBuildingSize.Small => "small",
        FactoryBuildingSize.Medium => "medium",
        FactoryBuildingSize.Large => "large",
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    });
}
