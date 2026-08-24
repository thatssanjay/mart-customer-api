using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Inventory.Dtos;
using MediatR;

namespace Mart.Customer.Application.Inventory.Queries.SearchStockProducts;

public sealed class SearchStockProductsQueryHandler
    : IRequestHandler<SearchStockProductsQuery, PagedResultDto<StockProductListItemDto>>
{
    private readonly IProductRepository _productRepository;

    public SearchStockProductsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<PagedResultDto<StockProductListItemDto>> Handle(
        SearchStockProductsQuery request,
        CancellationToken cancellationToken)
    {
        var (products, totalCount) = await _productRepository.SearchStockProductsAsync(
            request.FranchiseId,
            request.MartStoreId,
            request.Search,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResultDto<StockProductListItemDto>(
            products,
            request.PageNumber,
            request.PageSize,
            totalCount,
            totalPages);
    }
}
