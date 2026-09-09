using FluentValidation;

namespace Mart.Customer.Application.Wallets.Commands.ConvertRewardToStoreWallet;

public sealed class ConvertRewardToStoreWalletCommandValidator
    : AbstractValidator<ConvertRewardToStoreWalletCommand>
{
    private const decimal MaximumAmount = 9999999999999999.99m;

    public ConvertRewardToStoreWalletCommandValidator()
    {
        RuleFor(command => command.CustomerId).GreaterThan(0);
        RuleFor(command => command.StoreId).GreaterThan(0);
        RuleFor(command => command.RewardPoints)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaximumAmount)
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("Reward points must have no more than two decimal places.");
        RuleFor(command => command.RequestId)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("Request id contains unsupported characters.");
        RuleFor(command => command.CreatedBy).NotEmpty().MaximumLength(100);
    }
}
