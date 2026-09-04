namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record CustomerWalletDto(
    long CustomerWalletId,
    long CustomerId,
    int WalletTypeId,
    string WalletTypeName,
    string WalletTypeCode,
    string? WalletTypeDescription,
    decimal CurrentBalance,
    decimal TotalCredit,
    decimal TotalDebit,
    decimal TotalExpired,
    bool IsActive,
    bool IsWalletTypeActive,
    DateTime CreatedOn,
    DateTime? ModifiedOn,
    long? StoreId = null);
