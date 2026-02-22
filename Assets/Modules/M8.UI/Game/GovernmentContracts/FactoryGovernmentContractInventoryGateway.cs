using System;
using WrightTycoon.Core.Factories;
using WrightTycoon.Core.GovernmentContracts;

namespace WrightTycoon.Game.GovernmentContracts;

public sealed class FactoryGovernmentContractInventoryGateway : IGovernmentContractInventoryGateway
{
    private readonly AircraftProductionFactory _factory;

    public FactoryGovernmentContractInventoryGateway(AircraftProductionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public int GetAvailableUnits(string designId) => _factory.GetWarehouseUnits(designId);

    public bool TryConsumeUnits(string designId, int units, out string message) =>
        _factory.TryWithdrawFromWarehouse(designId, units, out message);
}
