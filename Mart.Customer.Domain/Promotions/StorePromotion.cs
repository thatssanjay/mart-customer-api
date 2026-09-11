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
}

public static class StorePromotionDiscountTypes
{
    public const string BillPercent = "BILL_PERCENT";
    public const string TotalBonusPoint = "TOTAL_BONUS_POINT";
}
