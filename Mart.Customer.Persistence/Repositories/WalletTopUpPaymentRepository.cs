using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class WalletTopUpPaymentRepository(ApplicationDbContext dbContext)
    : IWalletTopUpPaymentRepository
{
    public Task<WalletTopUpPayment?> GetByPaymentReferenceAsync(
        string paymentReference,
        CancellationToken cancellationToken = default) =>
        dbContext.WalletTopUpPayments.AsNoTracking().SingleOrDefaultAsync(
            payment => payment.PaymentReference == paymentReference, cancellationToken);

    public Task<WalletTopUpPayment?> GetByExternalReferenceAsync(
        string paymentMode,
        string referenceNumber,
        CancellationToken cancellationToken = default) =>
        dbContext.WalletTopUpPayments.AsNoTracking().SingleOrDefaultAsync(
            payment => payment.PaymentMode == paymentMode &&
                       payment.ReferenceNumber == referenceNumber,
            cancellationToken);

    public Task AddAsync(
        WalletTopUpPayment payment,
        CancellationToken cancellationToken = default) =>
        dbContext.WalletTopUpPayments.AddAsync(payment, cancellationToken).AsTask();
}
