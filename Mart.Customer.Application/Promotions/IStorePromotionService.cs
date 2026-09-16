namespace Mart.Customer.Application.Promotions;

public interface IStorePromotionService
{
    Task<IReadOnlyList<PromotionOrderDto>> SearchTodayOrdersAsync(
        long franchiseId,
        long storeId,
        string? invoiceNumber,
        string? mobileNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActiveStorePromotionDto>> GetActivePromotionsAsync(
        long franchiseId,
        long storeId,
        CancellationToken cancellationToken = default);

    Task<PromotionAllocationDto> AllocateAsync(
        long userId,
        long franchiseId,
        long storeId,
        long orderId,
        string promoCode,
        string createdBy,
        CancellationToken cancellationToken = default);
}

public sealed record PromotionOrderDto(
    long CustomerOrderId,
    string InvoiceNumber,
    DateTime OrderDate,
    long CustomerId,
    string CustomerName,
    string CustomerMobileNumber,
    decimal FinalPayableAmount,
    string OrderStatus,
    int Status,
    string? Remarks);

public sealed record ActiveStorePromotionDto(
    string PromoCode,
    string DiscountType,
    decimal RewardValue,
    int WalletTypeId,
    string WalletName);

public sealed record PromotionAllocationDto(
    long CustomerOrderId,
    string PromoCode,
    long CustomerWalletId,
    int WalletTypeId,
    string WalletName,
    decimal AwardedAmount,
    decimal BalanceAfter,
    long WalletTransactionId,
    string TransactionNumber,
    int Status,
    string Remarks);
