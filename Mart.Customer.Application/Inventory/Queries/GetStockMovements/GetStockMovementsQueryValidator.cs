using FluentValidation;

namespace Mart.Customer.Application.Inventory.Queries.GetStockMovements;

public sealed class GetStockMovementsQueryValidator : AbstractValidator<GetStockMovementsQuery>
{
    public GetStockMovementsQueryValidator()
    {
        RuleFor(query => query.FranchiseId).GreaterThan(0);
        RuleFor(query => query.MartStoreId).GreaterThan(0);
        RuleFor(query => query.ProductId)
            .GreaterThan(0)
            .When(query => query.ProductId.HasValue);
        RuleFor(query => query.MovementType)
            .MaximumLength(50)
            .When(query => !string.IsNullOrWhiteSpace(query.MovementType));
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate)
            .When(query => query.FromDate.HasValue && query.ToDate.HasValue);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
