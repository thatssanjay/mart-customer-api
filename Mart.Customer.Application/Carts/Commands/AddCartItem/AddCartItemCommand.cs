using Mart.Customer.Application.Carts.Dtos;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.AddCartItem;

public sealed record AddCartItemCommand(
    string CartNumber,
    long FranchiseId,
    long MartStoreId,
    long AddedByCashierId,
    long ProductId,
    string? ProductNameSnapshot,
    decimal Quantity,
    decimal UnitPrice,
    decimal MRP,
    decimal DiscountAmount,
    decimal GSTPercent) : IRequest<CreatedCartItemDto?>;
