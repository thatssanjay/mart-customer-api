using Mart.Customer.Application.Subscriptions.Dtos;
using MediatR;

namespace Mart.Customer.Application.Subscriptions.Commands.UpdateSubscriptionPaymentDetails;

public sealed record UpdateSubscriptionPaymentDetailsCommand(
    long Id,
    long CustomerId,
    bool? IsPointCreated,
    decimal? WalletCreditAmount,
    long? PointReferenceId,
    long? PaymentTransactionId) : IRequest<UpdatedSubscriptionPaymentDetailsDto?>;
