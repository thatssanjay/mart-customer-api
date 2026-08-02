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

    public void Remove(CustomerCart cart)
    {
        _dbContext.CustomerCarts.Remove(cart);
    }

    public async Task<IReadOnlyList<CustomerCart>> GetByCustomerAndStatusAsync(
        long customerId,
        string cartStatus,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CustomerCarts
            .AsNoTracking()
            .Include(cart => cart.Items)
            .Where(cart => cart.CustomerId == customerId && cart.CartStatus == cartStatus)
            .OrderByDescending(cart => cart.CreatedOn)
            .ToListAsync(cancellationToken);
    }
}
