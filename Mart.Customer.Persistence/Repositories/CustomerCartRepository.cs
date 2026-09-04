using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Domain.Carts;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class CustomerCartRepository : ICustomerCartRepository
{
    private readonly ApplicationDbContext _dbContext;

    public CustomerCartRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsByCartNumberAsync(
        string cartNumber,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerCarts
            .AnyAsync(cart => cart.CartNumber == cartNumber, cancellationToken);
    }

    public Task AddAsync(CustomerCart cart, CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerCarts.AddAsync(cart, cancellationToken).AsTask();
    }

    public Task<CustomerCart?> GetByIdAsync(
        long customerCartId,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerCarts.SingleOrDefaultAsync(
            cart => cart.CustomerCartId == customerCartId &&
                    cart.FranchiseId == franchiseId &&
                    cart.MartStoreId == martStoreId,
            cancellationToken);
    }

    public Task<CustomerCart?> GetByIdForWalletAsync(
        long customerCartId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerCarts
            .Include(cart => cart.Items)
            .SingleOrDefaultAsync(
                cart => cart.CustomerCartId == customerCartId,
                cancellationToken);
    }

    public Task<CustomerCart?> GetByCartNumberAsync(
        string cartNumber,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerCarts
            .Include(cart => cart.Items)
            .SingleOrDefaultAsync(
                cart => cart.CartNumber == cartNumber &&
                        cart.FranchiseId == franchiseId &&
                        cart.MartStoreId == martStoreId,
                cancellationToken);
    }

    public Task<CustomerCart?> GetByCartNumberForCheckoutPreviewAsync(
        string cartNumber,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerCarts
            .AsNoTracking()
            .Include(cart => cart.Items)
            .SingleOrDefaultAsync(
                cart => cart.CartNumber == cartNumber &&
                        cart.FranchiseId == franchiseId &&
                        cart.MartStoreId == martStoreId,
                cancellationToken);
    }

    public Task<CustomerCart?> GetByCartNumberForCheckoutAsync(
        string cartNumber,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerCarts
            .Include(cart => cart.Items)
            .SingleOrDefaultAsync(
                cart => cart.CartNumber == cartNumber &&
                        cart.FranchiseId == franchiseId &&
                        cart.MartStoreId == martStoreId,
                cancellationToken);
    }

    public void Remove(CustomerCart cart)
    {
        _dbContext.CustomerCarts.Remove(cart);
    }

    public async Task<IReadOnlyList<CustomerCart>> GetByCustomerAndStatusAsync(
        long customerId,
        string cartStatus,
        long? franchiseId,
        long? martStoreId,
        CancellationToken cancellationToken = default,
        string? cartNumber = null)
    {
        var query = _dbContext.CustomerCarts
            .AsNoTracking()
            .Include(cart => cart.Items)
            .Where(cart => cart.CustomerId == customerId && cart.CartStatus == cartStatus);

        if (cartNumber is not null)
        {
            query = query.Where(cart => cart.CartNumber == cartNumber);
        }

        if (franchiseId.HasValue && martStoreId.HasValue)
        {
            query = query.Where(cart =>
                cart.FranchiseId == franchiseId.Value &&
                cart.MartStoreId == martStoreId.Value);
        }

        return await query
            .OrderByDescending(cart => cart.CreatedOn)
            .ToListAsync(cancellationToken);
    }
}
