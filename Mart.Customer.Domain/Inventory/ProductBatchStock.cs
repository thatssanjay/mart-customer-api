namespace Mart.Customer.Domain.Inventory;

public sealed class ProductBatchStock
{
    private ProductBatchStock()
    {
    }

    public long ProductBatchStockId { get; private set; }

    public long FranchiseId { get; private set; }

    public long MartStoreId { get; private set; }

    public long ProductId { get; private set; }

    public string? BatchNumber { get; private set; }

    public DateTime? ManufacturingDate { get; private set; }

    public DateTime? ExpiryDate { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal PurchasePrice { get; private set; }

    public decimal SellingPrice { get; private set; }

    public decimal MRP { get; private set; }

    public bool IsActive { get; private set; }

    public long? CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public long? ModifiedBy { get; private set; }

    public DateTime? ModifiedOn { get; private set; }

    public static ProductBatchStock Create(
        long franchiseId,
        long martStoreId,
        long productId,
        string? batchNumber,
        DateTime? manufacturingDate,
        DateTime? expiryDate,
        decimal quantity,
        decimal purchasePrice,
        decimal sellingPrice,
        decimal mrp,
        long createdBy,
        DateTime createdOn)
    {
        return new ProductBatchStock
        {
            FranchiseId = franchiseId,
            MartStoreId = martStoreId,
            ProductId = productId,
            BatchNumber = batchNumber,
            ManufacturingDate = manufacturingDate,
            ExpiryDate = expiryDate,
            Quantity = quantity,
            PurchasePrice = purchasePrice,
            SellingPrice = sellingPrice,
            MRP = mrp,
            IsActive = true,
            CreatedBy = createdBy,
            CreatedOn = createdOn
        };
    }

    public void Add(
        decimal quantity,
        decimal purchasePrice,
        decimal sellingPrice,
        decimal mrp,
        long modifiedBy,
        DateTime modifiedOn)
    {
        Quantity = checked(Quantity + quantity);
        PurchasePrice = purchasePrice;
        SellingPrice = sellingPrice;
        MRP = mrp;
        ModifiedBy = modifiedBy;
        ModifiedOn = modifiedOn;
    }

    public void Remove(decimal quantity, long modifiedBy, DateTime modifiedOn)
    {
        if (quantity <= 0 || quantity > Quantity)
        {
            throw new Common.DomainException("Insufficient batch stock.");
        }

        Quantity -= quantity;
        ModifiedBy = modifiedBy;
        ModifiedOn = modifiedOn;
    }
}
