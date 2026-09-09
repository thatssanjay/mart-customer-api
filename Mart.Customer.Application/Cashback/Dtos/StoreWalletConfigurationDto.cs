namespace Mart.Customer.Application.Cashback.Dtos;

public sealed record StoreWalletConfigurationDto(
    long StoreId,
    string StoreName,
    string? City,
    decimal ConversionRate);

public sealed record ActiveStoreWalletConversionDto(
    long StoreId,
    string StoreName,
    int WalletTypeId,
    long CashbackSettingId,
    int WalletSettingId,
    decimal ConversionRate,
    DateTime? ExpiryDate);

public sealed record StoreWalletConfigurationsPageDto(
    int PageNumber,
    int PageSize,
    int TotalRecords,
    bool HasNextPage,
    IReadOnlyList<StoreWalletConfigurationDto> Items);
