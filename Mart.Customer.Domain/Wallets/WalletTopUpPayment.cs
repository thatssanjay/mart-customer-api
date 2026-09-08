using Mart.Customer.Domain.Common;

namespace Mart.Customer.Domain.Wallets;

public sealed class WalletTopUpPayment
{
    public const string SuccessfulStatus = "SUCCESS";
    public const string ProcessingStatus = "PROCESSING";

    private WalletTopUpPayment() { }

    public long WalletTopUpPaymentId { get; private set; }
    public long CustomerId { get; private set; }
    public long CustomerWalletId { get; private set; }
    public decimal Amount { get; private set; }
    public decimal PreviousBalance { get; private set; }
    public decimal? NewBalance { get; private set; }
    public string PaymentMode { get; private set; } = string.Empty;
    public string? CardLast4 { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string PaymentReference { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public long? WalletTransactionId { get; private set; }
    public string? WalletTransactionNumber { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public DateTime? PaidOn { get; private set; }

    public static WalletTopUpPayment Begin(
        long customerId,
        long customerWalletId,
        decimal amount,
        decimal previousBalance,
        string paymentMode,
        string? cardLast4,
        string? referenceNumber,
        string paymentReference,
        DateTime createdOn)
    {
        if (customerId <= 0 || customerWalletId <= 0)
            throw new DomainException("Customer and wallet IDs must be greater than zero.");
        if (amount <= 0)
            throw new DomainException("Top-up amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(paymentMode) || string.IsNullOrWhiteSpace(paymentReference))
            throw new DomainException("Payment mode and payment reference are required.");

        return new WalletTopUpPayment
        {
            CustomerId = customerId,
            CustomerWalletId = customerWalletId,
            Amount = amount,
            PreviousBalance = previousBalance,
            PaymentMode = paymentMode.Trim().ToUpperInvariant(),
            CardLast4 = string.IsNullOrWhiteSpace(cardLast4) ? null : cardLast4.Trim(),
            ReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber) ? null : referenceNumber.Trim(),
            PaymentReference = paymentReference.Trim(),
            Status = ProcessingStatus,
            CreatedOn = createdOn
        };
    }

    public void Complete(WalletTransaction transaction, decimal newBalance, DateTime paidOn)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (Status != ProcessingStatus)
            throw new DomainException("Only a processing wallet top-up can be completed.");
        if (transaction.CustomerWalletId != CustomerWalletId || transaction.Amount != Amount)
            throw new DomainException("The wallet transaction does not match this top-up payment.");
        if (newBalance != PreviousBalance + Amount ||
            transaction.BalanceBefore != PreviousBalance ||
            transaction.BalanceAfter != newBalance)
            throw new DomainException("The wallet top-up balance audit is inconsistent.");

        NewBalance = newBalance;
        WalletTransactionId = transaction.WalletTransactionId;
        WalletTransactionNumber = transaction.TransactionNumber;
        Status = SuccessfulStatus;
        PaidOn = paidOn;
    }
}
