namespace Mart.Customer.Application.Orders.Dtos;

public sealed record OrderVerificationDto(
    string InvoiceNumber,
    DateTime InvoiceDate,
    long Store,
    decimal Amount,
    string Status,
    bool IsValid);
