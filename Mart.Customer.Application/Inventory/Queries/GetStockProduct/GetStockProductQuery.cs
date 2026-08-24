using Mart.Customer.Application.Inventory.Dtos;
using MediatR;

namespace Mart.Customer.Application.Inventory.Queries.GetStockProduct;

public sealed record GetStockProductQuery(
    long ProductId,
    long FranchiseId,
    long MartStoreId) : IRequest<StockProductDetailDto?>;
