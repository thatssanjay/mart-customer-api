using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Engine;
using Mart.Customer.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class OrderWalletSourceRepository(ApplicationDbContext db) : IOrderWalletSourceRepository
{
    public async Task<OrderWalletSource?> GetAsync(long orderId, CancellationToken cancellationToken)
    {
        return await db.CustomerOrders.AsNoTracking().Where(o => o.CustomerOrderId == orderId)
            .Select(o => new OrderWalletSource(o.CustomerOrderId, o.CustomerId, o.FranchiseId, o.MartStoreId,
                o.FinalPayableAmount, o.OrderStatus, o.OrderDate,
                db.Customers.Any(c => c.CustomerId == o.CustomerId && c.IsActive && !c.IsBlocked),
                db.MartStores.Any(s => s.StoreId == o.MartStoreId && s.FranchiseId == o.FranchiseId && s.IsActive),
                o.RewardEarned > 0 || o.CashbackEarned > 0 || db.WalletTransactions.Any(t =>
                    t.ReferenceType == "ORDER" && t.ReferenceId == orderId && t.TransactionType == "CREDIT"),
                o.Payments.OrderBy(p => p.CustomerOrderPaymentId).Select(p =>
                    new OrderWalletPayment(p.CustomerOrderPaymentId, p.PaymentMode, p.Amount, p.TransactionReference, p.PaidOn)).ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OrderWalletConfiguration?> GetConfigurationAsync(long storeId, DateTime effectiveAt, CancellationToken cancellationToken)
    {
        var settings = await db.CashbackConfigurations.AsNoTracking().Where(s => s.StoreId == storeId && s.IsActive == true &&
            (s.StartDate == null || s.StartDate <= effectiveAt) && (s.EndDate == null || s.EndDate >= effectiveAt))
            .Take(2).ToListAsync(cancellationToken);
        if (settings.Count > 1) throw new DomainException("Multiple active cashback settings exist for this store.");
        if (settings.Count == 0) return null;
        var setting = settings[0];
        var rules = await (from rule in db.CashbackSettingWallets.AsNoTracking()
            join type in db.WalletTypes.AsNoTracking() on rule.WalletTypeId equals type.Id
            where rule.CashbackSettingId == setting.CashbackSettingId && rule.IsActive && type.IsActive
                && (rule.IsNoExpiry || ((rule.StartDate == null || rule.StartDate <= effectiveAt)
                    && (rule.EndDate == null || rule.EndDate > effectiveAt)))
            orderby rule.WalletTypeId, rule.Id
            select new OrderWalletRule(rule.Id, rule.WalletTypeId, rule.PointPercentage,
                rule.IsNoExpiry, rule.StartDate, rule.EndDate)).ToListAsync(cancellationToken);
        return new OrderWalletConfiguration(setting, rules);
    }
}
