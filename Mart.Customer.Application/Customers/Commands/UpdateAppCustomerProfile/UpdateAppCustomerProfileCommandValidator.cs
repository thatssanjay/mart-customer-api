using FluentValidation;

namespace Mart.Customer.Application.Customers.Commands.UpdateAppCustomerProfile;

public sealed class UpdateAppCustomerProfileCommandValidator : AbstractValidator<UpdateAppCustomerProfileCommand>
{
    public UpdateAppCustomerProfileCommandValidator()
    {
        RuleFor(command => command.UserId).GreaterThan(0);
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.EmailAddress)
            .EmailAddress().MaximumLength(256)
            .When(command => !string.IsNullOrWhiteSpace(command.EmailAddress));
        RuleFor(command => command.Address).MaximumLength(250);
    }
}
