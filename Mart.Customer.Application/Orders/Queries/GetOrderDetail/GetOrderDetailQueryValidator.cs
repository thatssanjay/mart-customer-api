using FluentValidation;

namespace Mart.Customer.Application.Orders.Queries.GetOrderDetail;

public sealed class GetOrderDetailQueryValidator : AbstractValidator<GetOrderDetailQuery>
{
    public GetOrderDetailQueryValidator()
    {
        RuleFor(query => query.CustomerOrderId).GreaterThan(0);
        RuleFor(query => query.AccessScope).NotNull();
        RuleFor(query => query.AccessScope)
            .Must(scope =>
                scope.CustomerId is > 0 ||
                scope.FranchiseId is > 0 && scope.MartStoreId is > 0)
            .WithMessage("A valid customer or staff access scope is required.");
    }
}
