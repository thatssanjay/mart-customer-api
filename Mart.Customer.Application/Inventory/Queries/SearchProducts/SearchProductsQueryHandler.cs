using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Inventory.Dtos;
using MediatR;

namespace Mart.Customer.Application.Inventory.Queries.SearchProducts;

public sealed class SearchProductsQueryHandler
    : IRequestHandler<SearchProductsQuery, IReadOnlyList<ProductListItemDto>>
{
    private const int MaximumResults = 5;
    private readonly IProductRepository _productRepository;

    public SearchProductsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public Task<IReadOnlyList<ProductListItemDto>> Handle(
        SearchProductsQuery request,
        CancellationToken cancellationToken)
    {
        return _productRepository.SearchActiveAsync(
            request.Search,
            MaximumResults,
            cancellationToken);
    }
}
