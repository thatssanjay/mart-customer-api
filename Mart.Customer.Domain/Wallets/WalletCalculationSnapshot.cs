namespace Mart.Customer.Domain.Wallets;

/// <summary>Versioned value snapshot, never a live configuration lookup. Null means not supplied.</summary>
public sealed record WalletCalculationSnapshot
{
    public int SchemaVersion { get; init; } = 1;
    public long? ConfigurationId { get; init; }
    public long? AllocationConfigurationId { get; init; }
    public decimal? PointPercentage { get; init; }
    public decimal? ConversionRate { get; init; }
    public decimal? CalculationBase { get; init; }
    public decimal? AmountBeforeConversion { get; init; }
    public decimal? AmountBeforeCap { get; init; }
    public decimal? AmountAfterCap { get; init; }
    public decimal? RoundedAmount { get; init; }
    public long? SubscriptionId { get; init; }
    public decimal? ExtraPointPercentage { get; init; }
    public string? ExpiryRule { get; init; }
    public string? ConversionRule { get; init; }
    public string? CalculationBaseRule { get; init; }
    public string? RoundingPolicy { get; init; }
}
