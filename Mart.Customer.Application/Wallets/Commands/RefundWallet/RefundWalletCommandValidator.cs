using FluentValidation;

namespace Mart.Customer.Application.Wallets.Commands.RefundWallet;

public sealed class RefundWalletCommandValidator : AbstractValidator<RefundWalletCommand>
{
    private const decimal MaximumAmount = 9999999999999999.99m;

    public RefundWalletCommandValidator()
    {
        RuleFor(command => command.CustomerId).GreaterThan(0);
        RuleFor(command => command.WalletTypeId).GreaterThan(0);
        RuleFor(command => command.OriginalTransactionNumber)
            .NotEmpty()
            .MaximumLength(50);
        RuleFor(command => command.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaximumAmount)
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("Amount must have no more than two decimal places.");
        RuleFor(command => command.Remarks)
            .MaximumLength(500)
            .When(command => command.Remarks is not null);
        RuleFor(command => command.CreatedBy)
            .NotEmpty()
            .MaximumLength(100);
    }
}
