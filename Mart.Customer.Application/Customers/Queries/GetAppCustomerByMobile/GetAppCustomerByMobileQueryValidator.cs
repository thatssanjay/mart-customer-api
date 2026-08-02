using FluentValidation;

namespace Mart.Customer.Application.Customers.Queries.GetAppCustomerByMobile;

public sealed class GetAppCustomerByMobileQueryValidator : AbstractValidator<GetAppCustomerByMobileQuery>
{
    public GetAppCustomerByMobileQueryValidator()
    {
        RuleFor(query => query.MobileNumber).NotEmpty().MaximumLength(30);
    }
}
