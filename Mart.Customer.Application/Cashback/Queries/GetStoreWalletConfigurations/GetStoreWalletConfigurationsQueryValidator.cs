using FluentValidation;

namespace Mart.Customer.Application.Cashback.Queries.GetStoreWalletConfigurations;

public sealed class GetStoreWalletConfigurationsQueryValidator
    : AbstractValidator<GetStoreWalletConfigurationsQuery>
{
    public GetStoreWalletConfigurationsQueryValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
