using FluentValidation;

namespace Mart.Customer.Application.Wallets.Queries.GetWalletTransactionByNumber;

public sealed class GetWalletTransactionByNumberQueryValidator
    : AbstractValidator<GetWalletTransactionByNumberQuery>
{
    public GetWalletTransactionByNumberQueryValidator()
    {
        RuleFor(query => query.TransactionNumber)
            .NotEmpty()
            .MaximumLength(50);
    }
}
