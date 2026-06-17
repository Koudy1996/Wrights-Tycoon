using System;
using System.Reflection;
using NUnit.Framework;

namespace WrightTycoon.Core.Tests.GovernmentContracts;

[TestFixture]
public sealed class GovernmentContractNotebookWindowTests
{
    [Test]
    public void CalculateResizedRect_UsesMouseDragDelta()
    {
        var resized = InvokeCalculateResizedRect(520f, 380f, 500f, 360f, 620f, 430f);

        Assert.That(ReadFloatProperty(resized, "width"), Is.EqualTo(640f));
        Assert.That(ReadFloatProperty(resized, "height"), Is.EqualTo(450f));
    }

    [Test]
    public void CalculateResizedRect_ClampsToMinimumAndMaximumSize()
    {
        var minimum = InvokeCalculateResizedRect(520f, 380f, 500f, 360f, -500f, -360f);
        var maximum = InvokeCalculateResizedRect(520f, 380f, 500f, 360f, 3000f, 3000f);

        Assert.That(ReadFloatProperty(minimum, "width"), Is.EqualTo(400f));
        Assert.That(ReadFloatProperty(minimum, "height"), Is.EqualTo(240f));
        Assert.That(ReadFloatProperty(maximum, "width"), Is.EqualTo(1800f));
        Assert.That(ReadFloatProperty(maximum, "height"), Is.EqualTo(1100f));
    }

    private static object InvokeCalculateResizedRect(
        float startWidth,
        float startHeight,
        float startMouseX,
        float startMouseY,
        float currentMouseX,
        float currentMouseY)
    {
        var rectType = RequireType("UnityEngine.Rect, UnityEngine.CoreModule");
        var vector2Type = RequireType("UnityEngine.Vector2, UnityEngine.CoreModule");
        var windowType = RequireType("WrightTycoon.Game.GovernmentContracts.GovernmentContractNotebookWindow, WrightTycoon.Game");
        var method = windowType.GetMethod("CalculateResizedRect", BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        return method!.Invoke(null, new[]
        {
            Activator.CreateInstance(rectType, 120f, 80f, startWidth, startHeight),
            Activator.CreateInstance(vector2Type, startMouseX, startMouseY),
            Activator.CreateInstance(vector2Type, startWidth, startHeight),
            Activator.CreateInstance(vector2Type, currentMouseX, currentMouseY),
            Activator.CreateInstance(vector2Type, 400f, 240f),
            Activator.CreateInstance(vector2Type, 1800f, 1100f),
        })!;
    }

    private static Type RequireType(string typeName) =>
        Type.GetType(typeName, throwOnError: true)!;

    private static float ReadFloatProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null);
        return (float)property!.GetValue(instance)!;
    }
}
