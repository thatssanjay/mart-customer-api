using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Referrals.Dtos;
using MediatR;

namespace Mart.Customer.Application.Referrals.Queries.GetCustomerReferrals;

public sealed record GetCustomerReferralsQuery(long CustomerId, int PageNumber = 1, int PageSize = 10)
    : IRequest<PagedResultDto<CustomerReferralDto>>;
