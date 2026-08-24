using Mart.Customer.Domain.Carts;

namespace Mart.Customer.Application.Abstractions.Data;

public interface ICustomerCartRepository
{
    Task<bool> ExistsByCartNumberAsync(string cartNumber, CancellationToken cancellationToken = default);
    Task<CustomerCart?> GetByIdAsync(
        long customerCartId,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default);
    Task<CustomerCart?> GetByCartNumberAsync(
        string cartNumber,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default);
    Task<CustomerCart?> GetByCartNumberForCheckoutPreviewAsync(
        string cartNumber,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default);
    Task<CustomerCart?> GetByCartNumberForCheckoutAsync(
        string cartNumber,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerCart>> GetByCustomerAndStatusAsync(
        long customerId,
        string cartStatus,
        long? franchiseId,
        long? martStoreId,
        CancellationToken cancellationToken = default);
    Task AddAsync(CustomerCart cart, CancellationToken cancellationToken = default);
    void Remove(CustomerCart cart);
}
