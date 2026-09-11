using System.Globalization;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Common.Utilities;
using Mart.Customer.Application.Referrals.Dtos;
using Mart.Customer.Application.Referrals.Services;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Referrals;
using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Services;

public sealed class ReferralRewardService(
    ApplicationDbContext dbContext,
    IUnitOfWork unitOfWork) : IReferralRewardService
{
    private const string ReferralReferenceType = "REFERRAL";
    private const string ReferrerRewardRemarks = "REFERRER_REWARD";
    private const string ReferredCustomerRewardRemarks = "REFERRED_CUSTOMER_REWARD";

    public Task<ProcessReferralRewardsResultDto> ProcessAsync(
        long processedBy,
        CancellationToken cancellationToken = default)
    {
        if (processedBy <= 0)
            throw new DomainException("A valid authenticated internal user is required.");

        return unitOfWork.ExecuteInTransactionAsync(
            token => ProcessInTransactionAsync(processedBy, token),
            cancellationToken);
    }

    private async Task<ProcessReferralRewardsResultDto> ProcessInTransactionAsync(
        long processedBy,
        CancellationToken cancellationToken)
    {
        // A SQL Server execution strategy can replay the complete serializable transaction.
        dbContext.ChangeTracker.Clear();

        var activeReferrals = await dbContext.CustomerReferrals
            .Where(referral =>
                referral.Status == CustomerReferral.ActiveStatus &&
                !referral.RewardProcessed &&
                referral.ReferredCustomerId.HasValue)
            .OrderBy(referral => referral.CustomerReferralId)
            .ToListAsync(cancellationToken);

        var processedOn = DateTime.UtcNow;
        if (activeReferrals.Count == 0)
            return new ProcessReferralRewardsResultDto(0, 0, 0, 0, processedOn);

        var configurationIds = activeReferrals
            .Select(referral => referral.ReferralConfigId)
            .Distinct()
            .ToList();
        var configurations = await dbContext.ReferralConfigurations
            .AsNoTracking()
            .Where(configuration => configurationIds.Contains(configuration.Id))
            .ToDictionaryAsync(configuration => configuration.Id, cancellationToken);

        if (configurations.Count != configurationIds.Count)
            throw new DomainException("One or more active referrals have no referral configuration.");

        var referredCustomerIds = activeReferrals
            .Select(referral => referral.ReferredCustomerId!.Value)
            .Distinct()
            .ToList();
        var purchaseTotals = await dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                referredCustomerIds.Contains(order.CustomerId) &&
                order.OrderStatus == "Paid")
            .GroupBy(order => order.CustomerId)
            .Select(group => new
            {
                CustomerId = group.Key,
                Total = group.Sum(order => order.FinalPayableAmount)
            })
            .ToDictionaryAsync(item => item.CustomerId, item => item.Total, cancellationToken);

        var eligible = activeReferrals
            .Select(referral => new RewardCandidate(
                referral,
                configurations[referral.ReferralConfigId],
                purchaseTotals.GetValueOrDefault(referral.ReferredCustomerId!.Value)))
            .Where(candidate => candidate.TotalPurchaseAmount >= candidate.Configuration.MinimumPurchaseAmount)
            .ToList();

        if (eligible.Count == 0)
            return new ProcessReferralRewardsResultDto(activeReferrals.Count, 0, 0, 0, processedOn);

        ValidateConfigurations(eligible);

        var walletTypeIds = eligible
            .Select(candidate => candidate.Configuration.RewardWalletTypeId)
            .Distinct()
            .ToList();
        var walletTypes = await dbContext.WalletTypes
            .AsNoTracking()
            .Where(walletType => walletTypeIds.Contains(walletType.Id))
            .ToDictionaryAsync(walletType => walletType.Id, cancellationToken);

        foreach (var walletTypeId in walletTypeIds)
        {
            if (!walletTypes.TryGetValue(walletTypeId, out var walletType))
                throw new DomainException($"Referral wallet type {walletTypeId} was not found.");
            if (!walletType.IsActive)
                throw new DomainException($"Referral wallet type {walletTypeId} is inactive.");
            if (string.Equals(walletType.Code, WalletTypeCodes.MartWallet, StringComparison.OrdinalIgnoreCase))
                throw new DomainException("A store-scoped MART_WALLET cannot be used for referral rewards.");
        }

        var customerIds = eligible
            .SelectMany(candidate => new[]
            {
                candidate.Referral.ReferrerCustomerId,
                candidate.Referral.ReferredCustomerId!.Value
            })
            .Distinct()
            .ToList();
        var wallets = await dbContext.CustomerWallets
            .Where(wallet =>
                customerIds.Contains(wallet.CustomerId) &&
                walletTypeIds.Contains(wallet.WalletTypeId) &&
                wallet.StoreId == null)
            .ToListAsync(cancellationToken);

        var walletsByCustomerAndType = wallets.ToDictionary(
            wallet => (wallet.CustomerId, wallet.WalletTypeId));
        foreach (var candidate in eligible)
        {
            EnsureWallet(candidate.Referral.ReferrerCustomerId, candidate.Configuration.RewardWalletTypeId);
            EnsureWallet(candidate.Referral.ReferredCustomerId!.Value, candidate.Configuration.RewardWalletTypeId);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var referralIds = eligible
            .Select(candidate => candidate.Referral.CustomerReferralId)
            .ToList();
        var existingCredits = await dbContext.WalletTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.TransactionType == "CREDIT" &&
                transaction.ReferenceType == ReferralReferenceType &&
                transaction.ReferenceId.HasValue &&
                referralIds.Contains(transaction.ReferenceId.Value))
            .ToListAsync(cancellationToken);
        var creditsByWalletAndReferral = existingCredits
            .GroupBy(transaction => (transaction.CustomerWalletId, transaction.ReferenceId!.Value))
            .ToDictionary(group => group.Key, group => group.ToList());

        var createdBy = processedBy.ToString(CultureInfo.InvariantCulture);
        decimal referrerPointsCredited = 0;
        decimal referredCustomerPointsCredited = 0;

        foreach (var candidate in eligible)
        {
            var walletTypeId = candidate.Configuration.RewardWalletTypeId;
            var referralId = candidate.Referral.CustomerReferralId;
            var referrerWallet = walletsByCustomerAndType[(candidate.Referral.ReferrerCustomerId, walletTypeId)];
            var referredWallet = walletsByCustomerAndType[(candidate.Referral.ReferredCustomerId!.Value, walletTypeId)];

            if (await CreditAsync(
                    referrerWallet,
                    referralId,
                    candidate.Configuration.ReferrerRewardPoint,
                    ReferrerRewardRemarks,
                    createdBy,
                    processedOn,
                    creditsByWalletAndReferral,
                    cancellationToken))
                referrerPointsCredited += candidate.Configuration.ReferrerRewardPoint;

            if (await CreditAsync(
                    referredWallet,
                    referralId,
                    candidate.Configuration.ReferredCustomerRewardPoint,
                    ReferredCustomerRewardRemarks,
                    createdBy,
                    processedOn,
                    creditsByWalletAndReferral,
                    cancellationToken))
                referredCustomerPointsCredited += candidate.Configuration.ReferredCustomerRewardPoint;

            candidate.Referral.MarkRewardProcessed(processedOn);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProcessReferralRewardsResultDto(
            activeReferrals.Count,
            eligible.Count,
            referrerPointsCredited,
            referredCustomerPointsCredited,
            processedOn);

        void EnsureWallet(long customerId, int walletTypeId)
        {
            if (walletsByCustomerAndType.TryGetValue((customerId, walletTypeId), out var existingWallet))
            {
                if (!existingWallet.IsActive)
                    throw new DomainException($"Customer {customerId} referral wallet is inactive.");
                return;
            }

            var wallet = CustomerWallet.Create(customerId, walletTypeId, processedOn);
            dbContext.CustomerWallets.Add(wallet);
            walletsByCustomerAndType.Add((customerId, walletTypeId), wallet);
        }
    }

    private async Task<bool> CreditAsync(
        CustomerWallet wallet,
        long referralId,
        decimal amount,
        string remarks,
        string createdBy,
        DateTime processedOn,
        IReadOnlyDictionary<(long WalletId, long ReferralId), List<WalletTransaction>> existingCredits,
        CancellationToken cancellationToken)
    {
        if (existingCredits.TryGetValue((wallet.CustomerWalletId, referralId), out var credits))
        {
            if (credits.Count != 1 || credits[0].Amount != amount || credits[0].Remarks != remarks)
                throw new DomainException("Existing referral wallet transaction details are inconsistent.");
            return false;
        }

        var balanceBefore = wallet.CurrentBalance;
        wallet.Credit(amount, processedOn);
        var transaction = WalletTransaction.CreateCredit(
            ReferenceCodeGenerator.GenerateWithPrefix("TX"),
            wallet.CustomerWalletId,
            amount,
            balanceBefore,
            wallet.CurrentBalance,
            ReferralReferenceType,
            referralId,
            remarks,
            processedOn,
            createdBy);
        var bucket = WalletBalanceBucket.Create(
            wallet.CustomerWalletId,
            transaction,
            amount,
            expiryDate: null,
            createdOn: processedOn);

        await dbContext.WalletTransactions.AddAsync(transaction, cancellationToken);
        await dbContext.WalletBalanceBuckets.AddAsync(bucket, cancellationToken);
        return true;
    }

    private static void ValidateConfigurations(IReadOnlyCollection<RewardCandidate> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.Referral.ReferrerCustomerId == candidate.Referral.ReferredCustomerId)
                throw new DomainException("A referral cannot reward the same customer twice.");
            if (candidate.Configuration.MinimumPurchaseAmount < 0)
                throw new DomainException("Referral minimum purchase amount cannot be negative.");
            if (candidate.Configuration.ReferrerRewardPoint <= 0 ||
                candidate.Configuration.ReferredCustomerRewardPoint <= 0)
                throw new DomainException("Referral reward points must be greater than zero for both customers.");
            if (candidate.Configuration.RewardWalletTypeId <= 0)
                throw new DomainException("Referral wallet type is invalid.");
        }
    }

    private sealed record RewardCandidate(
        CustomerReferral Referral,
        ReferralConfiguration Configuration,
        decimal TotalPurchaseAmount);
}
