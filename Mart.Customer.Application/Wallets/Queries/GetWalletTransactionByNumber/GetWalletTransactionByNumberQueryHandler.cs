using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetWalletTransactionByNumber;

public sealed class GetWalletTransactionByNumberQueryHandler
    : IRequestHandler<GetWalletTransactionByNumberQuery, WalletTransactionDetailDto?>
{
    private readonly IWalletTransactionRepository _walletTransactionRepository;

    public GetWalletTransactionByNumberQueryHandler(IWalletTransactionRepository walletTransactionRepository)
    {
        _walletTransactionRepository = walletTransactionRepository;
    }

    public Task<WalletTransactionDetailDto?> Handle(
        GetWalletTransactionByNumberQuery request,
        CancellationToken cancellationToken)
    {
        return _walletTransactionRepository.GetByTransactionNumberAsync(
            request.TransactionNumber.Trim(),
            cancellationToken);
    }
}
