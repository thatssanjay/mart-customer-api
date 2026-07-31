using FluentValidation;

namespace Mart.Customer.Application.Auth.Commands.GenerateCustomerToken;

public sealed class GenerateCustomerTokenCommandValidator : AbstractValidator<GenerateCustomerTokenCommand>
{
    public GenerateCustomerTokenCommandValidator()
    {
        RuleFor(command => command.MobileNumber)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(command => command.Otp)
            .NotEmpty()
            .MaximumLength(10);
    }
}
