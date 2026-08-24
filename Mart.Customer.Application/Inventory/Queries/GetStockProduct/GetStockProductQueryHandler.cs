using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Inventory.Dtos;
using MediatR;

namespace Mart.Customer.Application.Inventory.Queries.GetStockProduct;

public sealed class GetStockProductQueryHandler
    : IRequestHandler<GetStockProductQuery, StockProductDetailDto?>
{
    private readonly IProductRepository _productRepository;

    public GetStockProductQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public Task<StockProductDetailDto?> Handle(
        GetStockProductQuery request,
        CancellationToken cancellationToken) =>
        _productRepository.GetStockProductAsync(
            request.ProductId,
            request.FranchiseId,
            request.MartStoreId,
            cancellationToken);
}
