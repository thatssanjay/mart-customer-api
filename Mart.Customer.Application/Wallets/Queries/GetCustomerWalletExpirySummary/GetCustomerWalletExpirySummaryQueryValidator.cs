using FluentValidation;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletExpirySummary;

public sealed class GetCustomerWalletExpirySummaryQueryValidator
    : AbstractValidator<GetCustomerWalletExpirySummaryQuery>
{
    public GetCustomerWalletExpirySummaryQueryValidator()
    {
        RuleFor(query => query.CustomerId)
            .GreaterThan(0);

        RuleFor(query => query.WalletTypeId)
            .GreaterThan(0);
    }
}
