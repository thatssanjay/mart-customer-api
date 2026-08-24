using System.Net;
using System.Net.Http.Json;
using FluentValidation.TestHelper;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Application.Wallets.Queries.GetWalletTransactionByNumber;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class GetWalletTransactionByNumberTests
    : IClassFixture<GetCustomerWalletsApiFactory>
{
    private readonly GetCustomerWalletsApiFactory _factory;

    public GetWalletTransactionByNumberTests(GetCustomerWalletsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetByTransactionNumber_WhenFound_ReturnsTransactionWalletAndWalletTypeProjection()
    {
        var transactionDate = new DateTime(2026, 8, 20, 12, 30, 0, DateTimeKind.Utc);
        await SeedAsync(
            customerId: 8101,
            walletTypeId: 811,
            transactionId: 81001,
            transactionNumber: "TX-SUPPORT-81001",
            transactionDate,
            isWalletActive: true,
            isWalletTypeActive: true);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/wallet-transactions/TX-SUPPORT-81001",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<WalletTransactionDetailDto>(
            CancellationToken.None);
        Assert.NotNull(transaction);
        Assert.Equal(81001, transaction.WalletTransactionId);
        Assert.Equal("TX-SUPPORT-81001", transaction.TransactionNumber);
        Assert.Equal(8101, transaction.CustomerId);
        Assert.Equal(811, transaction.WalletTypeId);
        Assert.Equal("Wallet 811", transaction.WalletTypeName);
        Assert.Equal("WALLET_811", transaction.WalletTypeCode);
        Assert.True(transaction.IsWalletActive);
        Assert.True(transaction.IsWalletTypeActive);
        Assert.Equal("CREDIT", transaction.TransactionType);
        Assert.Equal(25m, transaction.Amount);
        Assert.Equal(5m, transaction.BalanceBefore);
        Assert.Equal(30m, transaction.BalanceAfter);
        Assert.Equal("ORDER", transaction.ReferenceType);
        Assert.Equal(4567, transaction.ReferenceId);
        Assert.Equal("Support lookup test", transaction.Remarks);
        Assert.Equal(transactionDate, transaction.TransactionDate);
        Assert.Equal("wallet-service", transaction.CreatedBy);
    }

    [Fact]
    public async Task GetByTransactionNumber_WhenTransactionDoesNotExist_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/wallet-transactions/TX-DOES-NOT-EXIST",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByTransactionNumber_WhenTransactionNumberIsTooLong_ReturnsStandardValidationProblem()
    {
        using var client = _factory.CreateClient();
        var transactionNumber = new string('T', 51);

        using var response = await client.GetAsync(
            $"/api/wallet-transactions/{transactionNumber}",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(
            CancellationToken.None);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
    }

    [Fact]
    public async Task GetByTransactionNumber_WhenWalletAndTypeAreInactive_StillReturnsHistoricalTransaction()
    {
        await SeedAsync(
            customerId: 8201,
            walletTypeId: 821,
            transactionId: 82001,
            transactionNumber: "TX-INACTIVE-82001",
            transactionDate: DateTime.UtcNow,
            isWalletActive: false,
            isWalletTypeActive: false);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/wallet-transactions/TX-INACTIVE-82001",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<WalletTransactionDetailDto>(
            CancellationToken.None);
        Assert.NotNull(transaction);
        Assert.False(transaction.IsWalletActive);
        Assert.False(transaction.IsWalletTypeActive);
    }

    [Fact]
    public async Task GetByTransactionNumber_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/wallet-transactions/TX-SUPPORT-81001",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Validator_WhenTransactionNumberIsBlankOrTooLong_ReturnsFailures()
    {
        var validator = new GetWalletTransactionByNumberQueryValidator();

        validator.TestValidate(new GetWalletTransactionByNumberQuery("   "))
            .ShouldHaveValidationErrorFor(query => query.TransactionNumber);
        validator.TestValidate(new GetWalletTransactionByNumberQuery(new string('T', 51)))
            .ShouldHaveValidationErrorFor(query => query.TransactionNumber);
    }

    private async Task SeedAsync(
        long customerId,
        int walletTypeId,
        long transactionId,
        string transactionNumber,
        DateTime transactionDate,
        bool isWalletActive,
        bool isWalletTypeActive)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var walletType = CreateWalletType(walletTypeId, isWalletTypeActive);
        var wallet = CustomerWallet.Create(customerId, walletTypeId, DateTime.UtcNow);
        SetProperty(wallet, nameof(CustomerWallet.IsActive), isWalletActive);
        dbContext.WalletTypes.Add(walletType);
        dbContext.CustomerWallets.Add(wallet);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        dbContext.WalletTransactions.Add(CreateTransaction(
            transactionId,
            wallet.CustomerWalletId,
            transactionNumber,
            transactionDate));
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private static WalletType CreateWalletType(int id, bool isActive)
    {
        var walletType = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
        SetProperty(walletType, nameof(WalletType.Id), id);
        SetProperty(walletType, nameof(WalletType.Name), $"Wallet {id}");
        SetProperty(walletType, nameof(WalletType.Code), $"WALLET_{id}");
        SetProperty(walletType, nameof(WalletType.IsActive), isActive);
        SetProperty(walletType, nameof(WalletType.CreatedDate), DateTime.UtcNow);
        return walletType;
    }

    private static WalletTransaction CreateTransaction(
        long id,
        long customerWalletId,
        string transactionNumber,
        DateTime transactionDate)
    {
        var transaction = (WalletTransaction)Activator.CreateInstance(
            typeof(WalletTransaction),
            nonPublic: true)!;
        SetProperty(transaction, nameof(WalletTransaction.WalletTransactionId), id);
        SetProperty(transaction, nameof(WalletTransaction.CustomerWalletId), customerWalletId);
        SetProperty(transaction, nameof(WalletTransaction.TransactionNumber), transactionNumber);
        SetProperty(transaction, nameof(WalletTransaction.TransactionType), "CREDIT");
        SetProperty(transaction, nameof(WalletTransaction.Amount), 25m);
        SetProperty(transaction, nameof(WalletTransaction.BalanceBefore), 5m);
        SetProperty(transaction, nameof(WalletTransaction.BalanceAfter), 30m);
        SetProperty(transaction, nameof(WalletTransaction.ReferenceType), "ORDER");
        SetProperty(transaction, nameof(WalletTransaction.ReferenceId), 4567L);
        SetProperty(transaction, nameof(WalletTransaction.Remarks), "Support lookup test");
        SetProperty(transaction, nameof(WalletTransaction.TransactionDate), transactionDate);
        SetProperty(transaction, nameof(WalletTransaction.CreatedBy), "wallet-service");
        return transaction;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
        where TTarget : class
    {
        typeof(TTarget).GetProperty(propertyName)!.SetValue(target, value);
    }
}
