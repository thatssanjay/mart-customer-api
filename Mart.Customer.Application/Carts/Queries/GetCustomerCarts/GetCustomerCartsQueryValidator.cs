using FluentValidation;

namespace Mart.Customer.Application.Carts.Queries.GetCustomerCarts;

public sealed class GetCustomerCartsQueryValidator : AbstractValidator<GetCustomerCartsQuery>
{
    public GetCustomerCartsQueryValidator()
    {
        RuleFor(query => query.CustomerId).GreaterThan(0);
        RuleFor(query => query.CartStatus).NotEmpty().MaximumLength(30);
    }
}
