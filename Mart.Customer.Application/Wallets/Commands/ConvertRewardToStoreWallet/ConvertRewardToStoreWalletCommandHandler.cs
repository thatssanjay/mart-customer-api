using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Common.Utilities;
using Mart.Customer.Application.Cashback.Dtos;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Application.Wallets.Engine;
using Mart.Customer.Application.Wallets.Services;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.ConvertRewardToStoreWallet;

public sealed class ConvertRewardToStoreWalletCommandHandler(
    ICashbackConfigurationRepository cashbackConfigurations,
    ICustomerWalletRepository customerWallets,
    IWalletTypeRepository walletTypes,
    IWalletTransactionRepository walletTransactions,
    IWalletBalanceBucketRepository balanceBuckets,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ConvertRewardToStoreWalletCommand, RewardStoreWalletConversionResultDto>
{
    private const string ConversionReferenceType = "REWARD_CONVERSION";
    private const string ConversionRemarkPrefix = "Reward conversion";

    public Task<RewardStoreWalletConversionResultDto> Handle(
        ConvertRewardToStoreWalletCommand request,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(
            token => ConvertAsync(request, token), cancellationToken);
    }

    private async Task<RewardStoreWalletConversionResultDto> ConvertAsync(
        ConvertRewardToStoreWalletCommand request,
        CancellationToken cancellationToken)
    {
        var requestId = request.RequestId.Trim().ToUpperInvariant();
        var referenceId = CreateReferenceId(requestId);
        var remarks = $"{ConversionRemarkPrefix} {requestId}";
        var now = DateTime.UtcNow;

        var configuration = await cashbackConfigurations.GetActiveStoreWalletConversionAsync(
            request.StoreId, WalletTypeCodes.MartWallet, now, cancellationToken)
            ?? throw new DomainException("The selected store does not have an active conversion rate.");

        var activeTypes = await walletTypes.GetAsync(activeOnly: true, cancellationToken);
        var rewardType = activeTypes.SingleOrDefault(type =>
            string.Equals(type.Code, WalletTypeCodes.Reward, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException("The REWARD wallet type is not available.");
        var martWalletType = activeTypes.SingleOrDefault(type =>
            string.Equals(type.Code, WalletTypeCodes.MartWallet, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException("The MART_WALLET type is not available.");
        if (martWalletType.Id != configuration.WalletTypeId)
            throw new DomainException("The selected store conversion is not configured for MART_WALLET.");

        var rewardWallet = await customerWallets.GetByCustomerAndTypeAsync(
            request.CustomerId, rewardType.Id, cancellationToken)
            ?? throw new DomainException("Customer REWARD wallet not found.");
        if (!rewardWallet.IsActive)
            throw new DomainException("Customer REWARD wallet is inactive.");

        var storeWallet = await customerWallets.GetByCustomerAndTypeAsync(
            request.CustomerId, martWalletType.Id, cancellationToken, request.StoreId);
        if (storeWallet is not null && !storeWallet.IsActive)
        {
            throw new DomainException("The selected store wallet is inactive.");
        }

        var convertedAmount = WalletRoundingPolicy.RoundAmount(
            request.RewardPoints * configuration.ConversionRate / 100m);
        if (convertedAmount <= 0)
            throw new DomainException("The converted store wallet value must be greater than zero.");

        var existing = await walletTransactions.GetByReferenceAsync(
            request.CustomerId, ConversionReferenceType, referenceId, cancellationToken);
        if (existing.Count > 0)
        {
            if (storeWallet is null)
                throw new DomainException(
                    "The conversion request id has already been used with different conversion details.");
            return BuildReplayResult(
                request, requestId, referenceId, remarks, configuration,
                rewardWallet, storeWallet, convertedAmount, existing);
        }

        if (rewardWallet.CurrentBalance < request.RewardPoints)
            throw new DomainException("Insufficient REWARD balance.");

        var eligibleBuckets = await balanceBuckets.GetEligibleForRedemptionAsync(
            rewardWallet.CustomerWalletId, now, cancellationToken);
        // CurrentBalance is the authoritative spendable balance. Older reward
        // credits may predate balance buckets, so consume existing buckets when
        // possible and let the wallet balance represent any unbucketed remainder.
        var bucketBalance = eligibleBuckets.Sum(bucket => bucket.AvailableAmount);
        if (bucketBalance >= request.RewardPoints)
        {
            var allocations = WalletRedemptionBucketCalculator.Calculate(
                eligibleBuckets.Select(bucket => new WalletBalanceBucketDto(
                    bucket.WalletBalanceBucketId,
                    bucket.CustomerWalletId,
                    bucket.SourceTransactionId,
                    bucket.OriginalAmount,
                    bucket.AvailableAmount,
                    bucket.ExpiryDate,
                    bucket.CreatedOn,
                    null)),
                request.RewardPoints,
                now);
            var allocationAmounts = allocations.ToDictionary(
                allocation => allocation.WalletBalanceBucketId,
                allocation => allocation.RedeemAmount);
            foreach (var bucket in eligibleBuckets)
            {
                if (allocationAmounts.TryGetValue(bucket.WalletBalanceBucketId, out var amount))
                    bucket.Redeem(amount);
            }
        }
        else
        {
            // Consume any legacy buckets that do exist. The remainder is
            // represented by the authoritative wallet balance only.
            foreach (var bucket in eligibleBuckets)
                if (bucket.AvailableAmount > 0)
                    bucket.Redeem(bucket.AvailableAmount);
        }

        if (storeWallet is null)
        {
            storeWallet = CustomerWallet.Create(
                request.CustomerId, martWalletType.Id, now, request.StoreId);
            await customerWallets.AddRangeAsync([storeWallet], cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var rewardBalanceBefore = rewardWallet.CurrentBalance;
        rewardWallet.Redeem(request.RewardPoints, now);
        var rewardTransaction = WalletTransaction.CreateRedemption(
            ReferenceCodeGenerator.GenerateWithPrefix("TX"),
            rewardWallet.CustomerWalletId,
            request.RewardPoints,
            rewardBalanceBefore,
            rewardWallet.CurrentBalance,
            ConversionReferenceType,
            referenceId,
            remarks,
            now,
            request.CreatedBy);

        var storeBalanceBefore = storeWallet.CurrentBalance;
        storeWallet.Credit(convertedAmount, now);
        var storeTransaction = WalletTransaction.CreateCredit(
            ReferenceCodeGenerator.GenerateWithPrefix("TX"),
            storeWallet.CustomerWalletId,
            convertedAmount,
            storeBalanceBefore,
            storeWallet.CurrentBalance,
            ConversionReferenceType,
            referenceId,
            remarks,
            now,
            request.CreatedBy);
        var expiryDate = configuration.ExpiryDate.HasValue
            ? DateTime.SpecifyKind(configuration.ExpiryDate.Value, DateTimeKind.Utc)
            : (DateTime?)null;
        var storeBucket = WalletBalanceBucket.Create(
            storeWallet.CustomerWalletId, storeTransaction, convertedAmount, expiryDate, now);

        await walletTransactions.AddAsync(rewardTransaction, cancellationToken);
        await walletTransactions.AddAsync(storeTransaction, cancellationToken);
        await balanceBuckets.AddAsync(storeBucket, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResult(
            requestId, referenceId, configuration.StoreId, configuration.StoreName,
            configuration.ConversionRate, rewardTransaction, rewardWallet,
            storeTransaction, storeWallet, false);
    }

    private static RewardStoreWalletConversionResultDto BuildReplayResult(
        ConvertRewardToStoreWalletCommand request,
        string requestId,
        long referenceId,
        string remarks,
        ActiveStoreWalletConversionDto configuration,
        CustomerWallet rewardWallet,
        CustomerWallet storeWallet,
        decimal convertedAmount,
        IReadOnlyList<WalletTransaction> transactions)
    {
        var rewardTransaction = transactions.SingleOrDefault(transaction =>
            transaction.CustomerWalletId == rewardWallet.CustomerWalletId &&
            transaction.TransactionType == "REDEMPTION");
        var storeTransaction = transactions.SingleOrDefault(transaction =>
            transaction.CustomerWalletId == storeWallet.CustomerWalletId &&
            transaction.TransactionType == "CREDIT");
        if (transactions.Count != 2 || rewardTransaction is null || storeTransaction is null ||
            rewardTransaction.Amount != request.RewardPoints ||
            storeTransaction.Amount != convertedAmount ||
            rewardTransaction.Remarks != remarks || storeTransaction.Remarks != remarks)
        {
            throw new DomainException(
                "The conversion request id has already been used with different conversion details.");
        }

        return ToResult(
            requestId, referenceId, configuration.StoreId, configuration.StoreName,
            configuration.ConversionRate, rewardTransaction, rewardWallet,
            storeTransaction, storeWallet, true);
    }

    private static RewardStoreWalletConversionResultDto ToResult(
        string requestId,
        long referenceId,
        long storeId,
        string storeName,
        decimal conversionRate,
        WalletTransaction rewardTransaction,
        CustomerWallet rewardWallet,
        WalletTransaction storeTransaction,
        CustomerWallet storeWallet,
        bool replay)
    {
        return new RewardStoreWalletConversionResultDto(
            requestId,
            referenceId,
            storeId,
            storeName,
            conversionRate,
            rewardTransaction.Amount,
            storeTransaction.Amount,
            rewardWallet.CustomerWalletId,
            replay ? rewardTransaction.BalanceAfter : rewardWallet.CurrentBalance,
            storeWallet.CustomerWalletId,
            replay ? storeTransaction.BalanceAfter : storeWallet.CurrentBalance,
            rewardTransaction.WalletTransactionId,
            rewardTransaction.TransactionNumber,
            storeTransaction.WalletTransactionId,
            storeTransaction.TransactionNumber,
            rewardTransaction.TransactionDate,
            replay);
    }

    private static long CreateReferenceId(string requestId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(requestId));
        var value = BinaryPrimitives.ReadInt64BigEndian(hash) & long.MaxValue;
        return value == 0 ? 1 : value;
    }
}
