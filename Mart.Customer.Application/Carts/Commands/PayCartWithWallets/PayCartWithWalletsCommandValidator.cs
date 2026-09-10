using FluentValidation;

namespace Mart.Customer.Application.Carts.Commands.PayCartWithWallets;

public sealed class PayCartWithWalletsCommandValidator : AbstractValidator<PayCartWithWalletsCommand>
{
    public PayCartWithWalletsCommandValidator()
    {
        RuleFor(command => command.CartId).GreaterThan(0);
        RuleFor(command => command.CartNumber).NotEmpty().MaximumLength(50);
        RuleFor(command => command.PaymentToken).MaximumLength(200);
        RuleFor(command => command.CustomerId).GreaterThan(0);
        RuleFor(command => command.CreatedBy).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Deductions).NotNull().Must(items => items is { Count: >= 1 and <= 2 })
            .WithMessage("Select at least one wallet and no more than two wallets.");
        RuleForEach(command => command.Deductions).ChildRules(item =>
        {
            item.RuleFor(value => value.WalletCode).NotEmpty().MaximumLength(30);
            item.RuleFor(value => value.Amount)
                .GreaterThan(0)
                .Must(amount => decimal.Round(amount, 2) == amount)
                .WithMessage("Wallet deduction must be greater than zero and have no more than two decimal places.");
        });
    }
}
