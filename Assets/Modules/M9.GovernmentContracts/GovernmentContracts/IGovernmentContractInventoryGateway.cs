namespace WrightTycoon.Core.GovernmentContracts;

public interface IGovernmentContractInventoryGateway
{
    int GetAvailableUnits(string designId);

    bool TryConsumeUnits(string designId, int units, out string message);
}
