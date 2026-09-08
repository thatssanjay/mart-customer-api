using FluentValidation;

namespace Mart.Customer.Application.Wallets.Commands.TopUpWallet;

public sealed class TopUpWalletCommandValidator : AbstractValidator<TopUpWalletCommand>
{
    private static readonly string[] SupportedPaymentModes = ["CARD", "NETBANKING", "UPI"];
    private const decimal MaximumAmount = 9999999999999999.99m;

    public TopUpWalletCommandValidator()
    {
        RuleFor(command => command.CustomerId).GreaterThan(0);
        RuleFor(command => command.WalletCode)
            .NotEmpty()
            .Must(code => string.Equals(code?.Trim(), "WALLET", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only the WALLET balance can be topped up.");
        RuleFor(command => command.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaximumAmount)
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("Amount must have no more than two decimal places.");
        RuleFor(command => command.PaymentMode)
            .NotEmpty()
            .MaximumLength(20)
            .Must(mode => SupportedPaymentModes.Contains(mode?.Trim().ToUpperInvariant()))
            .WithMessage("Payment mode must be CARD, NETBANKING, or UPI.");
        RuleFor(command => command.CardLast4)
            .NotEmpty()
            .Matches("^[0-9]{4}$")
            .When(command => IsCard(command.PaymentMode));
        RuleFor(command => command.ReferenceNumber)
            .Empty()
            .When(command => IsCard(command.PaymentMode))
            .WithMessage("Reference number is not used for card payments.");
        RuleFor(command => command.ReferenceNumber)
            .NotEmpty()
            .MaximumLength(100)
            .When(command => !IsCard(command.PaymentMode));
        RuleFor(command => command.CardLast4)
            .Empty()
            .When(command => !IsCard(command.PaymentMode))
            .WithMessage("Card last 4 digits are only used for card payments.");
        RuleFor(command => command.PaymentReference).NotEmpty().MaximumLength(100);
        RuleFor(command => command.CreatedBy).NotEmpty().MaximumLength(100);
    }

    private static bool IsCard(string? mode) =>
        string.Equals(mode?.Trim(), "CARD", StringComparison.OrdinalIgnoreCase);
}
