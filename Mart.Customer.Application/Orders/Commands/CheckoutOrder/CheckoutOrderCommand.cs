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
    IReadOnlyList<CheckoutPayment> Payments) : IRequest<OrderCheckoutDto?>;

public sealed record CheckoutPayment(
    string? PaymentMode,
    decimal Amount,
    string? TransactionReference);
