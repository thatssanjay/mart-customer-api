using FluentValidation;

namespace Mart.Customer.Application.Orders.Queries.VerifyOrder;

public sealed class VerifyOrderQueryValidator : AbstractValidator<VerifyOrderQuery>
{
    public VerifyOrderQueryValidator()
    {
        RuleFor(query => query.VerificationCode)
            .NotEmpty()
            .MaximumLength(36);
    }
}
