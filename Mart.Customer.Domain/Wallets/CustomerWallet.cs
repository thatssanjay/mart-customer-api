using Mart.Customer.Domain.Common;

namespace Mart.Customer.Domain.Wallets;

public sealed class CustomerWallet
{
    private const decimal MaximumAmount = 9999999999999999.99m;

    private CustomerWallet()
    {
    }

    public long CustomerWalletId { get; private set; }

    public long CustomerId { get; private set; }

    public long? StoreId { get; private set; }

    public int WalletTypeId { get; private set; }

    public decimal CurrentBalance { get; private set; }

    public decimal TotalCredit { get; private set; }

    public decimal TotalDebit { get; private set; }

    public decimal TotalExpired { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public DateTime? ModifiedOn { get; private set; }

    public static CustomerWallet Create(long customerId, int walletTypeId, DateTime createdOn, long? storeId = null)
    {
        if (customerId <= 0)
        {
            throw new DomainException("Customer ID must be greater than zero.");
        }

        if (walletTypeId <= 0)
        {
            throw new DomainException("Wallet type ID must be greater than zero.");
        }

        if (storeId <= 0)
        {
            throw new DomainException("Store ID must be greater than zero.");
        }

        return new CustomerWallet
        {
            CustomerId = customerId,
            StoreId = storeId,
            WalletTypeId = walletTypeId,
            CurrentBalance = 0,
            TotalCredit = 0,
            TotalDebit = 0,
            TotalExpired = 0,
            IsActive = true,
            CreatedOn = createdOn
        };
    }

    public void Credit(decimal amount, DateTime modifiedOn)
    {
        if (!IsActive)
        {
            throw new DomainException("Customer wallet is inactive.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Credit amount must be greater than zero.");
        }

        if (decimal.Round(amount, 2) != amount || amount > MaximumAmount)
        {
            throw new DomainException("Credit amount must fit decimal(18,2).");
        }

        if (CurrentBalance > MaximumAmount - amount || TotalCredit > MaximumAmount - amount)
        {
            throw new DomainException("Credit would exceed the wallet amount limit.");
        }

        CurrentBalance += amount;
        TotalCredit += amount;
        ModifiedOn = modifiedOn;
    }

    public void Redeem(decimal amount, DateTime modifiedOn)
    {
        if (!IsActive)
        {
            throw new DomainException("Customer wallet is inactive.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Redemption amount must be greater than zero.");
        }

        if (decimal.Round(amount, 2) != amount || amount > MaximumAmount)
        {
            throw new DomainException("Redemption amount must fit decimal(18,2).");
        }

        if (CurrentBalance < amount)
        {
            throw new DomainException("Insufficient wallet balance.");
        }

        if (TotalDebit > MaximumAmount - amount)
        {
            throw new DomainException("Redemption would exceed the wallet amount limit.");
        }

        CurrentBalance -= amount;
        TotalDebit += amount;
        ModifiedOn = modifiedOn;
    }

    public void Refund(decimal amount, DateTime modifiedOn)
    {
        if (!IsActive)
        {
            throw new DomainException("Customer wallet is inactive.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Refund amount must be greater than zero.");
        }

        if (decimal.Round(amount, 2) != amount || amount > MaximumAmount)
        {
            throw new DomainException("Refund amount must fit decimal(18,2).");
        }

        if (CurrentBalance > MaximumAmount - amount)
        {
            throw new DomainException("Refund would exceed the wallet amount limit.");
        }

        CurrentBalance += amount;
        ModifiedOn = modifiedOn;
    }

    public void UpdateStatus(bool isActive, DateTime modifiedOn)
    {
        if (IsActive == isActive)
        {
            return;
        }

        IsActive = isActive;
        ModifiedOn = modifiedOn;
    }
}
