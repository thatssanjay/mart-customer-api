namespace Mart.Customer.Application.Carts.Dtos;

public sealed record UpdatedCartPaymentStatusDto(long CartId, string Status);
