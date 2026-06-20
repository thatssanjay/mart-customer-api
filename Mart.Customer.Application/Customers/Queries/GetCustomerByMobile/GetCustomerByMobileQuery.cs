using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Queries.GetCustomerByMobile;

public sealed record GetCustomerByMobileQuery(string MobileNumber) : IRequest<CustomerContactDto?>;
