using FluentValidation;

namespace Mart.Customer.Application.Orders.Queries.GetCustomerOrders;

public sealed class GetCustomerOrdersQueryValidator : AbstractValidator<GetCustomerOrdersQuery>
{
    public GetCustomerOrdersQueryValidator()
    {
        RuleFor(query => query.CustomerId)
            .GreaterThan(0);

        RuleFor(query => query.PageNumber)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
