using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
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

    public Task<CustomerEntity?> GetByIdAsync(long customerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Customers
            .FirstOrDefaultAsync(customer => customer.CustomerId == customerId, cancellationToken);
    }

    public async Task<CustomerEntity?> GetByMobileNumberAsync(string mobileNumber, CancellationToken cancellationToken = default)
    {
        var normalizedMobileNumber = mobileNumber.Trim();

        return await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(customer => customer.MobileNumber == normalizedMobileNumber, cancellationToken);
    }

    public async Task<(IReadOnlyList<CustomerEntity> Customers, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? displayName,
        string? mobileNumber,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var normalizedDisplayName = displayName.Trim();
            query = query.Where(customer =>
                customer.DisplayName != null &&
                EF.Functions.Like(customer.DisplayName, $"%{normalizedDisplayName}%"));
        }

        if (!string.IsNullOrWhiteSpace(mobileNumber))
        {
            var normalizedMobileNumber = mobileNumber.Trim();
            query = query.Where(customer => customer.MobileNumber == normalizedMobileNumber);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var customers = await query
            .OrderBy(customer => customer.CustomerId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (customers, totalCount);
    }

    public async Task<IReadOnlyList<CustomerLookupDto>> SearchAsync(
        string search,
        int maximumResults,
        CancellationToken cancellationToken = default)
    {
        var searchPattern = $"%{search.Trim()}%";

        return await _dbContext.Customers
            .AsNoTracking()
            .Where(customer =>
                (customer.DisplayName != null &&
                 EF.Functions.Like(customer.DisplayName, searchPattern)) ||
                EF.Functions.Like(customer.MobileNumber, searchPattern))
            .OrderBy(customer => customer.DisplayName)
            .ThenBy(customer => customer.CustomerId)
            .Take(maximumResults)
            .Select(customer => new CustomerLookupDto(
                customer.DisplayName,
                customer.MobileNumber))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByMobileNumberAsync(string mobileNumber, CancellationToken cancellationToken = default)
    {
        var normalizedMobileNumber = mobileNumber.Trim();

        return await _dbContext.Customers
            .AnyAsync(customer => customer.MobileNumber == normalizedMobileNumber, cancellationToken);
    }

    public Task<bool> ExistsByMobileNumberAsync(
        string mobileNumber,
        long excludedCustomerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedMobileNumber = mobileNumber.Trim();

        return _dbContext.Customers.AnyAsync(
            customer => customer.MobileNumber == normalizedMobileNumber &&
                        customer.CustomerId != excludedCustomerId,
            cancellationToken);
    }

    public Task<bool> ExistsByCustomerCodeAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        return _dbContext.Customers
            .AnyAsync(customer => customer.CustomerCode == customerCode, cancellationToken);
    }

    public async Task AddAsync(CustomerEntity customer, CancellationToken cancellationToken = default)
    {
        await _dbContext.Customers.AddAsync(customer, cancellationToken);
    }
}
