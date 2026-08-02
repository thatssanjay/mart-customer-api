using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Queries.SearchCustomers;

public sealed record SearchCustomersQuery(string? Search)
    : IRequest<IReadOnlyList<CustomerLookupDto>>;
