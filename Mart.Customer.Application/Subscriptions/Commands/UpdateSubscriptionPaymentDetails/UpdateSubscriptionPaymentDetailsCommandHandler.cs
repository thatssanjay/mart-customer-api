using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Subscriptions.Dtos;
using MediatR;

namespace Mart.Customer.Application.Subscriptions.Commands.UpdateSubscriptionPaymentDetails;

public sealed class UpdateSubscriptionPaymentDetailsCommandHandler
    : IRequestHandler<UpdateSubscriptionPaymentDetailsCommand, UpdatedSubscriptionPaymentDetailsDto?>
{
    private readonly ICustomerSubscriptionRepository _subscriptionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSubscriptionPaymentDetailsCommandHandler(
        ICustomerSubscriptionRepository subscriptionRepository,
        IUnitOfWork unitOfWork)
    {
        _subscriptionRepository = subscriptionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdatedSubscriptionPaymentDetailsDto?> Handle(
        UpdateSubscriptionPaymentDetailsCommand request,
        CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionRepository.GetByIdAndCustomerIdAsync(
            request.Id,
            request.CustomerId,
            cancellationToken);

        if (subscription is null)
        {
            return null;
        }

        subscription.UpdatePaymentDetails(
            request.IsPointCreated,
            request.WalletCreditAmount,
            request.PointReferenceId,
            request.PaymentTransactionId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdatedSubscriptionPaymentDetailsDto(
            subscription.Id,
            subscription.CustomerId,
            subscription.IsPointCreated,
            subscription.WalletCreditAmount,
            subscription.PointReferenceId,
            subscription.PaymentTransactionId);
    }
}
