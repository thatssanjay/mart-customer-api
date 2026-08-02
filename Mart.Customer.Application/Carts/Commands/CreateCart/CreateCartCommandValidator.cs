using FluentValidation;

namespace Mart.Customer.Application.Carts.Commands.CreateCart;

public sealed class CreateCartCommandValidator : AbstractValidator<CreateCartCommand>
{
    public CreateCartCommandValidator()
    {
        RuleFor(command => command.CustomerId).GreaterThan(0);
        RuleFor(command => command.FranchiseId).GreaterThan(0);
        RuleFor(command => command.MartStoreId).GreaterThan(0);
        RuleFor(command => command.AddedByCashierId).GreaterThan(0);
    }
}
