using FluentValidation;

namespace Mart.Customer.Application.Subscriptions.Commands.CreateCustomerSubscription;

public sealed class CreateCustomerSubscriptionCommandValidator
    : AbstractValidator<CreateCustomerSubscriptionCommand>
{
    private const decimal MaximumWalletCreditAmount = 9999999999999999.99m;

    public CreateCustomerSubscriptionCommandValidator()
    {
        RuleFor(command => command.CustomerId).GreaterThan(0);
        RuleFor(command => command.SubscriptionPlanId).GreaterThan(0);
        RuleFor(command => command.CreatedBy)
            .InclusiveBetween(1, int.MaxValue)
            .WithMessage("The authenticated user id is outside the supported range.");

        RuleFor(command => command.WalletCreditAmount)
            .GreaterThanOrEqualTo(0)
            .When(command => command.WalletCreditAmount.HasValue);

        RuleFor(command => command.WalletCreditAmount)
            .Must(amount => !amount.HasValue ||
                            amount.Value <= MaximumWalletCreditAmount &&
                            decimal.Round(amount.Value, 2) == amount.Value)
            .WithMessage("Wallet credit amount must fit decimal(18,2).");
    }
}
