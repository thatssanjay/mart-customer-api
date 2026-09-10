using FluentValidation.TestHelper;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Controllers.V1;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Commands.TopUpWallet;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class WalletTopUpTests
{
    [Fact]
    public void Endpoint_UsesMobileCustomerTokenAndAcceptsNoCustomerId()
    {
        var method = typeof(CustomersController).GetMethod(nameof(CustomersController.TopUpWallet))!;
        var route = Assert.Single(method.GetCustomAttributes(typeof(HttpPostAttribute), false)
            .Cast<HttpPostAttribute>());
        var authorization = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>());

        Assert.Equal("wallet-top-ups", route.Template);
        Assert.Equal(MartAuthorizationPolicies.MobileCustomer, authorization.Policy);
        Assert.Contains(method.GetParameters(), parameter => parameter.ParameterType == typeof(IMartUserContext));
        Assert.DoesNotContain(method.GetParameters(), parameter =>
            string.Equals(parameter.Name, "customerId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_AddsBalancePaymentLedgerAndHistoryBucketTogether()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedMainWalletAsync(db, 5001, 51, 10m);
        var handler = scope.ServiceProvider.GetRequiredService<TopUpWalletCommandHandler>();

        var result = await handler.Handle(Command(5001, 20m, "CARD", "1234", null, "PAY-001"), default);

        Assert.Equal(10m, result.PreviousBalance);
        Assert.Equal(30m, result.NewBalance);
        Assert.Equal("WALLET", result.WalletCode);
        Assert.Equal(30m, (await db.CustomerWallets.SingleAsync()).CurrentBalance);
        var payment = await db.WalletTopUpPayments.SingleAsync();
        Assert.Equal(WalletTopUpPayment.SuccessfulStatus, payment.Status);
        Assert.Equal(result.WalletTransactionId, payment.WalletTransactionId);
        var transaction = await db.WalletTransactions.SingleAsync();
        Assert.Equal("WALLET_TOP_UP", transaction.ReferenceType);
        Assert.Equal(payment.WalletTopUpPaymentId, transaction.ReferenceId);
        Assert.Equal(20m, transaction.Amount);
        Assert.Equal(10m, transaction.BalanceBefore);
        Assert.Equal(30m, transaction.BalanceAfter);
        Assert.Equal(transaction.WalletTransactionId,
            (await db.WalletBalanceBuckets.SingleAsync()).SourceTransactionId);
    }

    [Fact]
    public async Task Handle_WhenMainWalletDoesNotExist_CreatesItAtZeroAndCreditsTopUp()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedWalletTypeAsync(db, 54, "WALLET");
        var handler = scope.ServiceProvider.GetRequiredService<TopUpWalletCommandHandler>();

        var result = await handler.Handle(
            Command(5004, 500m, "UPI", null, "UPI-004", "PAY-004"), default);

        Assert.Equal(0m, result.PreviousBalance);
        Assert.Equal(500m, result.NewBalance);
        var wallet = await db.CustomerWallets.SingleAsync();
        Assert.Equal(5004, wallet.CustomerId);
        Assert.Equal(54, wallet.WalletTypeId);
        Assert.Null(wallet.StoreId);
        Assert.Equal(500m, wallet.CurrentBalance);
        Assert.Equal(500m, wallet.TotalCredit);
        Assert.Single(await db.WalletTopUpPayments.ToListAsync());
        Assert.Single(await db.WalletTransactions.ToListAsync());
        Assert.Single(await db.WalletBalanceBuckets.ToListAsync());
    }

    [Fact]
    public async Task Handle_RepeatedPaymentReferenceReturnsOriginalWithoutCreditingAgain()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedMainWalletAsync(db, 5002, 52, 10m);
        var handler = scope.ServiceProvider.GetRequiredService<TopUpWalletCommandHandler>();
        var command = Command(5002, 20m, "UPI", null, "UPI-001", "PAY-002");

        var first = await handler.Handle(command, default);
        var repeated = await handler.Handle(command, default);

        Assert.Equal(first.WalletTopUpPaymentId, repeated.WalletTopUpPaymentId);
        Assert.Equal(30m, (await db.CustomerWallets.SingleAsync()).CurrentBalance);
        Assert.Equal(1, await db.WalletTopUpPayments.CountAsync());
        Assert.Equal(1, await db.WalletTransactions.CountAsync());
        Assert.Equal(1, await db.WalletBalanceBuckets.CountAsync());
    }

    [Fact]
    public async Task Handle_WhenOnlyRewardWalletExists_RejectsTopUp()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedWalletAsync(db, 5003, 53, "REWARD", 10m);
        var handler = scope.ServiceProvider.GetRequiredService<TopUpWalletCommandHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            Command(5003, 20m, "NETBANKING", null, "BANK-001", "PAY-003"), default));

        Assert.Equal("The WALLET type is not available.", exception.Message);
        Assert.Equal(10m, (await db.CustomerWallets.SingleAsync()).CurrentBalance);
        Assert.Empty(await db.WalletTopUpPayments.ToListAsync());
        Assert.Empty(await db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public void Validator_RejectsNonWalletAndInvalidPaymentDetails()
    {
        var validator = new TopUpWalletCommandValidator();

        validator.TestValidate(Command(1, 10m, "CARD", null, null, "PAY-A") with { WalletCode = "REWARD" })
            .ShouldHaveValidationErrorFor(command => command.WalletCode);
        validator.TestValidate(Command(1, 10m, "CARD", "12", null, "PAY-B"))
            .ShouldHaveValidationErrorFor(command => command.CardLast4);
        validator.TestValidate(Command(1, 10m, "UPI", null, null, "PAY-C"))
            .ShouldHaveValidationErrorFor(command => command.ReferenceNumber);
    }

    private static TopUpWalletCommand Command(
        long customerId,
        decimal amount,
        string paymentMode,
        string? cardLast4,
        string? referenceNumber,
        string paymentReference) => new(
            customerId, "WALLET", amount, paymentMode, cardLast4,
            referenceNumber, paymentReference, "top-up-tests");

    private static AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        Register<ICustomerWalletRepository>(services, "CustomerWalletRepository");
        Register<IWalletTypeRepository>(services, "WalletTypeRepository");
        Register<IWalletTransactionRepository>(services, "WalletTransactionRepository");
        Register<IWalletBalanceBucketRepository>(services, "WalletBalanceBucketRepository");
        Register<IWalletTopUpPaymentRepository>(services, "WalletTopUpPaymentRepository");
        Register<IUnitOfWork>(services, "UnitOfWork");
        services.AddScoped<TopUpWalletCommandHandler>();
        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private static void Register<TService>(IServiceCollection services, string name)
        where TService : class => services.AddScoped(typeof(TService),
            typeof(ApplicationDbContext).Assembly.GetType(
                $"Mart.Customer.Persistence.Repositories.{name}", true)!);

    private static Task SeedMainWalletAsync(
        ApplicationDbContext db, long customerId, int walletTypeId, decimal balance) =>
        SeedWalletAsync(db, customerId, walletTypeId, "WALLET", balance);

    private static async Task SeedWalletAsync(
        ApplicationDbContext db,
        long customerId,
        int walletTypeId,
        string walletCode,
        decimal balance)
    {
        await SeedWalletTypeAsync(db, walletTypeId, walletCode, saveChanges: false);
        var wallet = CustomerWallet.Create(customerId, walletTypeId, DateTime.UtcNow);
        Set(wallet, nameof(CustomerWallet.CurrentBalance), balance);
        Set(wallet, nameof(CustomerWallet.TotalCredit), balance);
        db.CustomerWallets.Add(wallet);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static async Task SeedWalletTypeAsync(
        ApplicationDbContext db,
        int walletTypeId,
        string walletCode,
        bool saveChanges = true)
    {
        var type = (WalletType)Activator.CreateInstance(typeof(WalletType), true)!;
        Set(type, nameof(WalletType.Id), walletTypeId);
        Set(type, nameof(WalletType.Name), walletCode);
        Set(type, nameof(WalletType.Code), walletCode);
        Set(type, nameof(WalletType.IsActive), true);
        db.WalletTypes.Add(type);
        if (saveChanges)
        {
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }

    private static void Set<T>(T target, string propertyName, object value) where T : class =>
        typeof(T).GetProperty(propertyName)!.SetValue(target, value);
}
