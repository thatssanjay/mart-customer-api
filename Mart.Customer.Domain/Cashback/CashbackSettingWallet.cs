namespace Mart.Customer.Domain.Cashback;

public sealed class CashbackSettingWallet
{
    public int Id { get; private set; }
    public long? CashbackSettingId { get; private set; }
    public int WalletTypeId { get; private set; }
    public decimal PointPercentage { get; private set; }
    public decimal? ConversionRate { get; private set; }
    public bool IsNoExpiry { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public bool IsActive { get; private set; }
}
