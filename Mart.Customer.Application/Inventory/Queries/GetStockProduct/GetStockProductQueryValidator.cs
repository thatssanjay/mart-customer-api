using FluentValidation;

namespace Mart.Customer.Application.Inventory.Queries.GetStockProduct;

public sealed class GetStockProductQueryValidator : AbstractValidator<GetStockProductQuery>
{
    public GetStockProductQueryValidator()
    {
        RuleFor(query => query.ProductId).GreaterThan(0);
        RuleFor(query => query.FranchiseId).GreaterThan(0);
        RuleFor(query => query.MartStoreId).GreaterThan(0);
    }
}
