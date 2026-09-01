using FluentValidation;

namespace Mart.Customer.Application.Orders.Commands.CheckoutOrder;

public sealed class CheckoutOrderCommandValidator : AbstractValidator<CheckoutOrderCommand>
{
    private const decimal MaximumAmount = 9999999999999999.99m;

    public CheckoutOrderCommandValidator()
    {
        RuleFor(command => command.CartNumber).NotEmpty().MaximumLength(50);
        RuleFor(command => command.FranchiseId).GreaterThan(0);
        RuleFor(command => command.MartStoreId).GreaterThan(0);
        RuleFor(command => command.CashierId).GreaterThan(0);
        RuleFor(command => command.Payments).NotNull();
        RuleFor(command => command.WalletPaymentToken).MaximumLength(200);
        RuleForEach(command => command.Payments).ChildRules(payment =>
        {
            payment.RuleFor(item => item.PaymentMode).NotEmpty().MaximumLength(30);
            payment.RuleFor(item => item.Amount)
                .GreaterThan(0)
                .LessThanOrEqualTo(MaximumAmount)
                .Must(amount => decimal.Round(amount, 2) == amount)
                .WithMessage("Payment amount must have no more than two decimal places.");
            payment.RuleFor(item => item.TransactionReference)
                .MaximumLength(100)
                .When(item => item.TransactionReference is not null);
        });

        When(command => command.RedemptionAmount.HasValue, () =>
        {
            RuleFor(command => command.RedemptionAmount!.Value)
                .GreaterThan(0)
                .LessThanOrEqualTo(MaximumAmount)
                .Must(amount => decimal.Round(amount, 2) == amount)
                .WithMessage("Redemption amount must have no more than two decimal places.");
            RuleFor(command => command.WalletTypeId).NotNull().GreaterThan(0);
        });
        When(command => command.WalletTypeId.HasValue, () =>
            RuleFor(command => command.RedemptionAmount).NotNull());
    }
}
