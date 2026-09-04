namespace Mart.Customer.Application.Cashback.Dtos;

public sealed record CashbackSettingDto(
    long StoreId,
    decimal CashbackPercentage,
    int CashbackValidityDays,
    decimal MinimumPurchaseAmount,
    decimal MaximumCashbackPerOrder,
    bool IsActive);
