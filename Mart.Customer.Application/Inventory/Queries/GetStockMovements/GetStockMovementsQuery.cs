using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Inventory.Dtos;
using MediatR;

namespace Mart.Customer.Application.Inventory.Queries.GetStockMovements;

public sealed record GetStockMovementsQuery(
    long FranchiseId,
    long MartStoreId,
    long? ProductId = null,
    string? MovementType = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResultDto<StockMovementDto>>;
