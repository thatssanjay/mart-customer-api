using FluentValidation;

namespace Mart.Customer.Application.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(command => command.CustomerCode)
            .MaximumLength(50);

        RuleFor(command => command.FirstName)
            .MaximumLength(100);

        RuleFor(command => command.LastName)
            .MaximumLength(100);

        RuleFor(command => command.DisplayName)
            .MaximumLength(200);

        RuleFor(command => command.MobileNumber)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(command => command.Email)
            .EmailAddress()
            .MaximumLength(256)
            .When(command => !string.IsNullOrWhiteSpace(command.Email));

        RuleFor(command => command.Gender)
            .MaximumLength(30);

        RuleFor(command => command.AddressLine1)
            .MaximumLength(250);

        RuleFor(command => command.AddressLine2)
            .MaximumLength(250);

        RuleFor(command => command.City)
            .MaximumLength(100);

        RuleFor(command => command.State)
            .MaximumLength(100);

        RuleFor(command => command.Country)
            .MaximumLength(100);

        RuleFor(command => command.PinCode)
            .MaximumLength(20);

        RuleFor(command => command.PreferredLanguage)
            .MaximumLength(50);

        RuleFor(command => command.RegistrationSource)
            .MaximumLength(100);
    }
}
