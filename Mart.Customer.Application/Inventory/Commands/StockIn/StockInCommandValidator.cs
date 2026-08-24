using FluentValidation;

namespace Mart.Customer.Application.Inventory.Commands.StockIn;

public sealed class StockInCommandValidator : AbstractValidator<StockInCommand>
{
    private const decimal MaximumQuantity = 999999999999999.999m;
    private const decimal MaximumMoney = 9999999999999999.99m;

    public StockInCommandValidator()
    {
        RuleFor(command => command.UserId).GreaterThan(0);
        RuleFor(command => command.ProductId).GreaterThan(0);
        RuleFor(command => command.Quantity)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaximumQuantity)
            .Must(HasAtMostThreeDecimalPlaces)
            .WithMessage("Quantity must have no more than three decimal places.");
        RuleFor(command => command.MovementType)
            .NotEmpty()
            .MaximumLength(50);
        RuleFor(command => command.ReferenceType)
            .NotEmpty()
            .MaximumLength(50)
            .When(command => command.ReferenceId.HasValue);
        RuleFor(command => command.ReferenceId)
            .NotNull()
            .GreaterThan(0)
            .When(command => !string.IsNullOrWhiteSpace(command.ReferenceType));
        RuleFor(command => command.BatchNumber)
            .MaximumLength(100)
            .When(command => command.BatchNumber is not null);
        RuleFor(command => command.PurchasePrice).Must(BeValidMoney)
            .When(command => command.PurchasePrice.HasValue);
        RuleFor(command => command.SellingPrice).Must(BeValidMoney)
            .When(command => command.SellingPrice.HasValue);
        RuleFor(command => command.Mrp).Must(BeValidMoney)
            .When(command => command.Mrp.HasValue);
        RuleFor(command => command.Remarks)
            .MaximumLength(500)
            .When(command => command.Remarks is not null);
        RuleFor(command => command.AdjustmentReason)
            .MaximumLength(500)
            .When(command => command.AdjustmentReason is not null);
    }

    private static bool HasAtMostThreeDecimalPlaces(decimal value) =>
        decimal.Round(value, 3) == value;

    private static bool BeValidMoney(decimal? value) =>
        value is >= 0 and <= MaximumMoney && decimal.Round(value.Value, 2) == value.Value;
}
