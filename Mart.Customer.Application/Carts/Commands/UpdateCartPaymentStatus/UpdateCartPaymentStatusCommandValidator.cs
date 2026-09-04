using FluentValidation;

namespace Mart.Customer.Application.Carts.Commands.UpdateCartPaymentStatus;

public sealed class UpdateCartPaymentStatusCommandValidator : AbstractValidator<UpdateCartPaymentStatusCommand>
{
    public UpdateCartPaymentStatusCommandValidator()
    {
        RuleFor(command => command.CartId).GreaterThan(0);
        RuleFor(command => command.CustomerId).GreaterThan(0);
        RuleFor(command => command.Status).NotEmpty().MaximumLength(30);
    }
}
