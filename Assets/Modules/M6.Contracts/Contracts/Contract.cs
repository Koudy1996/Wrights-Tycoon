using System;

namespace WrightTycoon.Core.Contracts;

public sealed class Contract
{
    public Contract(
        string id,
        string companyName,
        int quantityUnits,
        int deadlineMonths,
        ContractRequirements requirements,
        int createdMonthIndex,
        double unitPrice = 0d,
        double estimatedMinimumUnitCost = 0d,
        double appliedMarkup = 0d)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        CompanyName = companyName ?? throw new ArgumentNullException(nameof(companyName));
        QuantityUnits = quantityUnits;
        DeadlineMonths = deadlineMonths;
        Requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        CreatedMonthIndex = createdMonthIndex;
        UnitPrice = unitPrice;
        EstimatedMinimumUnitCost = estimatedMinimumUnitCost;
        AppliedMarkup = appliedMarkup;
        Status = ContractStatus.Open;
        RemainingMonths = deadlineMonths;

        Validate();
    }

    public string Id { get; }

    public string CompanyName { get; }

    public int QuantityUnits { get; }

    public int DeadlineMonths { get; }

    public ContractRequirements Requirements { get; }

    public int CreatedMonthIndex { get; }

    public double UnitPrice { get; }

    public double TotalPrice => UnitPrice * QuantityUnits;

    public double EstimatedMinimumUnitCost { get; }

    public double AppliedMarkup { get; }

    public ContractStatus Status { get; private set; }

    public int? DeliveredMonthIndex { get; private set; }

    public int RemainingMonths { get; private set; }

    public int? CanceledMonthIndex { get; private set; }

    public string? CancellationReason { get; private set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Contract id cannot be empty.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            throw new ArgumentException("Company name cannot be empty.", nameof(CompanyName));
        }

        if (QuantityUnits < 5 || QuantityUnits > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(QuantityUnits), "Quantity must be within [5, 500] units.");
        }

        if (DeadlineMonths < 6 || DeadlineMonths > 36)
        {
            throw new ArgumentOutOfRangeException(nameof(DeadlineMonths), "Deadline must be within [6, 36] months.");
        }

        if (CreatedMonthIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CreatedMonthIndex), "Created month index must be greater than or equal to zero.");
        }

        if (UnitPrice < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(UnitPrice), "Unit price must be greater than or equal to zero.");
        }

        if (EstimatedMinimumUnitCost < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EstimatedMinimumUnitCost),
                "Estimated minimum unit cost must be greater than or equal to zero.");
        }

        if (AppliedMarkup < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(AppliedMarkup), "Applied markup must be greater than or equal to zero.");
        }

        Requirements.Validate();
    }

    public void MarkDelivered(int deliveredMonthIndex)
    {
        if (Status != ContractStatus.Open)
        {
            throw new InvalidOperationException($"Contract '{Id}' is already in status '{Status}'.");
        }

        if (deliveredMonthIndex < CreatedMonthIndex)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deliveredMonthIndex),
                "Delivered month index cannot be earlier than contract creation month.");
        }

        Status = ContractStatus.Delivered;
        DeliveredMonthIndex = deliveredMonthIndex;
    }

    public bool AdvanceMonth(int monthIndex)
    {
        if (Status != ContractStatus.Open)
        {
            return false;
        }

        if (monthIndex < CreatedMonthIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(monthIndex), "Month index cannot be before contract creation month.");
        }

        RemainingMonths--;
        if (RemainingMonths < 0)
        {
            MarkCanceled(monthIndex, "Deadline expired.");
            return true;
        }

        return false;
    }

    public void MarkCanceled(int canceledMonthIndex, string reason)
    {
        if (Status != ContractStatus.Open)
        {
            throw new InvalidOperationException($"Contract '{Id}' is already in status '{Status}'.");
        }

        if (canceledMonthIndex < CreatedMonthIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(canceledMonthIndex), "Canceled month index cannot be earlier than creation month.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Cancellation reason cannot be empty.", nameof(reason));
        }

        Status = ContractStatus.Canceled;
        CanceledMonthIndex = canceledMonthIndex;
        CancellationReason = reason.Trim();
    }
}
