using Mart.Customer.Application.Payments.Dtos;
using Mart.Customer.Domain.Carts;

namespace Mart.Customer.Application.Payments.Services;

public interface IWalletPaymentRequestService
{
    Task<WalletPaymentRequestDto?> CreateAsync(
        string cartNumber,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default);

    Task<WalletPaymentStatusDto?> GetStatusAsync(
        string paymentToken,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default);

    Task<WalletPaymentCartDto?> GetCartForCustomerAsync(
        string paymentToken,
        long customerId,
        CancellationToken cancellationToken = default);

    Task<string> ValidateForCheckoutAsync(
        CustomerCart cart,
        string? paymentToken,
        decimal finalPayableAmount,
        CancellationToken cancellationToken = default);
}
