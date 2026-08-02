using FluentValidation;

namespace Mart.Customer.Application.Carts.Commands.DeleteCartItemsByProduct;

public sealed class DeleteCartItemsByProductCommandValidator
    : AbstractValidator<DeleteCartItemsByProductCommand>
{
    public DeleteCartItemsByProductCommandValidator()
    {
        RuleFor(command => command.CartNumber).NotEmpty().MaximumLength(50);
        RuleFor(command => command.ProductId).GreaterThan(0);
        RuleFor(command => command.FranchiseId).GreaterThan(0);
        RuleFor(command => command.MartStoreId).GreaterThan(0);
    }
}
