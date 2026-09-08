using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Common.Utilities;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.TopUpWallet;

public sealed class TopUpWalletCommandHandler(
    ICustomerWalletRepository wallets,
    IWalletTypeRepository walletTypes,
    IWalletTransactionRepository walletTransactions,
    IWalletBalanceBucketRepository balanceBuckets,
    IWalletTopUpPaymentRepository topUpPayments,
    IUnitOfWork unitOfWork) : IRequestHandler<TopUpWalletCommand, WalletTopUpResultDto>
{
    private const string MainWalletCode = "WALLET";
    private const string TopUpReferenceType = "WALLET_TOP_UP";

    public Task<WalletTopUpResultDto> Handle(
        TopUpWalletCommand request,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            token => TopUpAsync(request, token), cancellationToken);

    private async Task<WalletTopUpResultDto> TopUpAsync(
        TopUpWalletCommand request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.WalletCode.Trim(), MainWalletCode,
                StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Only the WALLET balance can be topped up.");

        var paymentMode = request.PaymentMode.Trim().ToUpperInvariant();
        var paymentReference = request.PaymentReference.Trim();
        var referenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber)
            ? null
            : request.ReferenceNumber.Trim();
        var cardLast4 = string.IsNullOrWhiteSpace(request.CardLast4)
            ? null
            : request.CardLast4.Trim();

        var existing = await topUpPayments.GetByPaymentReferenceAsync(
            paymentReference, cancellationToken);
        if (existing is not null)
            return EnsureSamePayment(existing, request.CustomerId, request.Amount,
                paymentMode, cardLast4, referenceNumber);

        if (referenceNumber is not null)
        {
            existing = await topUpPayments.GetByExternalReferenceAsync(
                paymentMode, referenceNumber, cancellationToken);
            if (existing is not null)
                return EnsureSamePayment(existing, request.CustomerId, request.Amount,
                    paymentMode, cardLast4, referenceNumber);
        }

        var walletType = (await walletTypes.GetAsync(true, cancellationToken))
            .SingleOrDefault(type => string.Equals(
                type.Code, MainWalletCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException("The WALLET type is not available.");
        var wallet = await wallets.GetByCustomerAndTypeAsync(
            request.CustomerId, walletType.Id, cancellationToken)
            ?? throw new DomainException("The customer's WALLET was not found.");
        if (!wallet.IsActive)
            throw new DomainException("The customer's WALLET is inactive.");

        var paidOn = DateTime.UtcNow;
        var previousBalance = wallet.CurrentBalance;
        var payment = WalletTopUpPayment.Begin(
            request.CustomerId, wallet.CustomerWalletId, request.Amount,
            previousBalance, paymentMode, cardLast4, referenceNumber,
            paymentReference, paidOn);
        await topUpPayments.AddAsync(payment, cancellationToken);

        // Generate the payment ID first so the immutable wallet ledger can refer to it.
        // Both saves remain inside the same serializable database transaction.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        wallet.Credit(request.Amount, paidOn);
        var transaction = WalletTransaction.CreateCredit(
            ReferenceCodeGenerator.GenerateWithPrefix("TX"),
            wallet.CustomerWalletId,
            request.Amount,
            previousBalance,
            wallet.CurrentBalance,
            TopUpReferenceType,
            payment.WalletTopUpPaymentId,
            $"Wallet top-up payment {paymentReference}",
            paidOn,
            request.CreatedBy);
        var bucket = WalletBalanceBucket.Create(
            wallet.CustomerWalletId, transaction, request.Amount, null, paidOn);

        await walletTransactions.AddAsync(transaction, cancellationToken);
        await balanceBuckets.AddAsync(bucket, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        payment.Complete(transaction, wallet.CurrentBalance, paidOn);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResult(payment);
    }

    private static WalletTopUpResultDto EnsureSamePayment(
        WalletTopUpPayment payment,
        long customerId,
        decimal amount,
        string paymentMode,
        string? cardLast4,
        string? referenceNumber)
    {
        if (payment.CustomerId != customerId || payment.Amount != amount ||
            payment.PaymentMode != paymentMode || payment.CardLast4 != cardLast4 ||
            payment.ReferenceNumber != referenceNumber)
            throw new DomainException(
                "The payment reference has already been used with different top-up details.");
        if (payment.Status != WalletTopUpPayment.SuccessfulStatus)
            throw new DomainException("The payment reference is already being processed.");

        return ToResult(payment);
    }

    private static WalletTopUpResultDto ToResult(WalletTopUpPayment payment) => new(
        payment.WalletTopUpPaymentId,
        payment.CustomerId,
        payment.CustomerWalletId,
        MainWalletCode,
        payment.Amount,
        payment.PreviousBalance,
        payment.NewBalance!.Value,
        payment.PaymentMode,
        payment.CardLast4,
        payment.ReferenceNumber,
        payment.PaymentReference,
        payment.WalletTransactionId!.Value,
        payment.WalletTransactionNumber!,
        payment.PaidOn!.Value);
}
