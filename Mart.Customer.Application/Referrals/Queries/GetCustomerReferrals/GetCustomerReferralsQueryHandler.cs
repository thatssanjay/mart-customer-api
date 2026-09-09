using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Referrals.Dtos;
using MediatR;

namespace Mart.Customer.Application.Referrals.Queries.GetCustomerReferrals;

public sealed class GetCustomerReferralsQueryHandler(ICustomerReferralRepository referrals)
    : IRequestHandler<GetCustomerReferralsQuery, PagedResultDto<CustomerReferralDto>>
{
    public async Task<PagedResultDto<CustomerReferralDto>> Handle(
        GetCustomerReferralsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await referrals.GetPagedAsync(
            request.CustomerId, request.PageNumber, request.PageSize, cancellationToken);
        return new PagedResultDto<CustomerReferralDto>(
            items,
            request.PageNumber,
            request.PageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)request.PageSize));
    }
}
