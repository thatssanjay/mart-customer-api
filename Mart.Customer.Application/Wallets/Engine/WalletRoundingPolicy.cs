namespace Mart.Customer.Application.Wallets.Engine;

public static class WalletRoundingPolicy
{
    public const string Code = "DECIMAL_2_AWAY_FROM_ZERO";
    public static decimal RoundAmount(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
