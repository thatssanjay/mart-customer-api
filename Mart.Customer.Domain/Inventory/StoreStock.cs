namespace Mart.Customer.Domain.Inventory;

public sealed class StoreStock
{
    private StoreStock()
    {
    }

    public long StoreStockId { get; private set; }

    public long FranchiseId { get; private set; }

    public long MartStoreId { get; private set; }

    public long ProductId { get; private set; }

    public decimal CurrentQuantity { get; private set; }

    public decimal? LastPurchasePrice { get; private set; }

    public decimal? SellingPrice { get; private set; }

    public DateTime? LastStockUpdatedOn { get; private set; }

    public bool IsActive { get; private set; }

    public long? CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public long? ModifiedBy { get; private set; }

    public DateTime? ModifiedOn { get; private set; }

    public static StoreStock Create(
        long franchiseId,
        long martStoreId,
        long productId,
        decimal quantity,
        decimal purchasePrice,
        decimal sellingPrice,
        long createdBy,
        DateTime createdOn)
    {
        return new StoreStock
        {
            FranchiseId = franchiseId,
            MartStoreId = martStoreId,
            ProductId = productId,
            CurrentQuantity = quantity,
            LastPurchasePrice = purchasePrice,
            SellingPrice = sellingPrice,
            LastStockUpdatedOn = createdOn,
            IsActive = true,
            CreatedBy = createdBy,
            CreatedOn = createdOn
        };
    }

    public void Add(
        decimal quantity,
        decimal purchasePrice,
        decimal sellingPrice,
        long modifiedBy,
        DateTime modifiedOn)
    {
        CurrentQuantity = checked(CurrentQuantity + quantity);
        LastPurchasePrice = purchasePrice;
        SellingPrice = sellingPrice;
        LastStockUpdatedOn = modifiedOn;
        IsActive = true;
        ModifiedBy = modifiedBy;
        ModifiedOn = modifiedOn;
    }

    public void Remove(decimal quantity, long modifiedBy, DateTime modifiedOn)
    {
        if (quantity <= 0 || quantity > CurrentQuantity)
        {
            throw new Common.DomainException("Insufficient store stock.");
        }

        CurrentQuantity -= quantity;
        LastStockUpdatedOn = modifiedOn;
        ModifiedBy = modifiedBy;
        ModifiedOn = modifiedOn;
    }
}
