using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Inventory.Dtos;
using MediatR;

namespace Mart.Customer.Application.Inventory.Queries.GetLowStockProducts;

public sealed class GetLowStockProductsQueryHandler
    : IRequestHandler<GetLowStockProductsQuery, PagedResultDto<LowStockProductDto>>
{
    private readonly IProductRepository _productRepository;

    public GetLowStockProductsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<PagedResultDto<LowStockProductDto>> Handle(
        GetLowStockProductsQuery request,
        CancellationToken cancellationToken)
    {
        var (products, totalCount) = await _productRepository.GetLowStockProductsAsync(
            request.FranchiseId,
            request.MartStoreId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResultDto<LowStockProductDto>(
            products,
            request.PageNumber,
            request.PageSize,
            totalCount,
            totalPages);
    }
}
