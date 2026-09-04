using FluentValidation;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletBalances;

public sealed class GetCustomerWalletBalancesQueryValidator
    : AbstractValidator<GetCustomerWalletBalancesQuery>
{
    public GetCustomerWalletBalancesQueryValidator()
    {
        RuleFor(query => query.CustomerId).GreaterThan(0);
    }
}
