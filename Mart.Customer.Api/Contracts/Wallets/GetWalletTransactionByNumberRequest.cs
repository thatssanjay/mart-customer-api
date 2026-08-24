namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class GetWalletTransactionByNumberRequest
{
    public string TransactionNumber { get; init; } = string.Empty;
}
