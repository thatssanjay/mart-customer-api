using FluentValidation;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletDetail;

public sealed class GetCustomerWalletDetailQueryValidator
    : AbstractValidator<GetCustomerWalletDetailQuery>
{
    public GetCustomerWalletDetailQueryValidator()
    {
        RuleFor(query => query.CustomerId)
            .GreaterThan(0);

        RuleFor(query => query.WalletTypeId)
            .GreaterThan(0);
    }
}
