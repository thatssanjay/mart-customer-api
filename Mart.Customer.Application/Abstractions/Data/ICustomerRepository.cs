using CustomerEntity = Mart.Customer.Domain.Customers.Customer;

namespace Mart.Customer.Application.Abstractions.Data;

public interface ICustomerRepository
{
    Task<CustomerEntity?> GetByMobileNumberAsync(string mobileNumber, CancellationToken cancellationToken = default);

    Task<bool> ExistsByMobileNumberAsync(string mobileNumber, CancellationToken cancellationToken = default);

    Task AddAsync(CustomerEntity customer, CancellationToken cancellationToken = default);
}
