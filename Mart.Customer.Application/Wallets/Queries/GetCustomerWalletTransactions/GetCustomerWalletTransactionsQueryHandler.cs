using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletTransactions;

public sealed class GetCustomerWalletTransactionsQueryHandler
    : IRequestHandler<GetCustomerWalletTransactionsQuery, PagedResultDto<WalletTransactionDto>?>
{
    private readonly ICustomerWalletRepository _customerWalletRepository;
    private readonly IWalletTransactionRepository _walletTransactionRepository;

    public GetCustomerWalletTransactionsQueryHandler(
        ICustomerWalletRepository customerWalletRepository,
        IWalletTransactionRepository walletTransactionRepository)
    {
        _customerWalletRepository = customerWalletRepository;
        _walletTransactionRepository = walletTransactionRepository;
    }

    public async Task<PagedResultDto<WalletTransactionDto>?> Handle(
        GetCustomerWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var customerWalletId = await _customerWalletRepository.GetActiveIdAsync(
            request.CustomerId,
            request.WalletTypeId,
            cancellationToken);

        if (!customerWalletId.HasValue)
        {
            return null;
        }

        var (transactions, totalCount) = await _walletTransactionRepository.GetPagedAsync(
            customerWalletId.Value,
            request.PageNumber,
            request.PageSize,
            request.TransactionType,
            request.FromDate,
            request.ToDate,
            request.ReferenceType,
            request.ReferenceId,
            cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResultDto<WalletTransactionDto>(
            transactions,
            request.PageNumber,
            request.PageSize,
            totalCount,
            totalPages);
    }
}
