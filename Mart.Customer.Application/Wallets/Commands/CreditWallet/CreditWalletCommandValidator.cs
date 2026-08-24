using FluentValidation;

namespace Mart.Customer.Application.Wallets.Commands.CreditWallet;

public sealed class CreditWalletCommandValidator : AbstractValidator<CreditWalletCommand>
{
    private const decimal MaximumAmount = 9999999999999999.99m;

    public CreditWalletCommandValidator()
    {
        RuleFor(command => command.CustomerId).GreaterThan(0);
        RuleFor(command => command.WalletTypeId).GreaterThan(0);
        RuleFor(command => command.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaximumAmount)
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("Amount must have no more than two decimal places.");
        RuleFor(command => command.ReferenceType)
            .NotEmpty()
            .MaximumLength(30)
            .When(command => command.ReferenceId.HasValue);
        RuleFor(command => command.ReferenceId)
            .NotNull()
            .GreaterThan(0)
            .When(command => !string.IsNullOrWhiteSpace(command.ReferenceType));
        RuleFor(command => command.ReferenceType)
            .MaximumLength(30)
            .When(command => !string.IsNullOrWhiteSpace(command.ReferenceType));
        RuleFor(command => command.Remarks)
            .MaximumLength(500)
            .When(command => command.Remarks is not null);
        RuleFor(command => command.ExpiryDate)
            .Must(expiryDate => !expiryDate.HasValue || expiryDate.Value > DateTime.UtcNow)
            .WithMessage("Expiry date must be in the future.");
        RuleFor(command => command.CreatedBy)
            .NotEmpty()
            .MaximumLength(100);
    }
}
