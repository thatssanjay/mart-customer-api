using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.VerifyOrder;

public sealed class VerifyOrderQueryHandler
    : IRequestHandler<VerifyOrderQuery, OrderVerificationDto?>
{
    private readonly ICustomerOrderRepository _orderRepository;

    public VerifyOrderQueryHandler(ICustomerOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public Task<OrderVerificationDto?> Handle(
        VerifyOrderQuery request,
        CancellationToken cancellationToken)
    {
        return Guid.TryParse(request.VerificationCode.Trim(), out var verificationCode)
            ? _orderRepository.GetVerificationAsync(
                verificationCode.ToString("N"),
                cancellationToken)
            : Task.FromResult<OrderVerificationDto?>(null);
    }
}
