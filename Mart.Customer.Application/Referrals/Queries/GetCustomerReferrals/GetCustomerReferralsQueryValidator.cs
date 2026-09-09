using FluentValidation;

namespace Mart.Customer.Application.Referrals.Queries.GetCustomerReferrals;

public sealed class GetCustomerReferralsQueryValidator : AbstractValidator<GetCustomerReferralsQuery>
{
    public GetCustomerReferralsQueryValidator()
    {
        RuleFor(query => query.CustomerId).GreaterThan(0);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 10);
    }
}
