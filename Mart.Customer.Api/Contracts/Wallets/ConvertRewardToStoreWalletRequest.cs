namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class ConvertRewardToStoreWalletRequest
{
    public long StoreId { get; init; }
    public decimal RewardPoints { get; init; }
    public string RequestId { get; init; } = string.Empty;
}
