namespace Mart.Customer.Domain.Inventory;

public sealed class Product
{
    private Product()
    {
    }

    public long ProductId { get; private set; }

    public string ProductCode { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public decimal GSTPercent { get; private set; }

    public decimal MRP { get; private set; }

    public decimal DefaultSellingPrice { get; private set; }

    public decimal DefaultPurchasePrice { get; private set; }

    public bool IsActive { get; private set; }
}
