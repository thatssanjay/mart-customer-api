using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Subscriptions.Dtos;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Subscriptions;
using MediatR;

namespace Mart.Customer.Application.Subscriptions.Commands.CreateCustomerSubscription;

public sealed class CreateCustomerSubscriptionCommandHandler
    : IRequestHandler<CreateCustomerSubscriptionCommand, CreatedCustomerSubscriptionDto>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerSubscriptionRepository _subscriptionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerSubscriptionCommandHandler(
        ICustomerRepository customerRepository,
        ICustomerSubscriptionRepository subscriptionRepository,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _subscriptionRepository = subscriptionRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<CreatedCustomerSubscriptionDto> Handle(
        CreateCustomerSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        return _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            try
            {
                return await CreateAsync(request, token);
            }
            catch
            {
                // Discard rolled-back entities before the execution strategy retries.
                _unitOfWork.ClearChanges();
                throw;
            }
        }, cancellationToken);
    }

    private async Task<CreatedCustomerSubscriptionDto> CreateAsync(
        CreateCustomerSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
        {
            throw new DomainException("Customer not found.");
        }

        if (!customer.IsActive || customer.IsBlocked)
        {
            throw new DomainException("Customer is inactive or blocked.");
        }

        var plan = await _subscriptionRepository.GetPlanByIdAsync(
            request.SubscriptionPlanId,
            cancellationToken);

        if (plan is null)
        {
            throw new DomainException("Subscription plan not found.");
        }

        var createdOn = DateTime.UtcNow;
        if (!plan.IsEffectiveOn(createdOn))
        {
            throw new DomainException("Subscription plan is not active.");
        }

        if (await _subscriptionRepository.HasActiveSubscriptionAsync(
                request.CustomerId, request.SubscriptionPlanId, cancellationToken))
        {
            throw new DomainException("Customer already has an active subscription for this plan.");
        }

        var subscription = CustomerSubscription.Create(
            request.CustomerId,
            plan,
            request.WalletCreditAmount,
            request.PointReferenceId,
            request.PaymentTransactionId,
            request.CreatedBy,
            createdOn);

        await _subscriptionRepository.AddAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedCustomerSubscriptionDto(subscription.Id);
    }
}
