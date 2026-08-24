namespace Mart.Customer.Domain.Inventory;

public sealed class StockAdjustment
{
    private StockAdjustment()
    {
    }

    public long StockAdjustmentId { get; private set; }

    public long FranchiseId { get; private set; }

    public long MartStoreId { get; private set; }

    public long ProductId { get; private set; }

    public string AdjustmentType { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public long? ApprovedBy { get; private set; }

    public DateTime? ApprovedOn { get; private set; }

    public long? CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public static StockAdjustment CreateAdd(
        long franchiseId,
        long martStoreId,
        long productId,
        decimal quantity,
        string reason,
        long createdBy,
        DateTime createdOn)
    {
        return new StockAdjustment
        {
            FranchiseId = franchiseId,
            MartStoreId = martStoreId,
            ProductId = productId,
            AdjustmentType = "ADJUSTMENT_ADD",
            Quantity = quantity,
            Reason = reason,
            CreatedBy = createdBy,
            CreatedOn = createdOn
        };
    }

    public static StockAdjustment CreateRemoval(
        long franchiseId,
        long martStoreId,
        long productId,
        string adjustmentType,
        decimal quantity,
        string reason,
        long createdBy,
        DateTime createdOn)
    {
        return new StockAdjustment
        {
            FranchiseId = franchiseId,
            MartStoreId = martStoreId,
            ProductId = productId,
            AdjustmentType = adjustmentType,
            Quantity = quantity,
            Reason = reason,
            CreatedBy = createdBy,
            CreatedOn = createdOn
        };
    }
}
