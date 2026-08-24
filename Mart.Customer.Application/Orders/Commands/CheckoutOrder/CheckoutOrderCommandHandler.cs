using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Application.Orders.Services;
using MediatR;

namespace Mart.Customer.Application.Orders.Commands.CheckoutOrder;

public sealed class CheckoutOrderCommandHandler
    : IRequestHandler<CheckoutOrderCommand, OrderCheckoutDto?>
{
    private readonly IOrderCheckoutService _checkoutService;

    public CheckoutOrderCommandHandler(IOrderCheckoutService checkoutService)
    {
        _checkoutService = checkoutService;
    }

    public Task<OrderCheckoutDto?> Handle(
        CheckoutOrderCommand request,
        CancellationToken cancellationToken) =>
        _checkoutService.CheckoutAsync(request, cancellationToken);
}
