using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Application.Orders.Services;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.GetOrderCheckoutPreview;

public sealed class GetOrderCheckoutPreviewQueryHandler
    : IRequestHandler<GetOrderCheckoutPreviewQuery, OrderCheckoutPreviewDto?>
{
    private readonly ICustomerCartRepository _cartRepository;
    private readonly IOrderCheckoutCalculator _checkoutCalculator;

    public GetOrderCheckoutPreviewQueryHandler(
        ICustomerCartRepository cartRepository,
        IOrderCheckoutCalculator checkoutCalculator)
    {
        _cartRepository = cartRepository;
        _checkoutCalculator = checkoutCalculator;
    }

    public async Task<OrderCheckoutPreviewDto?> Handle(
        GetOrderCheckoutPreviewQuery request,
        CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByCartNumberForCheckoutPreviewAsync(
            request.CartNumber!.Trim(),
            request.FranchiseId,
            request.MartStoreId,
            cancellationToken);
        if (cart is null)
        {
            return null;
        }

        return await _checkoutCalculator.CalculateAsync(
            cart,
            request.WalletTypeId,
            request.RedemptionAmount,
            cancellationToken);
    }
}
