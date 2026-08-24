using Mart.Customer.Application.Inventory.Commands.StockIn;
using Mart.Customer.Application.Inventory.Commands.StockOut;
using Mart.Customer.Application.Inventory.Dtos;

namespace Mart.Customer.Application.Inventory.Services;

public interface IInventoryStockService
{
    Task EnsureCartQuantityAvailableAsync(
        long productId,
        long franchiseId,
        long martStoreId,
        decimal requestedQuantity,
        CancellationToken cancellationToken = default);

    Task DeductSaleStockAsync(
        long customerOrderId,
        CancellationToken cancellationToken = default);

    Task<StockInResultDto> StockInAsync(
        StockInCommand command,
        CancellationToken cancellationToken = default);

    Task<StockOutResultDto> StockOutAsync(
        StockOutCommand command,
        CancellationToken cancellationToken = default);
}
