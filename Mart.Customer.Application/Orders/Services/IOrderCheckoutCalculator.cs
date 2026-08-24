using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Domain.Carts;

namespace Mart.Customer.Application.Orders.Services;

public interface IOrderCheckoutCalculator
{
    Task<OrderCheckoutPreviewDto> CalculateAsync(
        CustomerCart cart,
        int? walletTypeId,
        decimal? redemptionAmount,
        CancellationToken cancellationToken = default);
}
