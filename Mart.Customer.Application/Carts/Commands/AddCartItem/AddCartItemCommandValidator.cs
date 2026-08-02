using FluentValidation;

namespace Mart.Customer.Application.Carts.Commands.AddCartItem;

public sealed class AddCartItemCommandValidator : AbstractValidator<AddCartItemCommand>
{
    public AddCartItemCommandValidator()
    {
        RuleFor(command => command.CartNumber).NotEmpty().MaximumLength(50);
        RuleFor(command => command.FranchiseId).GreaterThan(0);
        RuleFor(command => command.MartStoreId).GreaterThan(0);
        RuleFor(command => command.AddedByCashierId).GreaterThan(0);
        RuleFor(command => command.ProductId).GreaterThan(0);
        RuleFor(command => command.ProductNameSnapshot).NotEmpty().MaximumLength(250);
        RuleFor(command => command.Quantity).GreaterThan(0);
        RuleFor(command => command.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MRP).GreaterThanOrEqualTo(0);
        RuleFor(command => command.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(command => command.GSTPercent).InclusiveBetween(0, 100);
        RuleFor(command => command)
            .Must(command => command.DiscountAmount <= command.Quantity * command.UnitPrice)
            .WithMessage("Discount amount cannot exceed the item gross amount.");
    }
}
