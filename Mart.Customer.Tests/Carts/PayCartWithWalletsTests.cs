using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Mart.Customer.Application.Carts.Dtos;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Mart.Customer.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Mart.Customer.Tests.Carts;

public sealed class PayCartWithWalletsTests : IClassFixture<GetCartsAuthenticationFactory>
{
    private readonly GetCartsAuthenticationFactory _factory;

    public PayCartWithWalletsTests(GetCartsAuthenticationFactory factory) => _factory = factory;

    [Theory]
    [InlineData(1000, 0, 1000, 0)]
    [InlineData(0, 1000, 0, 1000)]
    [InlineData(900, 300, 900, 100)]
    public async Task SelectedWallets_PayExactBill_AndCreateOneRedemptionPerWallet(
        decimal walletBalance,
        decimal storeWalletBalance,
        decimal walletDeduction,
        decimal storeWalletDeduction)
    {
        var customerId = Random.Shared.NextInt64(700_000, 799_999);
        var seeded = await SeedAsync(customerId, 1000m, walletBalance, storeWalletBalance);
        using var client = CreateClient(customerId);
        var deductions = new List<object>();
        if (walletDeduction > 0)
            deductions.Add(new { walletCode = "WALLET", amount = walletDeduction });
        if (storeWalletDeduction > 0)
            deductions.Add(new { walletCode = "MART_WALLET", amount = storeWalletDeduction });

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/customer/carts/{seeded.CartId}/wallet-payment",
            new { cartNumber = seeded.CartNumber, deductions });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CartWalletPaymentResultDto>();
        Assert.NotNull(result);
        Assert.Equal(1000m, result.PaidAmount);
        Assert.Equal("PAID", result.PaymentStatus);
        Assert.Equal(deductions.Count, result.Deductions.Count);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallets = await db.CustomerWallets.AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .OrderBy(item => item.StoreId)
            .ToListAsync();
        Assert.Equal(walletBalance - walletDeduction, wallets.Single(item => item.StoreId == null).CurrentBalance);
        Assert.Equal(storeWalletBalance - storeWalletDeduction, wallets.Single(item => item.StoreId == 11).CurrentBalance);
        Assert.Equal(deductions.Count, await db.WalletTransactions.CountAsync(item =>
            item.ReferenceType == "CART_PAYMENT" && item.ReferenceId == seeded.CartId));
        Assert.Equal("Paid", (await db.CustomerCarts.AsNoTracking()
            .SingleAsync(item => item.CustomerCartId == seeded.CartId)).CartStatus);
    }

    [Fact]
    public async Task PaymentTotalBelowBill_IsRejectedWithoutAnyDeduction()
    {
        const long customerId = 810_001;
        var seeded = await SeedAsync(customerId, 1000m, 900m, 300m);
        using var client = CreateClient(customerId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/customer/carts/{seeded.CartId}/wallet-payment",
            new
            {
                cartNumber = seeded.CartNumber,
                deductions = new object[]
                {
                    new { walletCode = "WALLET", amount = 899m },
                    new { walletCode = "MART_WALLET", amount = 100m }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUnchangedAsync(customerId, seeded.CartId, 900m, 300m);
    }

    [Fact]
    public async Task StoreWalletFromAnotherStore_IsRejected()
    {
        const long customerId = 810_002;
        var seeded = await SeedAsync(customerId, 100m, 0m, 0m, storeWalletStoreId: 12, storeWalletBalanceOverride: 100m);
        using var client = CreateClient(customerId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/customer/carts/{seeded.CartId}/wallet-payment",
            new
            {
                cartNumber = seeded.CartNumber,
                deductions = new[] { new { walletCode = "MART_WALLET", amount = 100m } }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(100m, (await db.CustomerWallets.AsNoTracking()
            .SingleAsync(item => item.CustomerId == customerId && item.StoreId == 12)).CurrentBalance);
        Assert.Empty(await db.WalletTransactions.Where(item =>
            item.ReferenceType == "CART_PAYMENT" && item.ReferenceId == seeded.CartId).ToListAsync());
    }

    [Fact]
    public async Task StoreWalletWithLegacyUnbucketedBalance_PaysFromAuthenticatedCustomersCurrentStoreWallet()
    {
        const long customerId = 810_004;
        var seeded = await SeedAsync(customerId, 100m, 0m, 100m);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var storeWalletId = await db.CustomerWallets
                .Where(item => item.CustomerId == customerId && item.StoreId == 11)
                .Select(item => item.CustomerWalletId)
                .SingleAsync();
            await db.WalletBalanceBuckets
                .Where(item => item.CustomerWalletId == storeWalletId)
                .ExecuteDeleteAsync();
        }

        using var client = CreateClient(customerId);
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/customer/carts/{seeded.CartId}/wallet-payment",
            new
            {
                cartNumber = seeded.CartNumber,
                deductions = new[] { new { walletCode = "MART_WALLET", amount = 100m } }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0m, (await verificationDb.CustomerWallets.AsNoTracking()
            .SingleAsync(item => item.CustomerId == customerId && item.StoreId == 11)).CurrentBalance);
        Assert.Equal(100m, (await verificationDb.WalletTransactions.AsNoTracking()
            .SingleAsync(item => item.ReferenceType == "CART_PAYMENT" && item.ReferenceId == seeded.CartId)).Amount);
    }

    [Fact]
    public async Task RepeatedPayment_IsBlockedAndCartIsPaidOnlyOnce()
    {
        const long customerId = 810_003;
        var seeded = await SeedAsync(customerId, 100m, 100m, 0m);
        using var client = CreateClient(customerId);
        var request = new
        {
            cartNumber = seeded.CartNumber,
            deductions = new[] { new { walletCode = "WALLET", amount = 100m } }
        };

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            $"/api/v1/customer/carts/{seeded.CartId}/wallet-payment", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            $"/api/v1/customer/carts/{seeded.CartId}/wallet-payment", request)).StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0m, (await db.CustomerWallets.AsNoTracking()
            .SingleAsync(item => item.CustomerId == customerId && item.StoreId == null)).CurrentBalance);
        Assert.Single(await db.WalletTransactions.Where(item =>
            item.ReferenceType == "CART_PAYMENT" && item.ReferenceId == seeded.CartId).ToListAsync());
        Assert.Equal("Paid", (await db.CustomerCarts.AsNoTracking()
            .SingleAsync(item => item.CustomerCartId == seeded.CartId)).CartStatus);
    }

    private async Task<(long CartId, string CartNumber)> SeedAsync(
        long customerId,
        decimal bill,
        decimal walletBalance,
        decimal storeWalletBalance,
        long storeWalletStoreId = 11,
        decimal? storeWalletBalanceOverride = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var walletType = await EnsureWalletTypeAsync(db, 91_001, "WALLET", "Wallet");
        var storeWalletType = await EnsureWalletTypeAsync(db, 91_002, "MART_WALLET", "Store Wallet");
        var cart = CustomerCart.Create(customerId, 7, 11, $"PAY-{Guid.NewGuid():N}", 41);
        cart.AddItem(1, "Payment item", 1, bill, bill, 0, 0, 41);
        cart.ChangeStatus("PaymentPending");
        db.CustomerCarts.Add(cart);
        await db.SaveChangesAsync();

        await SeedWalletAsync(db, customerId, walletType.Id, walletBalance, null);
        await SeedWalletAsync(
            db,
            customerId,
            storeWalletType.Id,
            storeWalletBalanceOverride ?? storeWalletBalance,
            storeWalletStoreId);
        return (cart.CustomerCartId, cart.CartNumber);
    }

    private static async Task<WalletType> EnsureWalletTypeAsync(
        ApplicationDbContext db,
        int id,
        string code,
        string name)
    {
        var existing = await db.WalletTypes.SingleOrDefaultAsync(item => item.Code == code);
        if (existing is not null) return existing;
        var walletType = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
        db.WalletTypes.Add(walletType);
        db.Entry(walletType).Property(item => item.Id).CurrentValue = id;
        db.Entry(walletType).Property(item => item.Code).CurrentValue = code;
        db.Entry(walletType).Property(item => item.Name).CurrentValue = name;
        db.Entry(walletType).Property(item => item.DisplayOrder).CurrentValue = id;
        db.Entry(walletType).Property(item => item.IsActive).CurrentValue = true;
        db.Entry(walletType).Property(item => item.CreatedDate).CurrentValue = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return walletType;
    }

    private static async Task SeedWalletAsync(
        ApplicationDbContext db,
        long customerId,
        int walletTypeId,
        decimal balance,
        long? storeId)
    {
        var now = DateTime.UtcNow;
        var wallet = CustomerWallet.Create(customerId, walletTypeId, now, storeId);
        db.CustomerWallets.Add(wallet);
        await db.SaveChangesAsync();
        if (balance <= 0) return;

        wallet.Credit(balance, now);
        var transaction = WalletTransaction.CreateCredit(
            $"TX-{Guid.NewGuid():N}", wallet.CustomerWalletId, balance, 0, balance,
            "TEST_SEED", customerId, "Payment test balance", now, "tests");
        db.WalletTransactions.Add(transaction);
        await db.SaveChangesAsync();
        db.WalletBalanceBuckets.Add(WalletBalanceBucket.Create(
            wallet.CustomerWalletId, transaction, balance, null, now));
        await db.SaveChangesAsync();
    }

    private async Task AssertUnchangedAsync(
        long customerId,
        long cartId,
        decimal walletBalance,
        decimal storeWalletBalance)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(walletBalance, (await db.CustomerWallets.AsNoTracking()
            .SingleAsync(item => item.CustomerId == customerId && item.StoreId == null)).CurrentBalance);
        Assert.Equal(storeWalletBalance, (await db.CustomerWallets.AsNoTracking()
            .SingleAsync(item => item.CustomerId == customerId && item.StoreId == 11)).CurrentBalance);
        Assert.Empty(await db.WalletTransactions.Where(item =>
            item.ReferenceType == "CART_PAYMENT" && item.ReferenceId == cartId).ToListAsync());
        Assert.Equal("PaymentPending", (await db.CustomerCarts.AsNoTracking()
            .SingleAsync(item => item.CustomerCartId == cartId)).CartStatus);
    }

    private HttpClient CreateClient(long customerId)
    {
        var client = _factory.CreateClient();
        var token = new JwtSecurityToken(
            "customer-tests",
            "cart-tests",
            [new Claim(MartTokenClaims.UserId, customerId.ToString()), new Claim(MartTokenClaims.LoginType, "customer")],
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetCartsAuthenticationFactory.CustomerKey)),
                SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
