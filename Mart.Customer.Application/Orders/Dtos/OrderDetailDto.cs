namespace Mart.Customer.Application.Orders.Dtos;

public sealed record OrderDetailDto(
    long CustomerOrderId,
    long CustomerCartId,
    string InvoiceNumber,
    long CustomerId,
    long FranchiseId,
    long MartStoreId,
    DateTime OrderDate,
    int TotalItemCount,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal GSTAmount,
    decimal NetAmount,
    int? RedemptionWalletTypeId,
    decimal RedemptionAmount,
    decimal FinalPayableAmount,
    decimal RewardEarned,
    decimal CashbackEarned,
    string OrderStatus,
    string InvoiceStatus,
    bool IsInvoiceDocumentAvailable,
    IReadOnlyList<OrderDetailItemDto> Items,
    IReadOnlyList<OrderDetailPaymentDto> Payments);

public sealed record OrderDetailItemDto(
    long CustomerOrderItemId,
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

public sealed record OrderDetailPaymentDto(
    long CustomerOrderPaymentId,
    string PaymentMode,
    decimal Amount,
    string? TransactionReference,
    DateTime PaidOn);

public sealed record OrderDetailAccessScope(
    long? CustomerId,
    long? FranchiseId,
    long? MartStoreId);

public enum OrderDetailResultStatus
{
    Found,
    NotFound,
    Forbidden
}

public sealed record OrderDetailResult(
    OrderDetailResultStatus Status,
    OrderDetailDto? Order)
{
    public static OrderDetailResult Found(OrderDetailDto order) =>
        new(OrderDetailResultStatus.Found, order);

    public static OrderDetailResult NotFound() =>
        new(OrderDetailResultStatus.NotFound, null);

    public static OrderDetailResult Forbidden() =>
        new(OrderDetailResultStatus.Forbidden, null);
}
