using FluentValidation;

namespace Mart.Customer.Application.Inventory.Commands.StockOut;

public sealed class StockOutCommandValidator : AbstractValidator<StockOutCommand>
{
    private const decimal MaximumQuantity = 999999999999999.999m;

    public StockOutCommandValidator()
    {
        RuleFor(command => command.UserId).GreaterThan(0);
        RuleFor(command => command.ProductId).GreaterThan(0);
        RuleFor(command => command.Quantity)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaximumQuantity)
            .Must(quantity => decimal.Round(quantity, 3) == quantity)
            .WithMessage("Quantity must have no more than three decimal places.");
        RuleFor(command => command.MovementType)
            .NotEmpty()
            .MaximumLength(50);
        RuleFor(command => command.BatchNumber)
            .MaximumLength(100)
            .When(command => command.BatchNumber is not null);
        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(500);
        RuleFor(command => command.Remarks)
            .MaximumLength(500)
            .When(command => command.Remarks is not null);
    }
}
