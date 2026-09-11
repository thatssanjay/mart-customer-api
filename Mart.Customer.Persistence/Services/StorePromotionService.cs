using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Common.Utilities;
using Mart.Customer.Application.Promotions;
using Mart.Customer.Application.Wallets.Engine;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Promotions;
using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Services;

internal sealed class StorePromotionService(
    ApplicationDbContext dbContext,
    IUnitOfWork unitOfWork,
    ICustomerWalletResolver walletResolver) : IStorePromotionService
{
    private const string AwardRemarks = "Store promotion awarded";

    public async Task<IReadOnlyList<PromotionOrderDto>> SearchTodayOrdersAsync(
        long franchiseId,
        long storeId,
        long? orderId,
        string? mobileNumber,
        CancellationToken cancellationToken = default)
    {
        if (franchiseId <= 0 || storeId <= 0)
            throw new DomainException("A valid franchise and store assignment is required.");
        if (orderId is null && string.IsNullOrWhiteSpace(mobileNumber))
            throw new DomainException("Enter an order ID or customer mobile number.");

        var start = DateTime.UtcNow.Date;
        var end = start.AddDays(1);
        var normalizedMobile = mobileNumber?.Trim();
        var query = dbContext.CustomerOrders.AsNoTracking()
            .Where(order => order.FranchiseId == franchiseId &&
                            order.MartStoreId == storeId &&
                            order.OrderDate >= start && order.OrderDate < end);

        if (orderId.HasValue)
            query = query.Where(order => order.CustomerOrderId == orderId.Value);
        if (!string.IsNullOrWhiteSpace(normalizedMobile))
            query = query.Where(order => order.CustomerMobileSnapshot == normalizedMobile);

        return await query
            .OrderByDescending(order => order.OrderDate)
            .Take(25)
            .Select(order => new PromotionOrderDto(
                order.CustomerOrderId,
                order.InvoiceNumber,
                order.OrderDate,
                order.CustomerId,
                order.CustomerNameSnapshot ?? "Customer",
                order.CustomerMobileSnapshot ?? string.Empty,
                order.FinalPayableAmount,
                order.OrderStatus,
                order.Status,
                order.Remarks))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActiveStorePromotionDto>> GetActivePromotionsAsync(
        long franchiseId,
        long storeId,
        CancellationToken cancellationToken = default)
    {
        if (franchiseId <= 0 || storeId <= 0)
            throw new DomainException("A valid franchise and store assignment is required.");

        var now = DateTime.UtcNow;
        return await (
            from promotion in dbContext.StorePromotions.AsNoTracking()
            join store in dbContext.MartStores.AsNoTracking()
                on promotion.StoreId equals store.StoreId
            join walletType in dbContext.WalletTypes.AsNoTracking()
                on promotion.WalletTypeId equals walletType.Id
            where store.FranchiseId == franchiseId && promotion.StoreId == storeId && promotion.IsActive &&
                  promotion.PromoCode != null &&
                  promotion.StartDate <= now && promotion.EndDate >= now &&
                  walletType.IsActive
            orderby promotion.PromoCode
            select new ActiveStorePromotionDto(
                promotion.PromoCode!,
                promotion.DiscountType,
                promotion.DiscountType == StorePromotionDiscountTypes.BillPercent
                    ? promotion.BillPercentDiscount ?? 0m
                    : promotion.BonusPoint,
                promotion.WalletTypeId,
                walletType.Name))
            .ToListAsync(cancellationToken);
    }

    public Task<PromotionAllocationDto> AllocateAsync(
        long userId,
        long franchiseId,
        long storeId,
        long orderId,
        string promoCode,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || franchiseId <= 0 || storeId <= 0)
            throw new DomainException("A valid authenticated franchise administrator is required.");
        if (orderId <= 0)
            throw new DomainException("Order ID must be greater than zero.");
        if (string.IsNullOrWhiteSpace(promoCode))
            throw new DomainException("Promo code is required.");

        return unitOfWork.ExecuteInTransactionAsync(
            token => AllocateWithinTransactionAsync(
                franchiseId, storeId, orderId, promoCode.Trim().ToUpperInvariant(), createdBy, token),
            cancellationToken);
    }

    private async Task<PromotionAllocationDto> AllocateWithinTransactionAsync(
        long franchiseId,
        long storeId,
        long orderId,
        string promoCode,
        string createdBy,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var start = now.Date;
        var end = start.AddDays(1);
        var order = await dbContext.CustomerOrders.SingleOrDefaultAsync(
            item => item.CustomerOrderId == orderId,
            cancellationToken) ?? throw new DomainException("Order not found.");

        if (order.FranchiseId != franchiseId || order.MartStoreId != storeId)
            throw new UnauthorizedAccessException("The order does not belong to the current franchise and store.");
        if (order.OrderDate < start || order.OrderDate >= end)
            throw new DomainException("Only today's orders are eligible for a store promotion.");

        var promotion = await dbContext.StorePromotions.AsNoTracking().SingleOrDefaultAsync(
            item => item.StoreId == storeId && item.PromoCode == promoCode,
            cancellationToken) ?? throw new DomainException("Promo code is not valid for the current store.");
        if (!promotion.IsActive || promotion.StartDate > now || promotion.EndDate < now)
            throw new DomainException("Promo code is inactive or outside its validity period.");

        var walletType = await dbContext.WalletTypes.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == promotion.WalletTypeId && item.IsActive,
            cancellationToken) ?? throw new DomainException("The configured promotion wallet type is inactive or unavailable.");

        var amount = CalculateAward(promotion, order.FinalPayableAmount);
        var wallet = await walletResolver.ResolveAsync(
            order.CustomerId,
            promotion.WalletTypeId,
            storeId,
            cancellationToken);
        var referenceType = $"STORE_PROMO:{promotion.Id}";
        var duplicate = await dbContext.WalletTransactions.AsNoTracking().AnyAsync(
            transaction => transaction.CustomerWalletId == wallet.CustomerWalletId &&
                           transaction.TransactionType == "CREDIT" &&
                           transaction.ReferenceType == referenceType &&
                           transaction.ReferenceId == orderId,
            cancellationToken);
        if (duplicate || order.Status == 1)
            throw new DomainException("This order promotion has already been awarded.");

        var balanceBefore = wallet.CurrentBalance;
        wallet.Credit(amount, now);
        var transaction = WalletTransaction.CreateCredit(
            ReferenceCodeGenerator.GenerateWithPrefix("TX"),
            wallet.CustomerWalletId,
            amount,
            balanceBefore,
            wallet.CurrentBalance,
            referenceType,
            orderId,
            $"{AwardRemarks} ({promoCode})",
            now,
            string.IsNullOrWhiteSpace(createdBy) ? "Franchise Admin" : createdBy.Trim());
        var bucket = WalletBalanceBucket.Create(wallet.CustomerWalletId, transaction, amount, null, now);

        order.MarkStorePromotionAwarded();
        await dbContext.WalletTransactions.AddAsync(transaction, cancellationToken);
        await dbContext.WalletBalanceBuckets.AddAsync(bucket, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new PromotionAllocationDto(
            order.CustomerOrderId,
            promoCode,
            wallet.CustomerWalletId,
            wallet.WalletTypeId,
            walletType.Name,
            amount,
            wallet.CurrentBalance,
            transaction.WalletTransactionId,
            transaction.TransactionNumber,
            order.Status,
            order.Remarks ?? AwardRemarks);
    }

    private static decimal CalculateAward(StorePromotion promotion, decimal orderAmount)
    {
        decimal amount = promotion.DiscountType switch
        {
            StorePromotionDiscountTypes.TotalBonusPoint => promotion.BonusPoint,
            StorePromotionDiscountTypes.BillPercent when promotion.BillPercentDiscount is > 0m =>
                orderAmount * promotion.BillPercentDiscount.Value / 100m,
            _ => throw new DomainException("The promo code has an unsupported reward configuration.")
        };

        amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        return amount > 0m
            ? amount
            : throw new DomainException("The configured promotion reward must be greater than zero.");
    }
}
