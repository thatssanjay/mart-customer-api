namespace Mart.Customer.Application.Carts.Dtos;

public sealed record CreatedCartItemDto(
    long CustomerCartItemId,
    string CartNumber,
    long ProductId,
    string ProductNameSnapshot,
    decimal Quantity,
    decimal UnitPrice,
    decimal MRP,
    decimal DiscountAmount,
    decimal GSTPercent,
    decimal GSTAmount,
    decimal LineTotal,
    long AddedByCashierId,
    DateTime AddedOn);
