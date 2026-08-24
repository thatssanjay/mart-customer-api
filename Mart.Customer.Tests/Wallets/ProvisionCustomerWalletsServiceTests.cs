using FluentValidation.TestHelper;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Commands.ProvisionCustomerWallets;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CustomerEntity = Mart.Customer.Domain.Customers.Customer;

namespace Mart.Customer.Tests.Wallets;

public sealed class ProvisionCustomerWalletsServiceTests
{
    [Fact]
    public async Task Handle_CreatesOnlyMissingActiveWallets_AndRepeatedCallSkipsAll()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var customer = CreateCustomer(20011);
        var existingWallet = CustomerWallet.Create(20011, 1, DateTime.UtcNow);
        var inactiveTypeWallet = CustomerWallet.Create(20011, 4, DateTime.UtcNow);
        dbContext.Customers.Add(customer);
        dbContext.WalletTypes.AddRange(
            CreateWalletType(1, "Reward", "REWARD", true),
            CreateWalletType(2, "Wallet", "WALLET", true),
            CreateWalletType(3, "Mart Wallet", "MART_WALLET", true),
            CreateWalletType(4, "Archived", "ARCHIVED", false));
        dbContext.CustomerWallets.AddRange(existingWallet, inactiveTypeWallet);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<CountingUnitOfWork>();
        var handler = scope.ServiceProvider.GetRequiredService<ProvisionCustomerWalletsCommandHandler>();

        var firstResult = await handler.Handle(
            new ProvisionCustomerWalletsCommand(20011),
            CancellationToken.None);
        var secondResult = await handler.Handle(
            new ProvisionCustomerWalletsCommand(20011),
            CancellationToken.None);

        Assert.Equal(20011, firstResult.CustomerId);
        Assert.Equal([3, 2], firstResult.Created.Select(wallet => wallet.WalletTypeId));
        Assert.All(firstResult.Created, wallet => Assert.True(wallet.CustomerWalletId > 0));
        Assert.Collection(
            firstResult.Skipped,
            wallet =>
            {
                Assert.Equal(1, wallet.WalletTypeId);
                Assert.Equal("REWARD", wallet.WalletTypeCode);
            });
        Assert.Empty(secondResult.Created);
        Assert.Equal([3, 1, 2], secondResult.Skipped.Select(wallet => wallet.WalletTypeId));
        Assert.Equal(2, unitOfWork.SaveChangesCallCount);
        Assert.Equal(
            4,
            await dbContext.CustomerWallets.CountAsync(wallet => wallet.CustomerId == 20011));
    }

    [Fact]
    public async Task Handle_WhenCustomerDoesNotExist_ThrowsDomainExceptionWithoutSaving()
    {
        await using var scope = CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ProvisionCustomerWalletsCommandHandler>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<CountingUnitOfWork>();

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ProvisionCustomerWalletsCommand(99999), CancellationToken.None));

        Assert.Equal("Customer not found.", exception.Message);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public void Validator_WhenCustomerIdIsNotPositive_ReturnsValidationFailure()
    {
        var validator = new ProvisionCustomerWalletsCommandValidator();

        var result = validator.TestValidate(new ProvisionCustomerWalletsCommand(0));

        result.ShouldHaveValidationErrorFor(command => command.CustomerId);
    }

    [Fact]
    public async Task Handle_WhenNoActiveWalletTypes_ReturnsEmptyResultAndSavesOnce()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Customers.Add(CreateCustomer(20011));
        dbContext.WalletTypes.Add(CreateWalletType(1, "Archived", "ARCHIVED", false));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<ProvisionCustomerWalletsCommandHandler>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<CountingUnitOfWork>();

        var result = await handler.Handle(
            new ProvisionCustomerWalletsCommand(20011),
            CancellationToken.None);

        Assert.Empty(result.Created);
        Assert.Empty(result.Skipped);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
        Assert.Empty(await dbContext.CustomerWallets.ToListAsync());
    }

    private static AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        RegisterInternalRepository<ICustomerRepository>(services, "CustomerRepository");
        RegisterInternalRepository<IWalletTypeRepository>(services, "WalletTypeRepository");
        RegisterInternalRepository<ICustomerWalletRepository>(services, "CustomerWalletRepository");
        services.AddScoped<CountingUnitOfWork>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CountingUnitOfWork>());
        services.AddScoped<ProvisionCustomerWalletsCommandHandler>();

        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private static void RegisterInternalRepository<TService>(IServiceCollection services, string typeName)
        where TService : class
    {
        var repositoryType = typeof(ApplicationDbContext).Assembly.GetType(
            $"Mart.Customer.Persistence.Repositories.{typeName}",
            throwOnError: true)!;
        services.AddScoped(typeof(TService), repositoryType);
    }

    private static CustomerEntity CreateCustomer(long customerId)
    {
        var customer = CustomerEntity.Create(
            $"CUS-{customerId}",
            null,
            null,
            $"Customer {customerId}",
            customerId.ToString(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            false,
            true,
            false,
            null,
            DateTime.UtcNow,
            null);
        SetProperty(customer, nameof(CustomerEntity.CustomerId), customerId);
        return customer;
    }

    private static WalletType CreateWalletType(int id, string name, string code, bool isActive)
    {
        var walletType = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
        SetProperty(walletType, nameof(WalletType.Id), id);
        SetProperty(walletType, nameof(WalletType.Name), name);
        SetProperty(walletType, nameof(WalletType.Code), code);
        SetProperty(walletType, nameof(WalletType.IsActive), isActive);
        SetProperty(walletType, nameof(WalletType.CreatedDate), DateTime.UtcNow);
        return walletType;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
        where TTarget : class
    {
        typeof(TTarget).GetProperty(propertyName)!.SetValue(target, value);
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _dbContext;

        public CountingUnitOfWork(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public int SaveChangesCallCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
