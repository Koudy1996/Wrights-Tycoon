using System;
using System.Collections.Generic;
using System.Linq;
using WrightTycoon.Core.Factories;

namespace WrightTycoon.Core.Contracts;

public sealed class ContractWarehouseDeliveryService
{
    private readonly ContractBoard _contractBoard;
    private readonly AircraftProductionFactory _productionFactory;

    public ContractWarehouseDeliveryService(
        ContractBoard contractBoard,
        AircraftProductionFactory productionFactory)
    {
        _contractBoard = contractBoard ?? throw new ArgumentNullException(nameof(contractBoard));
        _productionFactory = productionFactory ?? throw new ArgumentNullException(nameof(productionFactory));
    }

    public ContractDeliveryResult TryDeliverFromWarehouse(
        string contractId,
        string designId,
        IReadOnlyDictionary<string, FactoryAircraftTypeCatalogItem> catalog)
    {
        if (string.IsNullOrWhiteSpace(contractId))
        {
            throw new ArgumentException("Contract id cannot be empty.", nameof(contractId));
        }

        if (string.IsNullOrWhiteSpace(designId))
        {
            throw new ArgumentException("Design id cannot be empty.", nameof(designId));
        }

        if (catalog is null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        var contract = _contractBoard.OpenContracts.FirstOrDefault(item => string.Equals(item.Id, contractId, StringComparison.Ordinal));
        if (contract is null)
        {
            return new ContractDeliveryResult(
                contractId,
                false,
                new ContractEvaluationReport(Array.Empty<string>()),
                $"Contract '{contractId}' was not found in open contracts.");
        }

        if (!catalog.TryGetValue(designId, out var catalogItem))
        {
            return new ContractDeliveryResult(
                contractId,
                false,
                new ContractEvaluationReport(Array.Empty<string>()),
                $"Aircraft design '{designId}' is not available in catalog.");
        }

        var evaluation = _contractBoard.Evaluate(contract, catalogItem.Aircraft);
        if (!evaluation.CanDeliver)
        {
            return new ContractDeliveryResult(
                contractId,
                false,
                evaluation,
                $"Contract '{contractId}' is not deliverable with aircraft '{catalogItem.DisplayName}'.");
        }

        if (!_productionFactory.TryWithdrawFromWarehouse(designId, contract.QuantityUnits, out var withdrawalMessage))
        {
            return new ContractDeliveryResult(
                contractId,
                false,
                evaluation,
                $"Delivery failed. {withdrawalMessage}");
        }

        var delivery = _contractBoard.TryDeliver(contractId, catalogItem.Aircraft);
        if (!delivery.IsSuccess)
        {
            _productionFactory.AddWarehouseUnits(designId, contract.QuantityUnits);
            return delivery with { Message = $"{delivery.Message} Warehouse stock rollback applied." };
        }

        var remainingUnits = _productionFactory.GetWarehouseUnits(designId);
        return delivery with { Message = $"{delivery.Message} Source: {catalogItem.DisplayName}. Remaining stock: {remainingUnits}." };
    }
}
