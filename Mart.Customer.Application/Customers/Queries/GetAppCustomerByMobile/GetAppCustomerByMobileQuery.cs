using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Queries.GetAppCustomerByMobile;

public sealed record GetAppCustomerByMobileQuery(string MobileNumber) : IRequest<AppCustomerProfileDto?>;
