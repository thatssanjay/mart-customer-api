using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletTransactions;

public sealed record GetCustomerWalletTransactionsQuery(
    long CustomerId,
    int WalletTypeId,
    int PageNumber = 1,
    int PageSize = 10,
    string? TransactionType = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? ReferenceType = null,
    long? ReferenceId = null) : IRequest<PagedResultDto<WalletTransactionDto>?>;
