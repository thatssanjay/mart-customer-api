using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Commands.MarkOrderPointsAwarded;

public sealed record MarkOrderPointsAwardedCommand(long OrderId, long FranchiseId, long StoreId)
    : IRequest<MarkOrderPointsAwardedResult>;
