using FluentValidation;

namespace Mart.Customer.Application.Carts.Commands.DeleteCart;

public sealed class DeleteCartCommandValidator : AbstractValidator<DeleteCartCommand>
{
    public DeleteCartCommandValidator()
    {
        RuleFor(command => command.CartNumber)
            .NotEmpty()
            .MaximumLength(50);
        RuleFor(command => command.FranchiseId).GreaterThan(0);
        RuleFor(command => command.MartStoreId).GreaterThan(0);
    }
}
