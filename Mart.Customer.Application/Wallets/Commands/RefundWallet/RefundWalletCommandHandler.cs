using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Common.Utilities;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.RefundWallet;

public sealed class RefundWalletCommandHandler
    : IRequestHandler<RefundWalletCommand, RefundWalletResultDto>
{
    private readonly ICustomerWalletRepository _customerWalletRepository;
    private readonly IWalletTypeRepository _walletTypeRepository;
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IWalletBalanceBucketRepository _walletBalanceBucketRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RefundWalletCommandHandler(
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

    public Task<RefundWalletResultDto> Handle(
        RefundWalletCommand request,
        CancellationToken cancellationToken)
    {
        return _unitOfWork.ExecuteInTransactionAsync(
            transactionCancellationToken => RefundAsync(request, transactionCancellationToken),
            cancellationToken);
    }

    private async Task<RefundWalletResultDto> RefundAsync(
        RefundWalletCommand request,
        CancellationToken cancellationToken)
    {
        var wallet = await _customerWalletRepository.GetByCustomerAndTypeAsync(
            request.CustomerId,
            request.WalletTypeId,
            cancellationToken,
            request.StoreId);
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

        var originalTransactionNumber = request.OriginalTransactionNumber.Trim();
        var originalTransaction = await _walletTransactionRepository.GetRefundableByTransactionNumberAsync(
            originalTransactionNumber,
            cancellationToken);
        if (originalTransaction is null)
        {
            throw new DomainException("Original transaction not found.");
        }

        if (originalTransaction.CustomerWalletId != wallet.CustomerWalletId)
        {
            throw new DomainException("Original transaction does not belong to the specified customer wallet.");
        }

        if (originalTransaction.TransactionType != "REDEMPTION")
        {
            throw new DomainException("Only redemption transactions can be refunded.");
        }

        var previouslyRefundedAmount = await _walletTransactionRepository.GetRefundedAmountAsync(
            originalTransaction.WalletTransactionId,
            cancellationToken);
        var refundableAmount = originalTransaction.Amount - previouslyRefundedAmount;
        if (request.Amount > refundableAmount)
        {
            throw new DomainException("Refund amount exceeds the remaining refundable amount.");
        }

        var transactionDate = DateTime.UtcNow;
        var balanceBefore = wallet.CurrentBalance;
        wallet.Refund(request.Amount, transactionDate);

        var transaction = WalletTransaction.CreateRefund(
            ReferenceCodeGenerator.GenerateWithPrefix("TX"),
            wallet.CustomerWalletId,
            originalTransaction.WalletTransactionId,
            request.Amount,
            balanceBefore,
            wallet.CurrentBalance,
            request.Remarks,
            transactionDate,
            request.CreatedBy);
        var bucket = WalletBalanceBucket.Create(
            wallet.CustomerWalletId,
            transaction,
            request.Amount,
            expiryDate: null,
            transactionDate);

        await _walletTransactionRepository.AddAsync(transaction, cancellationToken);
        await _walletBalanceBucketRepository.AddAsync(bucket, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refundedAmount = previouslyRefundedAmount + request.Amount;
        return new RefundWalletResultDto(
            transaction.WalletTransactionId,
            transaction.TransactionNumber,
            originalTransaction.WalletTransactionId,
            originalTransaction.TransactionNumber,
            wallet.CustomerWalletId,
            wallet.CustomerId,
            wallet.WalletTypeId,
            transaction.Amount,
            transaction.BalanceBefore,
            transaction.BalanceAfter,
            refundedAmount,
            originalTransaction.Amount - refundedAmount,
            transaction.TransactionDate);
    }
}
