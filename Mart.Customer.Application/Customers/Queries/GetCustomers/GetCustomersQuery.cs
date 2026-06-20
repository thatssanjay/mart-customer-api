using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Queries.GetCustomers;

public sealed record GetCustomersQuery : IRequest<IReadOnlyList<CustomerDto>>;
