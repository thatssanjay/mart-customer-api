using FluentValidation;

namespace Mart.Customer.Application.Subscriptions.Commands.UpdateSubscriptionPaymentDetails;

public sealed class UpdateSubscriptionPaymentDetailsCommandValidator
    : AbstractValidator<UpdateSubscriptionPaymentDetailsCommand>
{
    private const decimal MaximumWalletCreditAmount = 9999999999999999.99m;

    public UpdateSubscriptionPaymentDetailsCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
        RuleFor(command => command.CustomerId).GreaterThan(0);
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
