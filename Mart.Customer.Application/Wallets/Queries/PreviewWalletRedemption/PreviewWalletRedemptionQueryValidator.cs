using FluentValidation;

namespace Mart.Customer.Application.Wallets.Queries.PreviewWalletRedemption;

public sealed class PreviewWalletRedemptionQueryValidator
    : AbstractValidator<PreviewWalletRedemptionQuery>
{
    private const decimal MaximumAmount = 9999999999999999.99m;

    public PreviewWalletRedemptionQueryValidator()
    {
        RuleFor(query => query.CustomerId).GreaterThan(0);
        RuleFor(query => query.WalletTypeId).GreaterThan(0);
        RuleFor(query => query.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaximumAmount)
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("Amount must have no more than two decimal places.");
    }
}
