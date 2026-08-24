using FluentValidation;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletBuckets;

public sealed class GetCustomerWalletBucketsQueryValidator
    : AbstractValidator<GetCustomerWalletBucketsQuery>
{
    public GetCustomerWalletBucketsQueryValidator()
    {
        RuleFor(query => query.CustomerId)
            .GreaterThan(0);

        RuleFor(query => query.WalletTypeId)
            .GreaterThan(0);
    }
}
