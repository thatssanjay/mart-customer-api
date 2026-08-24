using Mart.Customer.Application.Orders.Commands.CheckoutOrder;
using Mart.Customer.Application.Orders.Dtos;

namespace Mart.Customer.Application.Orders.Services;

public interface IOrderCheckoutService
{
    Task<OrderCheckoutDto?> CheckoutAsync(
        CheckoutOrderCommand command,
        CancellationToken cancellationToken = default);
}
