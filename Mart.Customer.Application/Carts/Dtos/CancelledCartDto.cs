namespace Mart.Customer.Application.Carts.Dtos;

public sealed record CancelledCartDto(
    long CustomerCartId,
    string CartNumber,
    string CartStatus,
    DateTime CancelledOn,
    string? Remarks);
