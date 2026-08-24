using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Inventory.Dtos;
using MediatR;

namespace Mart.Customer.Application.Inventory.Queries.SearchStockProducts;

public sealed record SearchStockProductsQuery(
    long FranchiseId,
    long MartStoreId,
    string? Search,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResultDto<StockProductListItemDto>>;
