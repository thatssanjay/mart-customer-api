using Mart.Customer.Application.Wallets.Dtos;

namespace Mart.Customer.Application.Orders.Dtos;

public sealed record OrderCheckoutPreviewDto(
    long CustomerCartId,
    string CartNumber,
    long CustomerId,
    long FranchiseId,
    long MartStoreId,
    string CartStatus,
    int TotalItemCount,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal GSTAmount,
    decimal NetAmount,
    decimal RedemptionAmount,
    decimal FinalPayableAmount,
    IReadOnlyList<OrderCheckoutPreviewItemDto> Items,
    RedeemPreviewResultDto? WalletRedemption);

public sealed record OrderCheckoutPreviewItemDto(
    long CustomerCartItemId,
    long ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal MRP,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal GSTPercent,
    decimal GSTAmount,
    decimal LineTotal);
