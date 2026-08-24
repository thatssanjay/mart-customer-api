using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Inventory.Dtos;
using MediatR;

namespace Mart.Customer.Application.Inventory.Queries.GetStockMovements;

public sealed class GetStockMovementsQueryHandler
    : IRequestHandler<GetStockMovementsQuery, PagedResultDto<StockMovementDto>>
{
    private readonly IStockMovementRepository _stockMovementRepository;

    public GetStockMovementsQueryHandler(IStockMovementRepository stockMovementRepository)
    {
        _stockMovementRepository = stockMovementRepository;
    }

    public async Task<PagedResultDto<StockMovementDto>> Handle(
        GetStockMovementsQuery request,
        CancellationToken cancellationToken)
    {
        var (movements, totalCount) = await _stockMovementRepository.GetPagedAsync(
            request.FranchiseId,
            request.MartStoreId,
            request.ProductId,
            request.MovementType,
            request.FromDate,
            request.ToDate,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResultDto<StockMovementDto>(
            movements,
            request.PageNumber,
            request.PageSize,
            totalCount,
            totalPages);
    }
}
