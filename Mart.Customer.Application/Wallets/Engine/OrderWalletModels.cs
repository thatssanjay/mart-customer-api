using Mart.Customer.Domain.Cashback;

namespace Mart.Customer.Application.Wallets.Engine;

// Access identifiers are supplied by the authenticated server context, never HTTP binding.
public sealed record OrderWalletAccess(long UserId, long FranchiseId, long StoreId, long? CustomerId = null);
public sealed record OrderWalletPayment(long Id, string Mode, decimal Amount, string? Reference, DateTime PaidOn);
public sealed record OrderWalletSource(long OrderId, long CustomerId, long FranchiseId, long StoreId,
    decimal PaidAmount, string Status, DateTime OrderedAt, bool CustomerActive, bool StoreActive,
    bool HasLegacyReward, IReadOnlyList<OrderWalletPayment> Payments);
public sealed record OrderWalletConfiguration(CashbackConfiguration Setting, IReadOnlyList<OrderWalletRule> Wallets);
// ConversionRate is intentionally absent from calculation inputs.
public sealed record OrderWalletRule(int Id, int WalletTypeId, decimal PointPercentage,
    bool IsNoExpiry, DateTime? StartDate, DateTime? EndDate);
public sealed record OrderWalletCreditResult(long OrderId, long CustomerId, long StoreId, decimal PaidAmount,
    bool IsIdempotentReplay, IReadOnlyList<OrderWalletCreditItem> Wallets);
public sealed record OrderWalletCreditItem(string WalletTypeCode, long CustomerWalletId, long? StoreId,
    decimal CreditedAmount, decimal BalanceAfter);
