using FluentValidation;

namespace Mart.Customer.Application.Orders.Queries.GetOrderDetailByInvoiceNumber;

public sealed class GetOrderDetailByInvoiceNumberQueryValidator
    : AbstractValidator<GetOrderDetailByInvoiceNumberQuery>
{
    public GetOrderDetailByInvoiceNumberQueryValidator()
    {
        RuleFor(query => query.InvoiceNumber)
            .NotEmpty()
            .MaximumLength(50);
        RuleFor(query => query.AccessScope).NotNull();
        RuleFor(query => query.AccessScope)
            .Must(scope =>
                scope.CustomerId is > 0 ||
                scope.FranchiseId is > 0 && scope.MartStoreId is > 0)
            .WithMessage("A valid customer or staff access scope is required.");
    }
}
