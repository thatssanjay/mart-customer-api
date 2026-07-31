using FluentValidation;

namespace Mart.Customer.Application.Customers.Queries.GetCustomers;

public sealed class GetCustomersQueryValidator : AbstractValidator<GetCustomersQuery>
{
    public GetCustomersQueryValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
