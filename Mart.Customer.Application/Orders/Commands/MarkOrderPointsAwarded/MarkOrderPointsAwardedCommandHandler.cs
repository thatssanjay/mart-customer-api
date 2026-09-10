using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Commands.MarkOrderPointsAwarded;

public sealed class MarkOrderPointsAwardedCommandHandler(ICustomerOrderRepository orderRepository)
    : IRequestHandler<MarkOrderPointsAwardedCommand, MarkOrderPointsAwardedResult>
{
    public async Task<MarkOrderPointsAwardedResult> Handle(
        MarkOrderPointsAwardedCommand request,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetPointsAwardStateAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return new MarkOrderPointsAwardedResult(MarkOrderPointsAwardedStatus.NotFound);
        }

        if (order.FranchiseId != request.FranchiseId || order.StoreId != request.StoreId)
        {
            return new MarkOrderPointsAwardedResult(MarkOrderPointsAwardedStatus.Forbidden);
        }

        if (order.IsPointsAwarded)
        {
            return new MarkOrderPointsAwardedResult(MarkOrderPointsAwardedStatus.AlreadyAwarded);
        }

        if (!await orderRepository.TryMarkPointsAwardedAsync(
                request.OrderId,
                request.FranchiseId,
                request.StoreId,
                cancellationToken))
        {
            order = await orderRepository.GetPointsAwardStateAsync(request.OrderId, cancellationToken);
            if (order is null)
            {
                return new MarkOrderPointsAwardedResult(MarkOrderPointsAwardedStatus.NotFound);
            }

            if (order.FranchiseId != request.FranchiseId || order.StoreId != request.StoreId)
            {
                return new MarkOrderPointsAwardedResult(MarkOrderPointsAwardedStatus.Forbidden);
            }

            return new MarkOrderPointsAwardedResult(MarkOrderPointsAwardedStatus.AlreadyAwarded);
        }

        order = await orderRepository.GetPointsAwardStateAsync(request.OrderId, cancellationToken);
        return new MarkOrderPointsAwardedResult(
            MarkOrderPointsAwardedStatus.Updated,
            new OrderPointsAwardedDto(
                order!.OrderId,
                order.IsPointsAwarded,
                order.PointsAwardedDate,
                order.PointsAwardedBy));
    }
}
