namespace Mart.Customer.Domain.Promotions;

public sealed class StorePromotion
{
    private StorePromotion() { }

    public int Id { get; private set; }
    public string? PromoCode { get; private set; }
    public long StoreId { get; private set; }
    public int WalletTypeId { get; private set; }
    public decimal BonusPoint { get; private set; }
    public string DiscountType { get; private set; } = string.Empty;
    public decimal? BillPercentDiscount { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public long? UsedBy { get; private set; }
    public DateTime? UsedOn { get; private set; }
    public string? Status { get; private set; }
    public long? CustomerId { get; private set; }

    public void MarkAwarded(long userId, long customerId, DateTime awardedOn)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));
        if (customerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(customerId));
        if (Status == StorePromotionStatuses.Awarded)
            throw new InvalidOperationException("The promotion code has already been awarded.");

        UsedBy = userId;
        UsedOn = awardedOn;
        Status = StorePromotionStatuses.Awarded;
        CustomerId = customerId;
    }
}

public static class StorePromotionDiscountTypes
{
    public const string BillPercent = "BILL_PERCENT";
    public const string TotalBonusPoint = "TOTAL_BONUS_POINT";
}

public static class StorePromotionStatuses
{
    public const string Awarded = "Awarded";
}
