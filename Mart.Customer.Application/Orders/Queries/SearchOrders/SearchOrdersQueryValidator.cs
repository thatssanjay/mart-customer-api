using FluentValidation;

namespace Mart.Customer.Application.Orders.Queries.SearchOrders;

public sealed class SearchOrdersQueryValidator : AbstractValidator<SearchOrdersQuery>
{
    public SearchOrdersQueryValidator()
    {
        RuleFor(query => query.FranchiseId).GreaterThan(0);
        RuleFor(query => query.MartStoreId).GreaterThan(0);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.CustomerName).MaximumLength(300);
        RuleFor(query => query.MobileNumber).MaximumLength(20);
        RuleFor(query => query.InvoiceNumber).MaximumLength(40);
    }
}
