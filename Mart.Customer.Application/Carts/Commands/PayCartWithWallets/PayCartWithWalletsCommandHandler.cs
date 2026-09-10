using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Carts.Dtos;
using Mart.Customer.Application.Payments.Services;
using Mart.Customer.Application.Wallets.Commands.RedeemWallet;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.PayCartWithWallets;

public sealed class PayCartWithWalletsCommandHandler(
    ICustomerCartRepository cartRepository,
    ICustomerWalletRepository customerWalletRepository,
    IWalletTransactionRepository walletTransactionRepository,
    IWalletTypeRepository walletTypeRepository,
    IWalletPaymentRequestService walletPaymentRequestService,
    IUnitOfWork unitOfWork,
    ISender sender) : IRequestHandler<PayCartWithWalletsCommand, CartWalletPaymentResultDto>
{
    private const string CartPaymentReferenceType = "CART_PAYMENT";
    private const string MainWalletCode = "WALLET";

    public Task<CartWalletPaymentResultDto> Handle(
        PayCartWithWalletsCommand request,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            token => PayAsync(request, token),
            cancellationToken);

    private async Task<CartWalletPaymentResultDto> PayAsync(
        PayCartWithWalletsCommand request,
        CancellationToken cancellationToken)
    {
        var cart = await cartRepository.GetByIdForWalletAsync(request.CartId, cancellationToken);
        if (cart is null ||
            cart.CustomerId != request.CustomerId ||
            !string.Equals(cart.CartNumber, request.CartNumber?.Trim(), StringComparison.Ordinal))
        {
            throw new DomainException("Invalid cart for the authenticated customer.");
        }

        var existingTransactions = await walletTransactionRepository.GetByReferenceAsync(
            request.CustomerId,
            CartPaymentReferenceType,
            cart.CustomerCartId,
            cancellationToken);
        if (existingTransactions.Count > 0 ||
            string.Equals(cart.CartStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Payment has already been completed for this cart.");
        }

        var attempt = cart.GetWalletPaymentAttempt();
        string paymentReference;
        if (attempt is not null)
        {
            paymentReference = await walletPaymentRequestService.ValidatePendingForPaymentAsync(
                cart,
                request.PaymentToken,
                cart.FinalPayableAmount,
                cancellationToken);
        }
        else
        {
            if (!string.Equals(cart.CartStatus, "PaymentPending", StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException("The cart is inactive or is not awaiting payment.");
            }

            if (!string.IsNullOrWhiteSpace(request.PaymentToken))
            {
                throw new DomainException("The wallet payment token does not belong to this cart.");
            }

            paymentReference = $"CART-{cart.CustomerCartId}";
        }

        if (cart.FinalPayableAmount <= 0)
        {
            throw new DomainException("The cart payable amount must be greater than zero.");
        }

        var deductions = request.Deductions
            .Select(item => new NormalizedDeduction(
                item.WalletCode?.Trim().ToUpperInvariant() ?? string.Empty,
                item.Amount))
            .ToList();
        if (deductions.Any(item => item.WalletCode is not (MainWalletCode or WalletTypeCodes.MartWallet)) ||
            deductions.GroupBy(item => item.WalletCode, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            throw new DomainException("Only one WALLET and one MART_WALLET deduction may be supplied.");
        }

        if (deductions.Sum(item => item.Amount) != cart.FinalPayableAmount)
        {
            throw new DomainException("Wallet deductions must exactly equal the final payable amount.");
        }

        var walletTypes = await walletTypeRepository.GetAsync(activeOnly: true, cancellationToken);
        var results = new List<CartWalletDeductionResultDto>(deductions.Count);
        foreach (var deduction in deductions)
        {
            var walletType = walletTypes.SingleOrDefault(type =>
                string.Equals(type.Code, deduction.WalletCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new DomainException($"{deduction.WalletCode} wallet type is not available.");
            var storeId = deduction.WalletCode == WalletTypeCodes.MartWallet
                ? cart.MartStoreId
                : (long?)null;
            var wallet = await customerWalletRepository.GetByCustomerAndTypeAsync(
                request.CustomerId,
                walletType.Id,
                cancellationToken,
                storeId);
            if (wallet is null || !wallet.IsActive)
            {
                throw new DomainException($"{deduction.WalletCode} is not available for this cart.");
            }

            if (deduction.Amount > wallet.CurrentBalance)
            {
                throw new DomainException($"Insufficient {deduction.WalletCode} balance.");
            }

            var redemption = await sender.Send(
                new RedeemWalletCommand(
                    request.CustomerId,
                    walletType.Id,
                    deduction.Amount,
                    CartPaymentReferenceType,
                    cart.CustomerCartId,
                    $"Payment for cart {cart.CartNumber}",
                    request.CreatedBy,
                    storeId),
                cancellationToken);
            results.Add(new CartWalletDeductionResultDto(
                deduction.WalletCode,
                redemption.CustomerWalletId,
                redemption.WalletTransactionId,
                redemption.TransactionNumber,
                redemption.Amount,
                redemption.BalanceBefore,
                redemption.BalanceAfter));
        }

        if (attempt is not null)
        {
            cart.UpdatePaymentStatus("PAID");
        }
        else
        {
            cart.ChangeStatus("Paid");
        }

        var paidOn = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CartWalletPaymentResultDto(
            cart.CustomerCartId,
            cart.CartNumber,
            paymentReference,
            "PAID",
            results.Sum(item => item.Amount),
            paidOn,
            results);
    }

    private sealed record NormalizedDeduction(string WalletCode, decimal Amount);
}
