using Mart.Customer.Application.Auth.Dtos;
using MediatR;

namespace Mart.Customer.Application.Auth.Queries.GetMartUserAccessScope;

public sealed record GetMartUserAccessScopeQuery(long UserId) : IRequest<MartUserAccessScopeDto>;
