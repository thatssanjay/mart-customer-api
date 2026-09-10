using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Common.Utilities;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Application.Wallets.Services;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.RedeemWallet;

public sealed class RedeemWalletCommandHandler
    : IRequestHandler<RedeemWalletCommand, RedeemWalletResultDto>
{
    private readonly ICustomerWalletRepository _customerWalletRepository;
    private readonly IWalletTypeRepository _walletTypeRepository;
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IWalletBalanceBucketRepository _walletBalanceBucketRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RedeemWalletCommandHandler(
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

    public Task<RedeemWalletResultDto> Handle(
        RedeemWalletCommand request,
        CancellationToken cancellationToken)
    {
        return _unitOfWork.ExecuteInTransactionAsync(
            transactionCancellationToken => RedeemAsync(request, transactionCancellationToken),
            cancellationToken);
    }

    private async Task<RedeemWalletResultDto> RedeemAsync(
        RedeemWalletCommand request,
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

        var referenceType = string.IsNullOrWhiteSpace(request.ReferenceType)
            ? null
            : request.ReferenceType.Trim().ToUpperInvariant();
        if (referenceType is not null && request.ReferenceId.HasValue)
        {
            var existingRedemption = await _walletTransactionRepository.GetRedemptionByReferenceAsync(
                wallet.CustomerWalletId,
                referenceType,
                request.ReferenceId.Value,
                cancellationToken);
            if (existingRedemption is not null)
            {
                if (existingRedemption.Amount != request.Amount)
                {
                    throw new DomainException(
                        "The redemption reference has already been used with different redemption details.");
                }

                return existingRedemption;
            }
        }

        if (wallet.CurrentBalance < request.Amount)
        {
            throw new DomainException("Insufficient wallet balance.");
        }

        var transactionDate = DateTime.UtcNow;
        var buckets = await _walletBalanceBucketRepository.GetEligibleForRedemptionAsync(
            wallet.CustomerWalletId,
            transactionDate,
            cancellationToken);
        var bucketDtos = buckets.Select(bucket => new WalletBalanceBucketDto(
            bucket.WalletBalanceBucketId,
            bucket.CustomerWalletId,
            bucket.SourceTransactionId,
            bucket.OriginalAmount,
            bucket.AvailableAmount,
            bucket.ExpiryDate,
            bucket.CreatedOn,
            null));
        // Store-wallet balances can include credits created before balance buckets
        // were introduced. CurrentBalance remains authoritative for those wallets;
        // consume every available bucket first and treat only the remainder as
        // legacy/unbucketed balance.
        var bucketBalance = bucketDtos.Sum(bucket => bucket.AvailableAmount);
        var bucketRedemptionAmount = request.StoreId is > 0
            ? Math.Min(request.Amount, bucketBalance)
            : request.Amount;
        var allocations = bucketRedemptionAmount > 0
            ? WalletRedemptionBucketCalculator.Calculate(
                bucketDtos,
                bucketRedemptionAmount,
                transactionDate)
            : [];

        var allocationsByBucketId = allocations.ToDictionary(
            allocation => allocation.WalletBalanceBucketId,
            allocation => allocation.RedeemAmount);
        foreach (var bucket in buckets)
        {
            if (allocationsByBucketId.TryGetValue(bucket.WalletBalanceBucketId, out var redeemAmount))
            {
                bucket.Redeem(redeemAmount);
            }
        }

        var balanceBefore = wallet.CurrentBalance;
        wallet.Redeem(request.Amount, transactionDate);
        var transaction = WalletTransaction.CreateRedemption(
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

        await _walletTransactionRepository.AddAsync(transaction, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RedeemWalletResultDto(
            transaction.WalletTransactionId,
            transaction.TransactionNumber,
            wallet.CustomerWalletId,
            wallet.CustomerId,
            wallet.WalletTypeId,
            transaction.Amount,
            transaction.BalanceBefore,
            transaction.BalanceAfter,
            wallet.TotalDebit,
            transaction.ReferenceType,
            transaction.ReferenceId,
            transaction.TransactionDate);
    }
}
