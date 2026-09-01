using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Commands.CheckoutOrder;

public sealed record CheckoutOrderCommand(
    string? CartNumber,
    long FranchiseId,
    long MartStoreId,
    long CashierId,
    int? WalletTypeId,
    decimal? RedemptionAmount,
    string? WalletPaymentToken,
    IReadOnlyList<CheckoutPayment> Payments) : IRequest<OrderCheckoutDto?>
{
    public CheckoutOrderCommand(
        string? cartNumber,
        long franchiseId,
        long martStoreId,
        long cashierId,
        int? walletTypeId,
        decimal? redemptionAmount,
        IReadOnlyList<CheckoutPayment> payments)
        : this(
            cartNumber,
            franchiseId,
            martStoreId,
            cashierId,
            walletTypeId,
            redemptionAmount,
            null,
            payments)
    {
    }
}

public sealed record CheckoutPayment(
    string? PaymentMode,
    decimal Amount,
    string? TransactionReference);
