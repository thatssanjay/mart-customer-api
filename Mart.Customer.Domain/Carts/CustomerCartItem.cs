namespace Mart.Customer.Domain.Carts;

public sealed class CustomerCartItem
{
    private CustomerCartItem()
    {
    }

    public long CustomerCartItemId { get; private set; }
    public long CustomerCartId { get; private set; }
    public long ProductId { get; private set; }
    public string ProductNameSnapshot { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal MRP { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal GSTPercent { get; private set; }
    public decimal GSTAmount { get; private set; }
    public decimal LineTotal { get; private set; }
    public long AddedByCashierId { get; private set; }
    public DateTime AddedOn { get; private set; }
    public CustomerCart CustomerCart { get; private set; } = null!;

    public decimal GrossAmount => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    public static CustomerCartItem Create(
        long productId,
        string productNameSnapshot,
        decimal quantity,
        decimal unitPrice,
        decimal mrp,
        decimal discountAmount,
        decimal gstPercent,
        long addedByCashierId)
    {
        var grossAmount = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        var taxableAmount = grossAmount - discountAmount;
        var gstAmount = decimal.Round(taxableAmount * gstPercent / 100, 2, MidpointRounding.AwayFromZero);

        return new CustomerCartItem
        {
            ProductId = productId,
            ProductNameSnapshot = productNameSnapshot.Trim(),
            Quantity = quantity,
            UnitPrice = unitPrice,
            MRP = mrp,
            DiscountAmount = discountAmount,
            GSTPercent = gstPercent,
            GSTAmount = gstAmount,
            LineTotal = taxableAmount + gstAmount,
            AddedByCashierId = addedByCashierId,
            AddedOn = DateTime.UtcNow
        };
    }

    internal void Update(
        string productNameSnapshot,
        decimal quantity,
        decimal unitPrice,
        decimal mrp,
        decimal discountAmount,
        decimal gstPercent)
    {
        var grossAmount = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        var taxableAmount = grossAmount - discountAmount;

        ProductNameSnapshot = productNameSnapshot.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        MRP = mrp;
        DiscountAmount = discountAmount;
        GSTPercent = gstPercent;
        GSTAmount = decimal.Round(taxableAmount * gstPercent / 100, 2, MidpointRounding.AwayFromZero);
        LineTotal = taxableAmount + GSTAmount;
    }
}
