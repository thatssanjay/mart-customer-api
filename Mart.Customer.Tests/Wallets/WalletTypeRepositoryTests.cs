using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class WalletTypeRepositoryTests
{
    [Fact]
    public async Task GetAsync_WhenActiveOnly_ReturnsOnlyActiveWalletTypesInNameOrderWithoutTracking()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedAsync(
            dbContext,
            CreateWalletType(3, "Rewards", "REWARDS", true, "Reward points"),
            CreateWalletType(2, "Cashback", "CASHBACK_OLD", false, null),
            CreateWalletType(1, "Cashback", "CASHBACK", true, "Cashback balance"));

        var repository = scope.ServiceProvider.GetRequiredService<IWalletTypeRepository>();

        var result = await repository.GetAsync(true, CancellationToken.None);

        Assert.Collection(
            result,
            walletType =>
            {
                Assert.Equal(1, walletType.Id);
                Assert.Equal("Cashback", walletType.Name);
                Assert.Equal("CASHBACK", walletType.Code);
                Assert.Equal("Cashback balance", walletType.Description);
                Assert.True(walletType.IsActive);
            },
            walletType => Assert.Equal(3, walletType.Id));
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetAsync_WhenActiveOnlyIsFalse_IncludesInactiveWalletTypes()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedAsync(
            dbContext,
            CreateWalletType(2, "Rewards", "REWARDS", true, null),
            CreateWalletType(1, "Archived", "ARCHIVED", false, null));

        var repository = scope.ServiceProvider.GetRequiredService<IWalletTypeRepository>();

        var result = await repository.GetAsync(false, CancellationToken.None);

        Assert.Equal([1, 2], result.Select(walletType => walletType.Id));
        Assert.False(result[0].IsActive);
    }

    [Fact]
    public async Task GetAsync_WhenNoWalletTypesMatch_ReturnsEmptyList()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedAsync(dbContext, CreateWalletType(1, "Archived", "ARCHIVED", false, null));

        var repository = scope.ServiceProvider.GetRequiredService<IWalletTypeRepository>();

        var result = await repository.GetAsync(true, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        await using var scope = CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWalletTypeRepository>();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetAsync(true, cancellationTokenSource.Token));
    }

    private static AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var repositoryType = typeof(ApplicationDbContext).Assembly.GetType(
            "Mart.Customer.Persistence.Repositories.WalletTypeRepository",
            throwOnError: true)!;
        services.AddScoped(typeof(IWalletTypeRepository), repositoryType);

        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private static async Task SeedAsync(
        ApplicationDbContext dbContext,
        params WalletType[] walletTypes)
    {
        dbContext.WalletTypes.AddRange(walletTypes);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();
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

    private static void SetProperty<T>(WalletType walletType, string propertyName, T value)
    {
        typeof(WalletType).GetProperty(propertyName)!.SetValue(walletType, value);
    }
}
