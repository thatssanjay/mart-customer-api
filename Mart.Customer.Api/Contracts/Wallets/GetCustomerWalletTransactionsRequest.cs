namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class GetCustomerWalletTransactionsRequest
{
    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public string? TransactionType { get; init; }

    public DateTime? FromDate { get; init; }

    public DateTime? ToDate { get; init; }

    public string? ReferenceType { get; init; }

    public long? ReferenceId { get; init; }
}
