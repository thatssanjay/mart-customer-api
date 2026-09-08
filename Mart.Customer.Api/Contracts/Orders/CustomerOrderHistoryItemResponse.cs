using Mart.Customer.Application.Orders.Dtos;

namespace Mart.Customer.Api.Contracts.Orders;

public sealed record CustomerOrderHistoryItemResponse(
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
    bool IsInvoiceDocumentAvailable,
    string? InvoicePdfReference)
{
    public static CustomerOrderHistoryItemResponse FromDto(OrderHistoryItemDto order) =>
        new(
            order.CustomerOrderId,
            order.InvoiceNumber,
            order.OrderDate,
            order.TotalItemCount,
            order.GrossAmount,
            order.DiscountAmount,
            order.GSTAmount,
            order.NetAmount,
            order.RedemptionAmount,
            order.FinalPayableAmount,
            order.RewardEarned,
            order.CashbackEarned,
            order.OrderStatus,
            order.InvoiceStatus,
            order.IsInvoiceDocumentAvailable,
            order.IsInvoiceDocumentAvailable
                ? $"/api/v1/orders/{order.CustomerOrderId}/invoice-pdf?disposition=attachment"
                : null);
}
