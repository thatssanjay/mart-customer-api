using FluentValidation;

namespace Mart.Customer.Application.Carts.Commands.ChangeCartStatus;

public sealed class ChangeCartStatusCommandValidator : AbstractValidator<ChangeCartStatusCommand>
{
    public ChangeCartStatusCommandValidator()
    {
        RuleFor(command => command.CartNumber).NotEmpty().MaximumLength(50);
        RuleFor(command => command.CartStatus).NotEmpty().MaximumLength(30);
        RuleFor(command => command.FranchiseId).GreaterThan(0);
        RuleFor(command => command.MartStoreId).GreaterThan(0);
    }
}
