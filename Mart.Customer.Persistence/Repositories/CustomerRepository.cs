using Mart.Customer.Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using CustomerEntity = Mart.Customer.Domain.Customers.Customer;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly ApplicationDbContext _dbContext;

    public CustomerRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerEntity?> GetByMobileNumberAsync(string mobileNumber, CancellationToken cancellationToken = default)
    {
        var normalizedMobileNumber = mobileNumber.Trim();

        return await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(customer => customer.MobileNumber == normalizedMobileNumber, cancellationToken);
    }

    public async Task<bool> ExistsByMobileNumberAsync(string mobileNumber, CancellationToken cancellationToken = default)
    {
        var normalizedMobileNumber = mobileNumber.Trim();

        return await _dbContext.Customers
            .AnyAsync(customer => customer.MobileNumber == normalizedMobileNumber, cancellationToken);
    }

    public async Task AddAsync(CustomerEntity customer, CancellationToken cancellationToken = default)
    {
        await _dbContext.Customers.AddAsync(customer, cancellationToken);
    }
}
