using FluentValidation;

namespace Mart.Customer.Application.Customers.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(command => command.CustomerId)
            .GreaterThan(0);

        RuleFor(command => command.DisplayName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.MobileNumber)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(command => command.Email)
            .EmailAddress()
            .MaximumLength(256)
            .When(command => !string.IsNullOrWhiteSpace(command.Email));

        RuleFor(command => command.AddressLine1)
            .MaximumLength(250);
    }
}
