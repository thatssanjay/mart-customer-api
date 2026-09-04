using Mart.Customer.Application.Wallets.Engine;

namespace Mart.Customer.Application.Abstractions.Data;

public interface IOrderWalletSourceRepository
{
    Task<OrderWalletSource?> GetAsync(long orderId, CancellationToken cancellationToken);
    Task<OrderWalletConfiguration?> GetConfigurationAsync(long storeId, DateTime effectiveAt, CancellationToken cancellationToken);
}
