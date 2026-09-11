using FluentValidation;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Common.Utilities;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;

namespace Mart.Customer.Application.Wallets.Engine;

public interface IWalletEngineService
{
    Task<WalletEngineResult> PostAsync(WalletPostingRequest request, CancellationToken cancellationToken = default);
    Task<OrderWalletCreditResult> CreditPaidOrderAsync(long orderId, OrderWalletAccess access,
        CancellationToken cancellationToken = default);
}

public sealed class WalletEngineService(IWalletOperationRepository operations, ICustomerWalletResolver resolver,
    IWalletLedgerService ledger, IWalletTypeRepository types, IUnitOfWork unitOfWork,
    IWalletPostingGuard guard, IValidator<WalletPostingRequest> validator,
    IOrderWalletSourceRepository orderSources, ICustomerSubscriptionRepository subscriptions) : IWalletEngineService
{
    public async Task<WalletEngineResult> PostAsync(WalletPostingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        guard.EnsureCleanEntry();
        // Freeze the caller's collection before asynchronous execution or whole-operation retries.
        request = request with { Allocations = request.Allocations?.ToArray()! };
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await unitOfWork.ExecuteInTransactionAsync(
                    token => PostInTransactionAsync(request, token), cancellationToken);
            }
            catch (Exception exception) when (attempt < 2 && guard.IsRetryableConflict(exception))
            {
                // The outer transaction has rolled back. Never reuse its tracked balances or identities.
                unitOfWork.ClearChanges();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), cancellationToken);
            }
            catch
            {
                unitOfWork.ClearChanges();
                throw;
            }
        }
    }

    public async Task<OrderWalletCreditResult> CreditPaidOrderAsync(long orderId, OrderWalletAccess access,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (orderId <= 0) throw new DomainException("Order id must be greater than zero.");
        if (access.UserId <= 0 || (access.CustomerId.HasValue
                ? access.CustomerId <= 0 || access.CustomerId != access.UserId
                : access.FranchiseId <= 0 || access.StoreId <= 0))
            throw new DomainException("A valid authenticated customer or store context is required.");
        guard.EnsureCleanEntry();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await unitOfWork.ExecuteInTransactionAsync(token =>
                    CreditPaidOrderInTransactionAsync(orderId, access, token), cancellationToken);
            }
            catch (Exception exception) when (attempt < 2 && guard.IsRetryableConflict(exception))
            {
                unitOfWork.ClearChanges();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), cancellationToken);
            }
            catch
            {
                unitOfWork.ClearChanges();
                throw;
            }
        }
    }

    private async Task<OrderWalletCreditResult> CreditPaidOrderInTransactionAsync(long orderId,
        OrderWalletAccess access, CancellationToken cancellationToken)
    {
        guard.EnsureTransaction();
        var source = await orderSources.GetAsync(orderId, cancellationToken)
            ?? throw new DomainException("Order not found.");
        if (access.CustomerId.HasValue)
        {
            if (source.CustomerId != access.CustomerId.Value)
                throw new UnauthorizedAccessException("The order does not belong to the authenticated customer.");
        }
        //else if (source.FranchiseId != access.FranchiseId || source.StoreId != access.StoreId)
         //   throw new UnauthorizedAccessException("The order is outside the authenticated store scope.");
        if (!source.CustomerActive) throw new DomainException("Customer is inactive or blocked.");
        if (!source.StoreActive) throw new DomainException("Store is inactive.");
        if (!string.Equals(source.Status, "PAID", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Order payment is not successful.");
        if (source.PaidAmount <= 0 || source.Payments.Count == 0 ||
            source.Payments.Any(x => x.Amount <= 0 || x.PaidOn > DateTime.UtcNow) ||
            source.Payments.Sum(x => x.Amount) != source.PaidAmount)
            throw new DomainException("Order payment is incomplete or invalid.");
        if (source.Payments.Any(x => !IsEligiblePaymentMode(x.Mode)))
            throw new DomainException("Only fully APP-paid orders are eligible for a wallet reward.");
        if (source.Payments.Any(x => string.IsNullOrWhiteSpace(x.Reference)))
            throw new DomainException("APP payment must have a settlement reference.");

        var existing = await operations.FindAsync(WalletOperationKinds.SaleReward,
            WalletOperation.SaleBusinessKey(orderId), cancellationToken);
       // if (existing is null && source.HasLegacyReward)
           // throw new DomainException("Order already has wallet rewards from the legacy checkout flow.");

        IReadOnlyList<WalletAllocationResult> allocations = [];
        if (existing is null)
        {
            var config = await orderSources.GetConfigurationAsync(source.StoreId, source.OrderedAt, cancellationToken)
                ?? throw new DomainException("Active wallet reward configuration was not found for the order store.");
            allocations = await CalculateAllocationsAsync(source, config, cancellationToken);
        }
        var posted = await PostInTransactionAsync(new WalletPostingRequest(
            WalletOperationKinds.SaleReward, WalletOperation.SaleBusinessKey(orderId), source.CustomerId,
            source.StoreId, source.OrderId, source.Payments[0].Id, source.OrderedAt,
            "SALE_POINT_PERCENTAGE_V1", access.UserId.ToString(), allocations), cancellationToken);
        var codes = (await types.GetAsync(false, cancellationToken)).ToDictionary(x => x.Id, x => x.Code);
        return new OrderWalletCreditResult(source.OrderId, source.CustomerId, source.StoreId, source.PaidAmount,
            posted.IsIdempotentReplay, posted.Allocations.Select(x => new OrderWalletCreditItem(
                codes[x.WalletTypeId], x.CustomerWalletId, x.StoreId, x.Amount, x.BalanceAfter)).ToList());
    }

    private async Task<IReadOnlyList<WalletAllocationResult>> CalculateAllocationsAsync(
        OrderWalletSource source, OrderWalletConfiguration config, CancellationToken cancellationToken)
    {
        if (config.Wallets.Count == 0) throw new DomainException("No active wallets are configured for this store reward.");
        if (config.Setting.MinimumPurchaseAmount < 0 || config.Setting.MaximumCashbackPerOrder < 0)
            throw new DomainException("Minimum purchase and maximum cashback cannot be negative.");
        if (config.Wallets.Any(x => x.PointPercentage is < 0 or > 100))
            throw new DomainException("Wallet point percentage configuration is invalid.");
        var raw = config.Wallets.Where(_ => source.PaidAmount >= (config.Setting.MinimumPurchaseAmount ?? 0))
            .Select(x => (Rule: x,
            Amount: WalletRoundingPolicy.RoundAmount(source.PaidAmount * x.PointPercentage / 100m))).ToList();
        var cap = config.Setting.MaximumCashbackPerOrder ?? 0m;
        var total = raw.Sum(x => x.Amount);
        if (cap > 0 && total > cap)
        {
            var shares = raw.Select(x => cap * x.Amount / total).ToArray();
            for (var i = 0; i < raw.Count; i++)
                raw[i] = (raw[i].Rule, decimal.Floor(shares[i] * 100m) / 100m);
            // Allocate residual cents by largest fractional share; ties retain configured wallet order.
            var remaining = WalletRoundingPolicy.RoundAmount(cap - raw.Sum(x => x.Amount));
            foreach (var i in Enumerable.Range(0, raw.Count).OrderByDescending(i => shares[i] - raw[i].Amount))
                if (remaining > 0)
                {
                    raw[i] = (raw[i].Rule, raw[i].Amount + 0.01m);
                    remaining -= 0.01m;
                }
        }
        var result = raw.Select(x => CreateAllocation(source, config, x.Rule, x.Amount,
            WalletComponentCodes.BaseReward, x.Rule.Id.ToString())).ToList();
        var subscription = await subscriptions.GetActiveOrderRewardConfigurationAsync(
            source.CustomerId, source.OrderedAt, cancellationToken);
        if (subscription is not null)
        {
            if (subscription.ExtraPointPercentage is < 0 or > 100)
                throw new DomainException("Subscription extra point percentage is invalid.");
            var amount = WalletRoundingPolicy.RoundAmount(
                source.PaidAmount * subscription.ExtraPointPercentage / 100m);
            result.Add(new WalletAllocationResult(subscription.WalletTypeId, source.StoreId,
                WalletComponentCodes.SubscriptionBonus, amount, ToUtc(subscription.ExpiryDate),
                new WalletCalculationSnapshot { CalculationBase = source.PaidAmount,
                    ExtraPointPercentage = subscription.ExtraPointPercentage, RoundedAmount = amount,
                    CalculationBaseRule = "FINAL_PAID_AMOUNT", ConversionRule = "NOT_APPLICABLE",
                    ExpiryRule = "CUSTOMER_SUBSCRIPTION_EXPIRY", RoundingPolicy = "2DP_AWAY_FROM_ZERO" }));
        }
        return result;
    }

    private static WalletAllocationResult CreateAllocation(OrderWalletSource source,
        OrderWalletConfiguration config, OrderWalletRule rule, decimal amount, string component, string key)
    {
        DateTime? expiry = rule.IsNoExpiry ? null : ToUtc(rule.EndDate);
        if (!rule.IsNoExpiry && (!expiry.HasValue || expiry <= source.OrderedAt))
            throw new DomainException("Wallet allocation expiry configuration is invalid.");
        return new WalletAllocationResult(rule.WalletTypeId, source.StoreId, component, amount, expiry,
            new WalletCalculationSnapshot { ConfigurationId = config.Setting.CashbackSettingId,
                AllocationConfigurationId = rule.Id, PointPercentage = rule.PointPercentage,
                CalculationBase = source.PaidAmount, AmountBeforeConversion = amount,
                AmountBeforeCap = WalletRoundingPolicy.RoundAmount(source.PaidAmount * rule.PointPercentage / 100m),
                AmountAfterCap = amount, RoundedAmount = amount, CalculationBaseRule = "FINAL_PAID_AMOUNT",
                ConversionRule = "CONVERSION_RATE_IGNORED", ExpiryRule = rule.IsNoExpiry ? "NO_EXPIRY" : "CONFIGURED_END_DATE",
                RoundingPolicy = "2DP_AWAY_FROM_ZERO" }, key);
    }

    private static DateTime? ToUtc(DateTime? value) => value.HasValue
        ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
    private static bool IsEligiblePaymentMode(string mode) =>
        string.Equals(mode.Trim(), "Wallet", StringComparison.OrdinalIgnoreCase);

    private async Task<WalletEngineResult> PostInTransactionAsync(WalletPostingRequest request,
        CancellationToken cancellationToken)
    {
        guard.EnsureTransaction();
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var existing = await operations.FindAsync(request.OperationKind, request.BusinessKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.CustomerId != request.CustomerId || existing.StoreId != request.StoreId ||
                existing.CustomerOrderId != request.CustomerOrderId ||
                existing.CustomerOrderPaymentId != request.CustomerOrderPaymentId)
                throw new DomainException("The wallet business key belongs to different source identifiers.");
            if (existing.Status != WalletOperationStatuses.Completed)
                throw new DomainException("The wallet operation is not completed.");
            return await operations.ReadResultAsync(existing.WalletOperationId, true, cancellationToken);
        }

        var normalized = new List<WalletAllocationResult>();
        foreach (var group in request.Allocations.GroupBy(x => x?.WalletTypeId))
        {
            if (group.Key is null)
                throw new DomainException("Wallet allocations cannot contain null entries.");
            var type = await types.GetByIdAsync(group.Key.Value, cancellationToken)
                ?? throw new DomainException("Wallet type not found.");
            if (!type.IsActive)
                throw new DomainException("Wallet type is inactive.");
            var isStoreWallet = string.Equals(type.Code, WalletTypeCodes.MartWallet, StringComparison.OrdinalIgnoreCase);
            foreach (var allocation in group)
            {
                WalletPostingRequestValidator.ValidateComponent(allocation);
                if (isStoreWallet && (!request.StoreId.HasValue ||
                    (allocation.StoreId.HasValue && allocation.StoreId != request.StoreId)))
                    throw new DomainException("MART_WALLET allocation must use the operation store.");
                if (allocation.ExpiryDate.HasValue && (allocation.ExpiryDate.Value.Kind != DateTimeKind.Utc ||
                    allocation.ExpiryDate <= request.EffectiveAt))
                    throw new DomainException("Allocation expiry must be UTC and later than the effective time.");
                normalized.Add(allocation with { StoreId = isStoreWallet ? request.StoreId : null });
            }
        }
        if (normalized.GroupBy(x => (x.WalletTypeId, x.StoreId, x.ComponentCode, x.AllocationKey)).Any(x => x.Count() > 1))
            throw new DomainException("Duplicate wallet operation component.");

        var operation = WalletOperation.CreateSaleReward(ReferenceCodeGenerator.GenerateWithPrefix("WOP", 9),
            request.BusinessKey, request.CustomerId, request.StoreId, request.CustomerOrderId,
            request.CustomerOrderPaymentId, request.EffectiveAt, request.CalculationVersion,
            request.CreatedBy, DateTime.UtcNow);
        await operations.AddAsync(operation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var resolvedWallets = new Dictionary<(int WalletTypeId, long? StoreId), CustomerWallet>();
        foreach (var allocation in normalized.Where(x => x.Amount > 0)
                     .OrderBy(x => x.WalletTypeId).ThenBy(x => x.StoreId)
                     .ThenBy(x => x.ComponentCode, StringComparer.Ordinal)
                     .ThenBy(x => x.AllocationKey, StringComparer.Ordinal))
        {
            var walletKey = (allocation.WalletTypeId, allocation.StoreId);
            if (!resolvedWallets.TryGetValue(walletKey, out var wallet))
            {
                wallet = await resolver.ResolveAsync(request.CustomerId, allocation.WalletTypeId,
                    allocation.StoreId, cancellationToken);
                resolvedWallets.Add(walletKey, wallet);
            }
            await ledger.PostCreditAsync(operation, wallet, allocation, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        operation.Complete(normalized.Any(x => x.Amount > 0), DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await operations.ReadResultAsync(operation.WalletOperationId, false, cancellationToken);
    }
}
