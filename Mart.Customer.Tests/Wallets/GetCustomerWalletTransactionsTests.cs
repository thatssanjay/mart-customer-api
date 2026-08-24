using System.Net;
using System.Net.Http.Json;
using FluentValidation.TestHelper;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletTransactions;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class GetCustomerWalletTransactionsTests
    : IClassFixture<GetCustomerWalletsApiFactory>
{
    private readonly GetCustomerWalletsApiFactory _factory;

    public GetCustomerWalletTransactionsTests(GetCustomerWalletsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetTransactions_WhenFilteredAndPaged_ReturnsNewestMatchingProjection()
    {
        var customerWalletId = await SeedWalletAsync(7101, 711, isWalletTypeActive: true);
        var otherCustomerWalletId = await SeedWalletAsync(7102, 712, isWalletTypeActive: true);
        var firstDate = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        await SeedTransactionsAsync(
            CreateTransaction(71001, customerWalletId, "TX-71001", "CREDIT", 10m, 0m, 10m, "ORDER", 901, firstDate, "First credit"),
            CreateTransaction(71002, customerWalletId, "TX-71002", "DEBIT", 3m, 10m, 7m, "ORDER", 902, firstDate.AddHours(1), "Redeem"),
            CreateTransaction(71003, customerWalletId, "TX-71003", "CREDIT", 5m, 7m, 12m, "ORDER", 903, firstDate.AddHours(2), "Latest credit"),
            CreateTransaction(71004, otherCustomerWalletId, "TX-71004", "CREDIT", 99m, 0m, 99m, "ORDER", 904, firstDate.AddHours(3), "Other wallet"));

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/7101/wallets/711/transactions?pageNumber=1&pageSize=1&transactionType=CREDIT&referenceType=ORDER",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<WalletTransactionDto>>(
            CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(1, result.PageNumber);
        var transaction = Assert.Single(result.Items);
        Assert.Equal(71003, transaction.WalletTransactionId);
        Assert.Equal("TX-71003", transaction.TransactionNumber);
        Assert.Equal("CREDIT", transaction.TransactionType);
        Assert.Equal(5m, transaction.Amount);
        Assert.Equal(7m, transaction.BalanceBefore);
        Assert.Equal(12m, transaction.BalanceAfter);
        Assert.Equal("ORDER", transaction.ReferenceType);
        Assert.Equal(903, transaction.ReferenceId);
        Assert.Equal("Latest credit", transaction.Remarks);
    }

    [Fact]
    public async Task GetTransactions_WhenNoTransactionsMatch_ReturnsEmptyPage()
    {
        await SeedWalletAsync(7201, 721, isWalletTypeActive: true);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/7201/wallets/721/transactions?transactionType=REFUND",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<WalletTransactionDto>>(
            CancellationToken.None);
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetTransactions_WhenPaginationIsInvalid_ReturnsStandardValidationProblem()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/customers/7301/wallets/731/transactions?pageSize=0",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(
            CancellationToken.None);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
    }

    [Fact]
    public async Task GetTransactions_WhenWalletTypeIsInactive_ReturnsNotFound()
    {
        await SeedWalletAsync(7401, 741, isWalletTypeActive: false);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/7401/wallets/741/transactions",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactions_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/customers/7101/wallets/711/transactions",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Validator_WhenIdentifiersPagingOrDateRangeAreInvalid_ReturnsFailures()
    {
        var validator = new GetCustomerWalletTransactionsQueryValidator();
        var fromDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc);

        var result = validator.TestValidate(new GetCustomerWalletTransactionsQuery(
            0,
            0,
            PageNumber: 0,
            PageSize: 101,
            FromDate: fromDate,
            ToDate: fromDate.AddDays(-1),
            ReferenceId: 0));

        result.ShouldHaveValidationErrorFor(query => query.CustomerId);
        result.ShouldHaveValidationErrorFor(query => query.WalletTypeId);
        result.ShouldHaveValidationErrorFor(query => query.PageNumber);
        result.ShouldHaveValidationErrorFor(query => query.PageSize);
        result.ShouldHaveValidationErrorFor(query => query.ToDate);
        result.ShouldHaveValidationErrorFor(query => query.ReferenceId);
    }

    private async Task<long> SeedWalletAsync(
        long customerId,
        int walletTypeId,
        bool isWalletTypeActive)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletTypes.Add(CreateWalletType(walletTypeId, isWalletTypeActive));
        var wallet = CustomerWallet.Create(customerId, walletTypeId, DateTime.UtcNow);
        dbContext.CustomerWallets.Add(wallet);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return wallet.CustomerWalletId;
    }

    private async Task SeedTransactionsAsync(params WalletTransaction[] transactions)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletTransactions.AddRange(transactions);
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
        string transactionType,
        decimal amount,
        decimal balanceBefore,
        decimal balanceAfter,
        string? referenceType,
        long? referenceId,
        DateTime transactionDate,
        string? remarks)
    {
        var transaction = (WalletTransaction)Activator.CreateInstance(
            typeof(WalletTransaction),
            nonPublic: true)!;
        SetProperty(transaction, nameof(WalletTransaction.WalletTransactionId), id);
        SetProperty(transaction, nameof(WalletTransaction.CustomerWalletId), customerWalletId);
        SetProperty(transaction, nameof(WalletTransaction.TransactionNumber), transactionNumber);
        SetProperty(transaction, nameof(WalletTransaction.TransactionType), transactionType);
        SetProperty(transaction, nameof(WalletTransaction.Amount), amount);
        SetProperty(transaction, nameof(WalletTransaction.BalanceBefore), balanceBefore);
        SetProperty(transaction, nameof(WalletTransaction.BalanceAfter), balanceAfter);
        SetProperty(transaction, nameof(WalletTransaction.ReferenceType), referenceType);
        SetProperty(transaction, nameof(WalletTransaction.ReferenceId), referenceId);
        SetProperty(transaction, nameof(WalletTransaction.Remarks), remarks);
        SetProperty(transaction, nameof(WalletTransaction.TransactionDate), transactionDate);
        return transaction;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
        where TTarget : class
    {
        typeof(TTarget).GetProperty(propertyName)!.SetValue(target, value);
    }
}
