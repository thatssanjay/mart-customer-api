namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class GetCurrentCustomerWalletTransactionsRequest
{
    public int WalletTypeId { get; init; }

    public long? StoreId { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public DateTime? FromDate { get; init; }

    public DateTime? ToDate { get; init; }
}
