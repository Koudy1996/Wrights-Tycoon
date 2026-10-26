using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Factories;
using WrightTycoon.Game.Launch;
using WrightTycoon.Game.UI;

namespace WrightTycoon.Game.Tests;

[Category("Extended")]
public sealed class SliderAndMotorFactoryUiTests
{
    [Test]
    public void SliderThumbHasFiftyPercentVisualGrowthAndExtraTwentyPercentClickableHalo()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Requires graphics.");
        var window = ScriptableObject.CreateInstance<FactoryPlannerGuiTestWindow>();
        window.position = new Rect(10, 10, 700, 400);
        var value = .5f;
        var enabled = true;
        Rect thumb = default, hit = default;
        window.Draw = () =>
        {
            var rect = new Rect(50, 80, 400, 40);
            thumb = WrightTycoonSlider.ThumbRect(rect, value, 0, 1);
            hit = WrightTycoonSlider.HitRect(thumb);
            Assert.That(thumb.width, Is.EqualTo(Mathf.Max(10, GUI.skin.horizontalSliderThumb.fixedWidth) * 1.5f));
            Assert.That(hit.width, Is.EqualTo(thumb.width * 1.2f).Within(.001));
            Assert.That(hit.height, Is.EqualTo(thumb.height * 1.2f).Within(.001));
            GUI.enabled = enabled;
            try { value = WrightTycoonSlider.Horizontal(rect, value, 0, 1); }
            finally { GUI.enabled = true; }
        };
        try
        {
            window.ShowUtility();
            window.SendEvent(new Event { type = EventType.Layout });
            window.SendEvent(new Event { type = EventType.Repaint });
            Assert.That(window.Error, Is.Null);
            var haloPoint = new Vector2(thumb.center.x, (hit.y + thumb.y) * .5f);
            Assert.That(thumb.Contains(haloPoint), Is.False);
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = haloPoint });
            Assert.That(GUIUtility.hotControl, Is.Not.EqualTo(0), "The invisible halo must actually capture the pointer.");
            window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = new Vector2(400, 100) });
            window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(400, 100) });
            Assert.That(value, Is.GreaterThan(.8));
            Assert.That(GUIUtility.hotControl, Is.EqualTo(0));
            var before = value;
            enabled = false;
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(60, 100) });
            window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(60, 100) });
            Assert.That(value, Is.EqualTo(before));
            Assert.That(window.Error, Is.Null, window.Error?.ToString());
        }
        finally { window.Draw = null; window.Close(); UnityEngine.Object.DestroyImmediate(window); }
    }

    [Test]
    public void PerPlantProductionCountersSurviveSaveAndStockCompletionChangesPlannerStatus()
    {
        var network = new EngineManufacturingNetwork();
        network.TryConstruct(double.MaxValue, out _, out _);
        for (var m = 0; m < 18; m++) network.AdvanceMonth();
        var site = network.Sites[0];
        var item = new EngineManufacturingCatalogItem("motor", "group", 1000, 100);
        network.Configure(site.SiteId, item, FactoryAutomationTier.HandBuilt, 2, 3, double.MaxValue, out _, out _);
        for (var m = 0; m < FactoryBalance.GetInitialInstallationMonths(FactoryAutomationTier.HandBuilt); m++) network.AdvanceMonth();
        var catalog = new Dictionary<string, EngineManufacturingCatalogItem> { [item.EngineDesignId] = item };
        Assert.That(FactoryPlannerProjection.Build(null, network, enginePartsGroup: _ => "group").Single().Status, Is.EqualTo("Ready"));
        network.ProcessMonth(0, catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(site.LastProducedEngines, Is.EqualTo(2));
        Assert.That(site.LastProducedParts, Is.EqualTo(3));
        Assert.That(site.LifetimeProducedEngines, Is.EqualTo(2));
        var restored = new EngineManufacturingNetwork();
        restored.RestoreSnapshot(network.CreateSnapshot());
        Assert.That(restored.Sites[0].LifetimeProducedEngines, Is.EqualTo(2));
        Assert.That(restored.Sites[0].LifetimeProducedParts, Is.EqualTo(3));
        Assert.That(FactoryPlannerProjection.Build(null, restored, enginePartsGroup: _ => "group").Single().Status, Is.EqualTo("Idle"));
        restored.ProcessMonth(1, catalog, Array.Empty<EngineManufacturingDemand>());
        Assert.That(restored.Sites[0].LastProducedEngines, Is.Zero);
        Assert.That(restored.Sites[0].LifetimeProducedEngines, Is.EqualTo(2));
        restored.TryConsumeEngineUnits("motor", 1);
        Assert.That(FactoryPlannerProjection.Build(null, restored, enginePartsGroup: _ => "group").Single().Status, Is.EqualTo("Ready"));
        Assert.That(AccountingDefaults.StartingCash, Is.EqualTo(750_000_000));
    }
}
