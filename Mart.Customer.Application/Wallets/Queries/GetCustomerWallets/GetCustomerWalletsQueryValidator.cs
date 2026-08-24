using FluentValidation;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWallets;

public sealed class GetCustomerWalletsQueryValidator
    : AbstractValidator<GetCustomerWalletsQuery>
{
    public GetCustomerWalletsQueryValidator()
    {
        RuleFor(query => query.CustomerId)
            .GreaterThan(0);
    }
}
