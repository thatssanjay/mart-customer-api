using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Carts.Dtos;
using Mart.Customer.Domain.Common;
using MediatR;

namespace Mart.Customer.Application.Carts.Queries.GetCustomerCarts;

public sealed class GetCustomerCartsQueryHandler
    : IRequestHandler<GetCustomerCartsQuery, IReadOnlyList<CartDetailsDto>>
{
    private readonly ICustomerCartRepository _cartRepository;

    public GetCustomerCartsQueryHandler(ICustomerCartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public async Task<IReadOnlyList<CartDetailsDto>> Handle(
        GetCustomerCartsQuery request,
        CancellationToken cancellationToken)
    {
        var status = request.CartStatus!.Trim();
        if (request.CartNumber is not null)
        {
            status = status.ToUpperInvariant() switch
            {
                "ACTIVE" => "Active",
                "PAYMENTPENDING" => "PaymentPending",
                "CUSTOMERAPPROVED" => "CustomerApproved",
                "PAID" => "Paid",
                "CANCELLED" => "Cancelled",
                _ => status
            };
        }

        var carts = await _cartRepository.GetByCustomerAndStatusAsync(
            request.CustomerId,
            status,
            request.FranchiseId,
            request.StoreId,
            cancellationToken,
            request.CartNumber?.Trim());

        if (request.CartNumber is not null && carts.Count == 0)
        {
            // Use the same response for absent carts, other owners, and status mismatches.
            throw new DomainException("Invalid cart number.");
        }

        return carts.Select(cart => new CartDetailsDto(
            cart.CustomerCartId,
            cart.CustomerId,
            cart.FranchiseId,
            cart.MartStoreId,
            cart.CartNumber,
            cart.CartStatus,
            cart.TotalItemCount,
            cart.GrossAmount,
            cart.DiscountAmount,
            cart.GSTAmount,
            cart.NetAmount,
            cart.RewardPointsToRedeem,
            cart.RedeemAmount,
            cart.FinalPayableAmount,
            cart.AddedByCashierId,
            cart.CustomerApprovedOn,
            cart.PaidOn,
            cart.CancelledOn,
            cart.Remarks,
            cart.CreatedOn,
            cart.ModifiedOn,
            cart.Items
                .OrderBy(item => item.AddedOn)
                .Select(item => new CartItemDetailsDto(
                    item.CustomerCartItemId,
                    item.CustomerCartId,
                    item.ProductId,
                    item.ProductNameSnapshot,
                    item.Quantity,
                    item.UnitPrice,
                    item.MRP,
                    item.DiscountAmount,
                    item.GSTPercent,
                    item.GSTAmount,
                    item.LineTotal,
                    item.AddedByCashierId,
                    item.AddedOn))
                .ToList()))
            .ToList();
    }
}
