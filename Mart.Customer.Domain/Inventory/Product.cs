namespace Mart.Customer.Domain.Inventory;

public sealed class Product
{
    private Product()
    {
    }

    public long ProductId { get; private set; }

    public string ProductCode { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public long CategoryId { get; private set; }

    public long? BrandId { get; private set; }

    public long UnitId { get; private set; }

    public string? Barcode { get; private set; }

    public string? HSNCode { get; private set; }

    public string ProductType { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsExpiryApplicable { get; private set; }

    public bool IsBatchApplicable { get; private set; }

    public decimal GSTPercent { get; private set; }

    public decimal MRP { get; private set; }

    public decimal DefaultSellingPrice { get; private set; }

    public decimal DefaultPurchasePrice { get; private set; }

    public long MinimumQuantity { get; private set; }

    public long MaximumQuantity { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsStockManaged { get; private set; }
}
