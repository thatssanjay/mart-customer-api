using FluentValidation;

namespace Mart.Customer.Application.Auth.Commands.GenerateInternalUserToken;

public sealed class GenerateInternalUserTokenCommandValidator : AbstractValidator<GenerateInternalUserTokenCommand>
{
    public GenerateInternalUserTokenCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(200);
    }
}
