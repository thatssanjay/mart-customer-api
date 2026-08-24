using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Domain.Common;
using MediatR;

namespace Mart.Customer.Application.Auth.Queries.GetMartUserAccessScope;

public sealed class GetMartUserAccessScopeQueryHandler
    : IRequestHandler<GetMartUserAccessScopeQuery, MartUserAccessScopeDto>
{
    private readonly IInternalUserRepository _internalUserRepository;

    public GetMartUserAccessScopeQueryHandler(IInternalUserRepository internalUserRepository)
    {
        _internalUserRepository = internalUserRepository;
    }

    public async Task<MartUserAccessScopeDto> Handle(
        GetMartUserAccessScopeQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserId <= 0)
        {
            throw new DomainException("The authenticated token does not contain a valid user id.");
        }

        return await _internalUserRepository.GetAccessScopeAsync(request.UserId, cancellationToken)
            ?? throw new DomainException("The user does not have an active franchise and store assignment.");
    }
}
