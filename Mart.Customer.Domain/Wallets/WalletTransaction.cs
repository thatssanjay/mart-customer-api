namespace Mart.Customer.Domain.Wallets;

using Mart.Customer.Domain.Common;

public sealed class WalletTransaction
{
    public const string RefundReferenceType = "WALLET_TRANSACTION";

    private WalletTransaction()
    {
    }

    public long WalletTransactionId { get; private set; }

    public string TransactionNumber { get; private set; } = string.Empty;

    public long CustomerWalletId { get; private set; }

    public string TransactionType { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public decimal BalanceBefore { get; private set; }

    public decimal BalanceAfter { get; private set; }

    public string? ReferenceType { get; private set; }

    public long? ReferenceId { get; private set; }

    public string? Remarks { get; private set; }

    public DateTime TransactionDate { get; private set; }

    public string? CreatedBy { get; private set; }

    public static WalletTransaction CreateCredit(
        string transactionNumber,
        long customerWalletId,
        decimal amount,
        decimal balanceBefore,
        decimal balanceAfter,
        string? referenceType,
        long? referenceId,
        string? remarks,
        DateTime transactionDate,
        string createdBy)
    {
        if (string.IsNullOrWhiteSpace(transactionNumber))
        {
            throw new DomainException("Transaction number is required.");
        }

        if (customerWalletId <= 0)
        {
            throw new DomainException("Customer wallet ID must be greater than zero.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Credit amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new DomainException("Created by is required.");
        }

        return new WalletTransaction
        {
            TransactionNumber = transactionNumber.Trim(),
            CustomerWalletId = customerWalletId,
            TransactionType = "CREDIT",
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReferenceType = string.IsNullOrWhiteSpace(referenceType) ? null : referenceType.Trim(),
            ReferenceId = referenceId,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
            TransactionDate = transactionDate,
            CreatedBy = createdBy.Trim()
        };
    }

    public static WalletTransaction CreateRedemption(
        string transactionNumber,
        long customerWalletId,
        decimal amount,
        decimal balanceBefore,
        decimal balanceAfter,
        string? referenceType,
        long? referenceId,
        string? remarks,
        DateTime transactionDate,
        string createdBy)
    {
        if (string.IsNullOrWhiteSpace(transactionNumber))
        {
            throw new DomainException("Transaction number is required.");
        }

        if (customerWalletId <= 0)
        {
            throw new DomainException("Customer wallet ID must be greater than zero.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Redemption amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new DomainException("Created by is required.");
        }

        return new WalletTransaction
        {
            TransactionNumber = transactionNumber.Trim(),
            CustomerWalletId = customerWalletId,
            TransactionType = "REDEMPTION",
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReferenceType = string.IsNullOrWhiteSpace(referenceType) ? null : referenceType.Trim(),
            ReferenceId = referenceId,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
            TransactionDate = transactionDate,
            CreatedBy = createdBy.Trim()
        };
    }

    public static WalletTransaction CreateRefund(
        string transactionNumber,
        long customerWalletId,
        long originalTransactionId,
        decimal amount,
        decimal balanceBefore,
        decimal balanceAfter,
        string? remarks,
        DateTime transactionDate,
        string createdBy)
    {
        if (string.IsNullOrWhiteSpace(transactionNumber))
        {
            throw new DomainException("Transaction number is required.");
        }

        if (customerWalletId <= 0)
        {
            throw new DomainException("Customer wallet ID must be greater than zero.");
        }

        if (originalTransactionId <= 0)
        {
            throw new DomainException("Original transaction ID must be greater than zero.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Refund amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new DomainException("Created by is required.");
        }

        return new WalletTransaction
        {
            TransactionNumber = transactionNumber.Trim(),
            CustomerWalletId = customerWalletId,
            TransactionType = "REFUND",
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReferenceType = RefundReferenceType,
            ReferenceId = originalTransactionId,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
            TransactionDate = transactionDate,
            CreatedBy = createdBy.Trim()
        };
    }
}
