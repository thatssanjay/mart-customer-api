using Mart.Customer.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Tests.Orders;

public sealed class CustomerOrderSqlServerMappingTests
{
    [Fact]
    public void OrderQueries_UseTheBillingV2SqlColumnNames()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Mart;Trusted_Connection=True")
            .Options;
        using var dbContext = new ApplicationDbContext(options);

        var orderSql = dbContext.CustomerOrders.AsNoTracking().ToQueryString();
        Assert.Contains("[InvoiceDate]", orderSql);
        Assert.Contains("[SubTotalAmount]", orderSql);
        Assert.Contains("[TaxableAmount]", orderSql);
        Assert.Contains("[RedeemAmount]", orderSql);
        Assert.Contains("[RewardPointsEarned]", orderSql);
        Assert.Contains("[IsPointsAwarded]", orderSql);
        Assert.DoesNotContain("[IsRewardPointAwarded]", orderSql);
        Assert.Contains("[CashbackAmount]", orderSql);
        Assert.Contains("[CashierUserId]", orderSql);
        Assert.DoesNotContain("[GrossAmount]", orderSql);
        Assert.DoesNotContain("[InvoiceStatus]", orderSql);

        var itemSql = dbContext.CustomerOrderItems.AsNoTracking().ToQueryString();
        Assert.Contains("[TaxableAmount]", itemSql);
        Assert.DoesNotContain("[CustomerCartItemId]", itemSql);
        Assert.DoesNotContain("[GrossAmount]", itemSql);

        var paymentSql = dbContext.CustomerOrderPayments.AsNoTracking().ToQueryString();
        Assert.Contains("[PaymentModeCode]", paymentSql);
        Assert.Contains("[ReferenceNumber]", paymentSql);
        Assert.DoesNotContain("[TransactionReference]", paymentSql);
    }
}
