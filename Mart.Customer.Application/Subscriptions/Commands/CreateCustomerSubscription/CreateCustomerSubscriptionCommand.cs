using Mart.Customer.Application.Subscriptions.Dtos;
using MediatR;

namespace Mart.Customer.Application.Subscriptions.Commands.CreateCustomerSubscription;

public sealed record CreateCustomerSubscriptionCommand(
    long CustomerId,
    int SubscriptionPlanId,
    decimal? WalletCreditAmount,
    long? PointReferenceId,
    long? PaymentTransactionId,
    long CreatedBy) : IRequest<CreatedCustomerSubscriptionDto>;
