using FluentValidation;

namespace Mart.Customer.Application.Inventory.Queries.GetLowStockProducts;

public sealed class GetLowStockProductsQueryValidator : AbstractValidator<GetLowStockProductsQuery>
{
    public GetLowStockProductsQueryValidator()
    {
        RuleFor(query => query.FranchiseId).GreaterThan(0);
        RuleFor(query => query.MartStoreId).GreaterThan(0);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
