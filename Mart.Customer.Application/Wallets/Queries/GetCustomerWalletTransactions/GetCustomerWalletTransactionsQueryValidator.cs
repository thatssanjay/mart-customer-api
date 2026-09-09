using FluentValidation;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletTransactions;

public sealed class GetCustomerWalletTransactionsQueryValidator
    : AbstractValidator<GetCustomerWalletTransactionsQuery>
{
    public GetCustomerWalletTransactionsQueryValidator()
    {
        RuleFor(query => query.CustomerId)
            .GreaterThan(0);

        RuleFor(query => query.WalletTypeId)
            .GreaterThan(0);

        RuleFor(query => query.StoreId)
            .GreaterThan(0)
            .When(query => query.StoreId.HasValue);

        RuleFor(query => query.PageNumber)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.TransactionType)
            .MaximumLength(30)
            .When(query => !string.IsNullOrWhiteSpace(query.TransactionType));

        RuleFor(query => query.ReferenceType)
            .MaximumLength(30)
            .When(query => !string.IsNullOrWhiteSpace(query.ReferenceType));

        RuleFor(query => query.ReferenceId)
            .GreaterThan(0)
            .When(query => query.ReferenceId.HasValue);

        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate)
            .When(query => query.FromDate.HasValue && query.ToDate.HasValue);
    }
}
