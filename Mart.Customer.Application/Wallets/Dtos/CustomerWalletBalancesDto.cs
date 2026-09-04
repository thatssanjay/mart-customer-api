namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record CustomerWalletBalancesDto(
    long CustomerId,
    IReadOnlyList<CustomerWalletBalanceDto> Wallets);

public sealed record CustomerWalletBalanceDto(
    int WalletTypeId,
    string WalletName,
    string WalletCode,
    int DisplayOrder,
    long? StoreId,
    string? StoreCode,
    string? StoreName,
    decimal Balance);
