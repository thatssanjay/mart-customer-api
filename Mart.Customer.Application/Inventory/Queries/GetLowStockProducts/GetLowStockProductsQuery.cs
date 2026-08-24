using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Inventory.Dtos;
using MediatR;

namespace Mart.Customer.Application.Inventory.Queries.GetLowStockProducts;

public sealed record GetLowStockProductsQuery(
    long FranchiseId,
    long MartStoreId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResultDto<LowStockProductDto>>;
