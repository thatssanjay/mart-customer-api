using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;

namespace Mart.Customer.Application.Wallets.Engine;

public interface ICustomerWalletResolver
{
    Task<CustomerWallet> ResolveAsync(long customerId, int walletTypeId, long? storeId,
        CancellationToken cancellationToken = default);
}

public sealed class CustomerWalletResolver(ICustomerWalletRepository wallets,
    IWalletTypeRepository types, IUnitOfWork unitOfWork, IWalletPostingGuard guard) : ICustomerWalletResolver
{
    public async Task<CustomerWallet> ResolveAsync(long customerId, int walletTypeId, long? storeId,
        CancellationToken cancellationToken = default)
    {
        guard.EnsureTransaction();
        if (customerId <= 0 || walletTypeId <= 0)
            throw new DomainException("Customer and wallet type IDs must be positive.");
        var type = await types.GetByIdAsync(walletTypeId, cancellationToken)
            ?? throw new DomainException("Wallet type not found.");
        if (!type.IsActive)
            throw new DomainException("Wallet type is inactive.");
        if (string.Equals(type.Code, WalletTypeCodes.MartWallet, StringComparison.OrdinalIgnoreCase))
        {
            if (!storeId.HasValue || storeId <= 0)
                throw new DomainException("MART_WALLET requires a store.");
        }
        else
        {
            storeId = null;
        }

        var wallet = await wallets.GetByCustomerAndTypeAsync(customerId, walletTypeId, cancellationToken, storeId);
        if (wallet is not null)
        {
            if (!wallet.IsActive)
                throw new DomainException("Customer wallet is inactive.");
            return wallet;
        }
        wallet = CustomerWallet.Create(customerId, walletTypeId, DateTime.UtcNow, storeId);
        await wallets.AddRangeAsync([wallet], cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return wallet;
    }
}
