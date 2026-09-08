using Mart.Customer.Domain.Wallets;

namespace Mart.Customer.Application.Abstractions.Data;

public interface IWalletTopUpPaymentRepository
{
    Task<WalletTopUpPayment?> GetByPaymentReferenceAsync(
        string paymentReference,
        CancellationToken cancellationToken = default);

    Task<WalletTopUpPayment?> GetByExternalReferenceAsync(
        string paymentMode,
        string referenceNumber,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        WalletTopUpPayment payment,
        CancellationToken cancellationToken = default);
}
