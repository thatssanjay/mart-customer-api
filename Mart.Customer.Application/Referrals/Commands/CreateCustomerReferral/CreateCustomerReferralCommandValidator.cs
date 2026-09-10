using FluentValidation;

namespace Mart.Customer.Application.Referrals.Commands.CreateCustomerReferral;

public sealed class CreateCustomerReferralCommandValidator : AbstractValidator<CreateCustomerReferralCommand>
{
    public CreateCustomerReferralCommandValidator()
    {
        RuleFor(command => command.ReferrerCustomerId).GreaterThan(0);
        RuleFor(command => command.ReferralConfigId)
            .GreaterThan(0)
            .WithMessage("Referral configuration is required.");
        RuleFor(command => command.ReferredMobileNumber)
            .NotEmpty()
            .Matches("^[0-9]{10}$")
            .WithMessage("Enter a valid 10 digit mobile number.");
    }
}
