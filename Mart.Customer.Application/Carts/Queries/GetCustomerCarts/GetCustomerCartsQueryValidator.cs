using FluentValidation;

namespace Mart.Customer.Application.Carts.Queries.GetCustomerCarts;

public sealed class GetCustomerCartsQueryValidator : AbstractValidator<GetCustomerCartsQuery>
{
    public GetCustomerCartsQueryValidator()
    {
        RuleFor(query => query.CustomerId).GreaterThan(0);
        RuleFor(query => query.CartStatus).NotEmpty().MaximumLength(30);
        RuleFor(query => query.FranchiseId)
            .GreaterThan(0)
            .When(query => query.FranchiseId.HasValue || query.StoreId.HasValue);
        RuleFor(query => query.StoreId)
            .GreaterThan(0)
            .When(query => query.FranchiseId.HasValue || query.StoreId.HasValue);
    }
}
