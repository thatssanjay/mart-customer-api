namespace Mart.Customer.Domain.Wallets;

public static class WalletTypeCodes
{
    public const string MartWallet = "MART_WALLET";
}

public static class WalletOperationKinds
{
    public const string SaleReward = "SALE_REWARD";
}

public static class WalletOperationStatuses
{
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
}

public static class WalletOperationOutcomes
{
    public const string Credited = "CREDITED";
    public const string NoReward = "NO_REWARD";
}

public static class WalletComponentCodes
{
    public const string BaseReward = "BASE_REWARD";
    public const string SubscriptionBonus = "SUBSCRIPTION_BONUS";
    public const string DefaultAllocation = "DEFAULT";
}
