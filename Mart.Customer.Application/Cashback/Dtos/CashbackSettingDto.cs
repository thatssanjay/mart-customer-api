namespace Mart.Customer.Application.Cashback.Dtos;

public sealed record CashbackSettingDto(
    int StoreId,
    decimal CashbackPercentage,
    int CashbackValidityDays,
    decimal MinimumPurchaseAmount,
    decimal MaximumCashbackPerOrder,
    bool IsActive);
