using System;
using UnityEngine;

namespace WrightTycoon.Game.GovernmentContracts;

[Serializable]
public sealed class ContractUIState
{
    public bool IsOpen;
    public Vector2 Position;
    public Vector2 Size;
}
