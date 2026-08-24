namespace Mart.Customer.Domain.Orders;

public sealed class CustomerOrderItem
{
    private CustomerOrderItem() { }

    public long CustomerOrderItemId { get; private set; }
    public long CustomerOrderId { get; private set; }
    public long CustomerCartItemId { get; private set; }
    public long ProductId { get; private set; }
    public string? ProductCodeSnapshot { get; private set; }
    public string? HSNCodeSnapshot { get; private set; }
    public string ProductNameSnapshot { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal MRP { get; private set; }
    public decimal GrossAmount => Quantity * UnitPrice;
    public decimal DiscountAmount { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal CGSTAmount { get; private set; }
    public decimal SGSTAmount { get; private set; }
    public decimal IGSTAmount { get; private set; }
    public decimal GSTPercent { get; private set; }
    public decimal GSTAmount { get; private set; }
    public decimal LineTotal { get; private set; }
    public CustomerOrder CustomerOrder { get; private set; } = null!;

    internal static CustomerOrderItem Create(
        long customerCartItemId, long productId, string productName, decimal quantity,
        decimal unitPrice, decimal mrp, decimal grossAmount, decimal discountAmount,
        decimal gstPercent, decimal gstAmount, decimal lineTotal) =>
        new()
        {
            CustomerCartItemId = customerCartItemId,
            ProductId = productId,
            ProductNameSnapshot = productName.Trim(),
            Quantity = quantity,
            UnitPrice = unitPrice,
            MRP = mrp,
            DiscountAmount = discountAmount,
            TaxableAmount = grossAmount - discountAmount,
            GSTPercent = gstPercent,
            GSTAmount = gstAmount,
            LineTotal = lineTotal
        };

    public void ApplyProductSnapshot(string productCode, string productName)
    {
        ProductCodeSnapshot = string.IsNullOrWhiteSpace(productCode)
            ? null
            : productCode.Trim();
        ProductNameSnapshot = productName.Trim();
    }

    internal void ApplyTaxSplit(bool isIntraState)
    {
        if (isIntraState)
        {
            CGSTAmount = decimal.Round(GSTAmount / 2m, 2, MidpointRounding.AwayFromZero);
            SGSTAmount = GSTAmount - CGSTAmount;
            IGSTAmount = 0m;
            return;
        }

        CGSTAmount = 0m;
        SGSTAmount = 0m;
        IGSTAmount = GSTAmount;
    }
}
