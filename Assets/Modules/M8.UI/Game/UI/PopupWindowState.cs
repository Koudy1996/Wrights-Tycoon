using System;

namespace WrightTycoon.Game.UI;

[Serializable]
public sealed class PopupWindowState
{
    public bool IsOpen { get; set; }

    public float PositionX { get; set; }

    public float PositionY { get; set; }

    public float SizeX { get; set; }

    public float SizeY { get; set; }
}
