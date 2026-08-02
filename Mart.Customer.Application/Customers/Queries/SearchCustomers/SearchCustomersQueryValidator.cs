using FluentValidation;

namespace Mart.Customer.Application.Customers.Queries.SearchCustomers;

public sealed class SearchCustomersQueryValidator : AbstractValidator<SearchCustomersQuery>
{
    public SearchCustomersQueryValidator()
    {
        RuleFor(query => query.Search)
            .NotEmpty()
            .MaximumLength(200);
    }
}
