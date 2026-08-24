using CustomerEntity = Mart.Customer.Domain.Customers.Customer;
using Mart.Customer.Application.Customers.Dtos;

namespace Mart.Customer.Application.Abstractions.Data;

public interface ICustomerRepository
{
    Task<bool> ExistsByIdAsync(long customerId, CancellationToken cancellationToken = default);

    Task<CustomerEntity?> GetByIdAsync(long customerId, CancellationToken cancellationToken = default);

    Task<CustomerEntity?> GetByMobileNumberAsync(string mobileNumber, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CustomerEntity> Customers, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? displayName,
        string? mobileNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerLookupDto>> SearchAsync(
        string search,
        int maximumResults,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByMobileNumberAsync(string mobileNumber, CancellationToken cancellationToken = default);

    Task<bool> ExistsByMobileNumberAsync(
        string mobileNumber,
        long excludedCustomerId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByCustomerCodeAsync(string customerCode, CancellationToken cancellationToken = default);

    Task AddAsync(CustomerEntity customer, CancellationToken cancellationToken = default);
}
