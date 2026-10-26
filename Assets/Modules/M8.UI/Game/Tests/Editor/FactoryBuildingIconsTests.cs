using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using WrightTycoon.Core.Balance;
using WrightTycoon.Game.Launch;

namespace WrightTycoon.Game.Tests;

[Category("Extended")]
public sealed class FactoryBuildingIconsTests
{
    [TestCase(FactoryBuildingSize.Small, "small")]
    [TestCase(FactoryBuildingSize.Medium, "medium")]
    [TestCase(FactoryBuildingSize.Large, "large")]
    public void SuppliedFactoryArtworkLoadsWithTransparentUiImport(FactoryBuildingSize size, string name)
    {
        var key = FactoryBuildingIcons.ResourceKey(size);
        Assert.That(key, Is.EqualTo("WT04/FactoryBuildings/" + name));
        var texture = Resources.Load<Texture2D>(key);
        Assert.That(texture, Is.Not.Null);
        Assert.That(texture.width, Is.GreaterThan(500));
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
        Assert.That(importer.alphaIsTransparency, Is.True);
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
    }
}
