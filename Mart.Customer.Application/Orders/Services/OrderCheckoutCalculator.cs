using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Application.Wallets.Services;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Domain.Common;

namespace Mart.Customer.Application.Orders.Services;

public sealed class OrderCheckoutCalculator : IOrderCheckoutCalculator
{
    private readonly IWalletRedemptionPreviewService _walletRedemptionPreviewService;

    public OrderCheckoutCalculator(IWalletRedemptionPreviewService walletRedemptionPreviewService)
    {
        _walletRedemptionPreviewService = walletRedemptionPreviewService;
    }

    public async Task<OrderCheckoutPreviewDto> CalculateAsync(
        CustomerCart cart,
        int? walletTypeId,
        decimal? redemptionAmount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cart);

        if (!string.Equals(cart.CartStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Only an active cart can be checked out.");
        }

        if (cart.Items.Count == 0)
        {
            throw new DomainException("The cart must contain at least one item before checkout.");
        }

        var items = cart.Items
            .OrderBy(item => item.AddedOn)
            .ThenBy(item => item.CustomerCartItemId)
            .Select(item =>
            {
                var grossAmount = RoundMoney(item.Quantity * item.UnitPrice);
                var taxableAmount = grossAmount - item.DiscountAmount;
                var gstAmount = RoundMoney(taxableAmount * item.GSTPercent / 100m);
                var lineTotal = taxableAmount + gstAmount;

                return new OrderCheckoutPreviewItemDto(
                    item.CustomerCartItemId,
                    item.ProductId,
                    item.ProductNameSnapshot,
                    item.Quantity,
                    item.UnitPrice,
                    item.MRP,
                    grossAmount,
                    item.DiscountAmount,
                    item.GSTPercent,
                    gstAmount,
                    lineTotal);
            })
            .ToList();

        var grossAmount = items.Sum(item => item.GrossAmount);
        var discountAmount = items.Sum(item => item.DiscountAmount);
        var gstAmount = items.Sum(item => item.GSTAmount);
        var netAmount = items.Sum(item => item.LineTotal);
        var requestedRedemption = redemptionAmount ?? 0m;

        if (walletTypeId.HasValue != redemptionAmount.HasValue)
        {
            throw new DomainException("Wallet type and redemption amount must be provided together.");
        }

        if (redemptionAmount.HasValue && requestedRedemption <= 0m)
        {
            throw new DomainException("Redemption amount must be greater than zero.");
        }

        if (redemptionAmount.HasValue && decimal.Round(requestedRedemption, 2) != requestedRedemption)
        {
            throw new DomainException("Redemption amount must have no more than two decimal places.");
        }

        if (walletTypeId.HasValue && walletTypeId.Value <= 0)
        {
            throw new DomainException("Wallet type must be greater than zero.");
        }

        if (requestedRedemption > netAmount)
        {
            throw new DomainException("Redemption amount cannot exceed the cart net amount.");
        }

        RedeemPreviewResultDto? walletRedemption = null;
        if (requestedRedemption > 0m)
        {
            walletRedemption = await _walletRedemptionPreviewService.PreviewAsync(
                cart.CustomerId,
                walletTypeId!.Value,
                requestedRedemption,
                cancellationToken,
                cart.MartStoreId);
        }

        return new OrderCheckoutPreviewDto(
            cart.CustomerCartId,
            cart.CartNumber,
            cart.CustomerId,
            cart.FranchiseId,
            cart.MartStoreId,
            cart.CartStatus,
            items.Count,
            grossAmount,
            discountAmount,
            gstAmount,
            netAmount,
            requestedRedemption,
            netAmount - requestedRedemption,
            items,
            walletRedemption);
    }

    private static decimal RoundMoney(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
