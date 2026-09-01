namespace Mart.Customer.Application.Orders.Dtos;

public sealed record OrderSearchItemDto(
    long CustomerOrderId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    string? CustomerName,
    string? CustomerMobileNumber,
    decimal FinalPayableAmount,
    string OrderStatus);
