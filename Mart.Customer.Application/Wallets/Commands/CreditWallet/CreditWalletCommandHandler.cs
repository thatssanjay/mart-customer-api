using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Common.Utilities;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.CreditWallet;

public sealed class CreditWalletCommandHandler
    : IRequestHandler<CreditWalletCommand, CreditWalletResultDto>
{
    private readonly ICustomerWalletRepository _customerWalletRepository;
    private readonly IWalletTypeRepository _walletTypeRepository;
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IWalletBalanceBucketRepository _walletBalanceBucketRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreditWalletCommandHandler(
        ICustomerWalletRepository customerWalletRepository,
        IWalletTypeRepository walletTypeRepository,
        IWalletTransactionRepository walletTransactionRepository,
        IWalletBalanceBucketRepository walletBalanceBucketRepository,
        IUnitOfWork unitOfWork)
    {
        _customerWalletRepository = customerWalletRepository;
        _walletTypeRepository = walletTypeRepository;
        _walletTransactionRepository = walletTransactionRepository;
        _walletBalanceBucketRepository = walletBalanceBucketRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<CreditWalletResultDto> Handle(
        CreditWalletCommand request,
        CancellationToken cancellationToken)
    {
        return _unitOfWork.ExecuteInTransactionAsync(
            transactionCancellationToken => CreditAsync(request, transactionCancellationToken),
            cancellationToken);
    }

    private async Task<CreditWalletResultDto> CreditAsync(
        CreditWalletCommand request,
        CancellationToken cancellationToken)
    {
        var wallet = await _customerWalletRepository.GetByCustomerAndTypeAsync(
            request.CustomerId,
            request.WalletTypeId,
            cancellationToken);
        if (wallet is null)
        {
            throw new DomainException("Customer wallet not found.");
        }

        if (!wallet.IsActive)
        {
            throw new DomainException("Customer wallet is inactive.");
        }

        var walletType = await _walletTypeRepository.GetByIdAsync(
            request.WalletTypeId,
            cancellationToken);
        if (walletType is null)
        {
            throw new DomainException("Wallet type not found.");
        }

        if (!walletType.IsActive)
        {
            throw new DomainException("Wallet type is inactive.");
        }

        var referenceType = string.IsNullOrWhiteSpace(request.ReferenceType)
            ? null
            : request.ReferenceType.Trim().ToUpperInvariant();
        if (referenceType is not null && request.ReferenceId.HasValue)
        {
            var existingCredit = await _walletTransactionRepository.GetCreditByReferenceAsync(
                wallet.CustomerWalletId,
                referenceType,
                request.ReferenceId.Value,
                cancellationToken);
            if (existingCredit is not null)
            {
                if (existingCredit.Amount != request.Amount ||
                    existingCredit.ExpiryDate != request.ExpiryDate)
                {
                    throw new DomainException(
                        "The credit reference has already been used with different credit details.");
                }

                return existingCredit;
            }
        }

        var transactionDate = DateTime.UtcNow;
        var balanceBefore = wallet.CurrentBalance;
        wallet.Credit(request.Amount, transactionDate);

        var transaction = WalletTransaction.CreateCredit(
            ReferenceCodeGenerator.GenerateWithPrefix("TX"),
            wallet.CustomerWalletId,
            request.Amount,
            balanceBefore,
            wallet.CurrentBalance,
            referenceType,
            request.ReferenceId,
            request.Remarks,
            transactionDate,
            request.CreatedBy);
        var bucket = WalletBalanceBucket.Create(
            wallet.CustomerWalletId,
            transaction,
            request.Amount,
            request.ExpiryDate,
            transactionDate);

        await _walletTransactionRepository.AddAsync(transaction, cancellationToken);
        await _walletBalanceBucketRepository.AddAsync(bucket, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreditWalletResultDto(
            transaction.WalletTransactionId,
            transaction.TransactionNumber,
            wallet.CustomerWalletId,
            wallet.CustomerId,
            wallet.WalletTypeId,
            transaction.Amount,
            transaction.BalanceBefore,
            transaction.BalanceAfter,
            wallet.TotalCredit,
            transaction.ReferenceType,
            transaction.ReferenceId,
            bucket.ExpiryDate,
            transaction.TransactionDate);
    }
}
