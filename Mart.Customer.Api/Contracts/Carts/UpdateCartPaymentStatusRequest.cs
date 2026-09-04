namespace Mart.Customer.Api.Contracts.Carts;

public sealed record UpdateCartPaymentStatusRequest(long CartId, string? Status);
