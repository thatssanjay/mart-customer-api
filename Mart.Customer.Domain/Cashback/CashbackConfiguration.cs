namespace Mart.Customer.Domain.Cashback;

public sealed class CashbackConfiguration
{
    private CashbackConfiguration()
    {
    }

    public long CashbackSettingId { get; private set; }

    public long? StoreId { get; private set; }

    public decimal? CashbackPercentage { get; private set; }

    public int? CashbackValidityDays { get; private set; }

    public decimal? MinimumPurchaseAmount { get; private set; }

    public decimal? MaximumCashbackPerOrder { get; private set; }

    public bool? IsActive { get; private set; }

    public int? CreatedBy { get; private set; }

    public DateTime? CreatedOn { get; private set; }

    public DateTime? StartDate { get; private set; }

    public DateTime? EndDate { get; private set; }
}
