using Mart.Customer.Application.Wallets.Dtos;

namespace Mart.Customer.Application.Abstractions.Data;

public interface IWalletTypeRepository
{
    Task<WalletTypeDto?> GetByIdAsync(
        int walletTypeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WalletTypeDto>> GetAsync(
        bool activeOnly,
        CancellationToken cancellationToken = default);
}
