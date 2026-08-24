using FluentValidation;

namespace Mart.Customer.Application.Carts.Commands.CancelCart;

public sealed class CancelCartCommandValidator : AbstractValidator<CancelCartCommand>
{
    public CancelCartCommandValidator()
    {
        RuleFor(command => command.CustomerCartId).GreaterThan(0);
        RuleFor(command => command.Remarks).MaximumLength(500);
        RuleFor(command => command.FranchiseId).GreaterThan(0);
        RuleFor(command => command.MartStoreId).GreaterThan(0);
    }
}
