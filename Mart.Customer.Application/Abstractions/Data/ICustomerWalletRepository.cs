using Mart.Customer.Domain.Wallets;
using Mart.Customer.Application.Wallets.Dtos;

namespace Mart.Customer.Application.Abstractions.Data;

public interface ICustomerWalletRepository
{
    Task<long?> GetActiveIdAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default);

    Task<CustomerWalletDto?> GetDetailAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default);

    Task<CustomerWalletDto?> GetDetailIncludingInactiveAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerWalletDto>> GetDetailsByCustomerIdAsync(
        long customerId,
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerWallet>> GetByCustomerIdAsync(
        long customerId,
        CancellationToken cancellationToken = default);

    Task<CustomerWallet?> GetByCustomerAndTypeAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IEnumerable<CustomerWallet> wallets,
        CancellationToken cancellationToken = default);
}
