namespace Mart.Customer.Application.Orders.Dtos;

public sealed record OrderHistoryItemDto(
    long CustomerOrderId,
    string InvoiceNumber,
    DateTime OrderDate,
    int TotalItemCount,
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
    bool IsInvoiceDocumentAvailable);
