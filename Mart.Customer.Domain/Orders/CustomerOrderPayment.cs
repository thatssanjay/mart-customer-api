namespace Mart.Customer.Domain.Orders;

public sealed class CustomerOrderPayment
{
    private CustomerOrderPayment() { }

    public long CustomerOrderPaymentId { get; private set; }
    public long CustomerOrderId { get; private set; }
    public string PaymentMode { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string? TransactionReference { get; private set; }
    public DateTime PaidOn { get; private set; }
    public CustomerOrder CustomerOrder { get; private set; } = null!;

    internal static CustomerOrderPayment Create(
        string paymentMode,
        decimal amount,
        string? transactionReference,
        DateTime paidOn) =>
        new()
        {
            PaymentMode = paymentMode,
            Amount = amount,
            TransactionReference = string.IsNullOrWhiteSpace(transactionReference)
                ? null
                : transactionReference.Trim(),
            PaidOn = paidOn
        };
}
