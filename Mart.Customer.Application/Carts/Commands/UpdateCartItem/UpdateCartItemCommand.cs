using Mart.Customer.Application.Carts.Dtos;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.UpdateCartItem;

public sealed record UpdateCartItemCommand(
    string CartNumber,
    long CustomerCartItemId,
    long FranchiseId,
    long MartStoreId,
    string? ProductNameSnapshot,
    decimal Quantity,
    decimal UnitPrice,
    decimal MRP,
    decimal DiscountAmount,
    decimal GSTPercent) : IRequest<CreatedCartItemDto?>;
