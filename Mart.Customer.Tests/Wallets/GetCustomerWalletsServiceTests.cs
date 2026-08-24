using FluentValidation.TestHelper;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletDetail;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWallets;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class GetCustomerWalletsServiceTests
{
    [Fact]
    public async Task GetDetailAsync_WhenWalletAndTypeAreActive_ReturnsProjectedDetailWithoutTracking()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletTypes.Add(CreateWalletType(
            1,
            "Cashback",
            "CASHBACK",
            true,
            "Cashback balance"));
        var customerWallet = CreateCustomerWallet(101, 1, true, 25.50m);
        SetProperty(customerWallet, nameof(CustomerWallet.TotalCredit), 30m);
        SetProperty(customerWallet, nameof(CustomerWallet.TotalDebit), 4m);
        SetProperty(customerWallet, nameof(CustomerWallet.TotalExpired), 0.50m);
        dbContext.CustomerWallets.Add(customerWallet);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerWalletRepository>();

        var result = await repository.GetDetailAsync(101, 1, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(101, result.CustomerId);
        Assert.Equal(1, result.WalletTypeId);
        Assert.Equal("Cashback", result.WalletTypeName);
        Assert.Equal("CASHBACK", result.WalletTypeCode);
        Assert.Equal("Cashback balance", result.WalletTypeDescription);
        Assert.Equal(25.50m, result.CurrentBalance);
        Assert.Equal(30m, result.TotalCredit);
        Assert.Equal(4m, result.TotalDebit);
        Assert.Equal(0.50m, result.TotalExpired);
        Assert.True(result.IsActive);
        Assert.True(result.IsWalletTypeActive);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetDetailAsync_WhenWalletOrWalletTypeIsInactive_ReturnsNull()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletTypes.AddRange(
            CreateWalletType(1, "Active", "ACTIVE", true, null),
            CreateWalletType(2, "Inactive", "INACTIVE", false, null));
        dbContext.CustomerWallets.AddRange(
            CreateCustomerWallet(101, 1, false, 1m),
            CreateCustomerWallet(101, 2, true, 2m));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerWalletRepository>();

        var inactiveWallet = await repository.GetDetailAsync(101, 1, CancellationToken.None);
        var inactiveWalletType = await repository.GetDetailAsync(101, 2, CancellationToken.None);

        Assert.Null(inactiveWallet);
        Assert.Null(inactiveWalletType);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetDetailAsync_WhenWalletBelongsToAnotherCustomer_ReturnsNull()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletTypes.Add(CreateWalletType(1, "Cashback", "CASHBACK", true, null));
        dbContext.CustomerWallets.Add(CreateCustomerWallet(202, 1, true, 10m));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerWalletRepository>();

        var result = await repository.GetDetailAsync(101, 1, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDetailsByCustomerIdAsync_ByDefault_ReturnsOnlyActiveWalletsWithActiveTypes()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletTypes.AddRange(
            CreateWalletType(1, "Cashback", "CASHBACK", true, "Cashback balance"),
            CreateWalletType(2, "Archived", "ARCHIVED", false, null),
            CreateWalletType(3, "Rewards", "REWARDS", true, "Reward points"));
        dbContext.CustomerWallets.AddRange(
            CreateCustomerWallet(101, 1, true, 25.50m),
            CreateCustomerWallet(101, 2, true, 10m),
            CreateCustomerWallet(101, 3, false, 5m),
            CreateCustomerWallet(202, 1, true, 99m));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerWalletRepository>();

        var result = await repository.GetDetailsByCustomerIdAsync(
            101,
            includeInactive: false,
            CancellationToken.None);

        var wallet = Assert.Single(result);
        Assert.Equal(101, wallet.CustomerId);
        Assert.Equal(1, wallet.WalletTypeId);
        Assert.Equal("Cashback", wallet.WalletTypeName);
        Assert.Equal("CASHBACK", wallet.WalletTypeCode);
        Assert.Equal("Cashback balance", wallet.WalletTypeDescription);
        Assert.Equal(25.50m, wallet.CurrentBalance);
        Assert.True(wallet.IsActive);
        Assert.True(wallet.IsWalletTypeActive);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetDetailsByCustomerIdAsync_WhenIncludeInactive_IncludesBothInactiveStatuses()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletTypes.AddRange(
            CreateWalletType(1, "Active type", "ACTIVE", true, null),
            CreateWalletType(2, "Inactive type", "INACTIVE", false, null));
        dbContext.CustomerWallets.AddRange(
            CreateCustomerWallet(101, 1, false, 1m),
            CreateCustomerWallet(101, 2, true, 2m));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerWalletRepository>();

        var result = await repository.GetDetailsByCustomerIdAsync(
            101,
            includeInactive: true,
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, wallet => !wallet.IsActive && wallet.IsWalletTypeActive);
        Assert.Contains(result, wallet => wallet.IsActive && !wallet.IsWalletTypeActive);
    }

    [Fact]
    public async Task GetDetailsByCustomerIdAsync_WhenNoJoinedWalletMatches_ReturnsEmptyList()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletTypes.Add(CreateWalletType(1, "Cashback", "CASHBACK", true, null));
        dbContext.CustomerWallets.Add(CreateCustomerWallet(202, 1, true, 10m));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerWalletRepository>();

        var result = await repository.GetDetailsByCustomerIdAsync(
            101,
            includeInactive: false,
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDetailsByCustomerIdAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        await using var scope = CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerWalletRepository>();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetDetailsByCustomerIdAsync(
                101,
                includeInactive: false,
                cancellationTokenSource.Token));
    }

    [Fact]
    public void Validator_WhenCustomerIdIsNotPositive_ReturnsValidationFailure()
    {
        var validator = new GetCustomerWalletsQueryValidator();

        var result = validator.TestValidate(new GetCustomerWalletsQuery(0, false));

        result.ShouldHaveValidationErrorFor(query => query.CustomerId);
    }

    [Fact]
    public void DetailValidator_WhenIdentifiersAreNotPositive_ReturnsValidationFailures()
    {
        var validator = new GetCustomerWalletDetailQueryValidator();

        var result = validator.TestValidate(new GetCustomerWalletDetailQuery(0, 0));

        result.ShouldHaveValidationErrorFor(query => query.CustomerId);
        result.ShouldHaveValidationErrorFor(query => query.WalletTypeId);
    }

    private static AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var repositoryType = typeof(ApplicationDbContext).Assembly.GetType(
            "Mart.Customer.Persistence.Repositories.CustomerWalletRepository",
            throwOnError: true)!;
        services.AddScoped(typeof(ICustomerWalletRepository), repositoryType);

        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private static WalletType CreateWalletType(
        int id,
        string name,
        string code,
        bool isActive,
        string? description)
    {
        var walletType = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
        SetProperty(walletType, nameof(WalletType.Id), id);
        SetProperty(walletType, nameof(WalletType.Name), name);
        SetProperty(walletType, nameof(WalletType.Code), code);
        SetProperty(walletType, nameof(WalletType.Description), description);
        SetProperty(walletType, nameof(WalletType.IsActive), isActive);
        SetProperty(walletType, nameof(WalletType.CreatedDate), DateTime.UtcNow);
        return walletType;
    }

    private static CustomerWallet CreateCustomerWallet(
        long customerId,
        int walletTypeId,
        bool isActive,
        decimal currentBalance)
    {
        var wallet = CustomerWallet.Create(customerId, walletTypeId, DateTime.UtcNow);
        SetProperty(wallet, nameof(CustomerWallet.IsActive), isActive);
        SetProperty(wallet, nameof(CustomerWallet.CurrentBalance), currentBalance);
        return wallet;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
        where TTarget : class
    {
        typeof(TTarget).GetProperty(propertyName)!.SetValue(target, value);
    }
}
