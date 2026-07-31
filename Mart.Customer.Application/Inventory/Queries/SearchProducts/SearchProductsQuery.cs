using Mart.Customer.Application.Inventory.Dtos;
using MediatR;

namespace Mart.Customer.Application.Inventory.Queries.SearchProducts;

public sealed record SearchProductsQuery(string? Search) : IRequest<IReadOnlyList<ProductListItemDto>>;
