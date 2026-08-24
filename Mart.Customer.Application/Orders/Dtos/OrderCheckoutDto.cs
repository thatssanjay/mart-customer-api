namespace Mart.Customer.Application.Orders.Dtos;

public sealed record OrderCheckoutDto(
    long CustomerOrderId,
    long CustomerCartId,
    string InvoiceNumber,
    long CustomerId,
    long FranchiseId,
    long MartStoreId,
    DateTime OrderDate,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal GSTAmount,
    decimal NetAmount,
    decimal RedemptionAmount,
    decimal FinalPayableAmount,
    decimal RewardEarned,
    decimal CashbackEarned,
    string OrderStatus,
    string InvoiceStatus,
    bool IsIdempotentRetry,
    IReadOnlyList<OrderCheckoutItemDto> Items,
    IReadOnlyList<OrderCheckoutPaymentDto> Payments);

public sealed record OrderCheckoutItemDto(
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

public sealed record OrderCheckoutPaymentDto(
    long CustomerOrderPaymentId,
    string PaymentMode,
    decimal Amount,
    string? TransactionReference,
    DateTime PaidOn);
