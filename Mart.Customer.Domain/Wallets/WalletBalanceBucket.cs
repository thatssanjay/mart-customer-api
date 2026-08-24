namespace Mart.Customer.Domain.Wallets;

using Mart.Customer.Domain.Common;

public sealed class WalletBalanceBucket
{
    private WalletBalanceBucket()
    {
    }

    public long WalletBalanceBucketId { get; private set; }

    public long CustomerWalletId { get; private set; }

    public long SourceTransactionId { get; private set; }

    public decimal OriginalAmount { get; private set; }

    public decimal AvailableAmount { get; private set; }

    public DateTime? ExpiryDate { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public WalletTransaction SourceTransaction { get; private set; } = null!;

    public static WalletBalanceBucket Create(
        long customerWalletId,
        WalletTransaction sourceTransaction,
        decimal amount,
        DateTime? expiryDate,
        DateTime createdOn)
    {
        if (customerWalletId <= 0)
        {
            throw new DomainException("Customer wallet ID must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(sourceTransaction);

        if (amount <= 0)
        {
            throw new DomainException("Bucket amount must be greater than zero.");
        }

        if (expiryDate.HasValue && expiryDate.Value <= createdOn)
        {
            throw new DomainException("Expiry date must be later than the credit transaction date.");
        }

        return new WalletBalanceBucket
        {
            CustomerWalletId = customerWalletId,
            SourceTransaction = sourceTransaction,
            OriginalAmount = amount,
            AvailableAmount = amount,
            ExpiryDate = expiryDate,
            CreatedOn = createdOn
        };
    }

    public void Redeem(decimal amount)
    {
        if (amount <= 0)
        {
            throw new DomainException("Bucket redemption amount must be greater than zero.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("Bucket redemption amount must have no more than two decimal places.");
        }

        if (AvailableAmount < amount)
        {
            throw new DomainException("Bucket has insufficient available balance.");
        }

        AvailableAmount -= amount;
    }
}
