using FluentValidation;

namespace Mart.Customer.Application.Orders.Queries.GetOrderCheckoutPreview;

public sealed class GetOrderCheckoutPreviewQueryValidator
    : AbstractValidator<GetOrderCheckoutPreviewQuery>
{
    private const decimal MaximumAmount = 9999999999999999.99m;

    public GetOrderCheckoutPreviewQueryValidator()
    {
        RuleFor(query => query.CartNumber).NotEmpty().MaximumLength(50);
        RuleFor(query => query.FranchiseId).GreaterThan(0);
        RuleFor(query => query.MartStoreId).GreaterThan(0);

        When(query => query.RedemptionAmount.HasValue, () =>
        {
            RuleFor(query => query.RedemptionAmount!.Value)
                .GreaterThan(0)
                .LessThanOrEqualTo(MaximumAmount)
                .Must(amount => decimal.Round(amount, 2) == amount)
                .WithMessage("Redemption amount must have no more than two decimal places.");
            RuleFor(query => query.WalletTypeId)
                .NotNull()
                .GreaterThan(0);
        });

        When(query => query.WalletTypeId.HasValue, () =>
        {
            RuleFor(query => query.RedemptionAmount)
                .NotNull()
                .WithMessage("Redemption amount is required when a wallet type is provided.");
        });
    }
}
