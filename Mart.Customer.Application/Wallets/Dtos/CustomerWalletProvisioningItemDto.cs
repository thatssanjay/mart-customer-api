namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record CustomerWalletProvisioningItemDto(
    long CustomerWalletId,
    int WalletTypeId,
    string WalletTypeCode,
    bool IsActive);
