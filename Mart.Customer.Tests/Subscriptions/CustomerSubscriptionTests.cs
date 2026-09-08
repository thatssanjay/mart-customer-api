using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Contracts.Subscriptions;
using Mart.Customer.Api.Controllers.V1;
using Mart.Customer.Application;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Subscriptions.Commands.CreateCustomerSubscription;
using Mart.Customer.Application.Subscriptions.Dtos;
using Mart.Customer.Application.Subscriptions.Queries.GetCustomerSubscriptions;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Subscriptions;
using Mart.Customer.Persistence;
using Mart.Customer.Shared.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using CustomerEntity = Mart.Customer.Domain.Customers.Customer;

namespace Mart.Customer.Tests.Subscriptions;

public sealed class CustomerSubscriptionTests
{
    [Fact]
    public async Task Subscribe_UsesTokenIdentityAndPlanValues_AndGetReturnsOnlyOwnSubscriptions()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = fixture.Controller(7);
        var request = JsonSerializer.Deserialize<SubscribeRequest>(
            """{"subscriptionPlanId":1,"customerId":99,"createdBy":99,"walletCreditAmount":9999}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var result = Assert.IsType<CreatedResult>(await controller.Subscribe(request, default));
        var id = Assert.IsType<CreatedCustomerSubscriptionDto>(result.Value).Id;
        var stored = await fixture.Db.CustomerSubscriptions.SingleAsync();
        Assert.Equal(id, stored.Id);
        Assert.Equal(7, stored.CustomerId);
        Assert.Equal(7, stored.CreatedBy);
        Assert.Equal(499m, stored.SubscriptionAmount);
        Assert.Equal(stored.CreatedOn.Date, stored.StartDate);
        Assert.Equal(stored.StartDate.AddMonths(1), stored.ExpiryDate);
        Assert.Equal(20m, stored.ExtraPointPercentage);
        Assert.Equal(30m, stored.FeeToWalletPercentage);
        Assert.Equal(1, stored.WalletTypeId);
        Assert.Equal("ACTIVE", stored.Status);
        Assert.Null(stored.WalletCreditAmount);
        Assert.Null(stored.PaymentTransactionId);

        await fixture.Controller(8).Subscribe(new SubscribeRequest(1), default);
        fixture.Db.ChangeTracker.Clear();
        var response = Assert.IsType<OkObjectResult>(await controller.GetMySubscriptions(default));
        var item = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<CustomerSubscriptionDto>>(response.Value));
        Assert.Equal(id, item.Id);
        Assert.Equal("Monthly plan", item.PlanName);
        Assert.Equal(1, item.SubscriptionPlan.DurationValue);
        Assert.Equal("MONTH", item.SubscriptionPlan.DurationType);
        Assert.Empty(fixture.Db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Subscribe_RejectsActiveDuplicate_EvenWhenItsDatesHaveExpired()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Controller(7).Subscribe(new SubscribeRequest(1), default);
        var stored = await fixture.Db.CustomerSubscriptions.SingleAsync();
        Set(stored, nameof(CustomerSubscription.ExpiryDate), DateTime.UtcNow.AddDays(-1));
        await fixture.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Controller(7).Subscribe(new SubscribeRequest(1), default));
        Assert.Equal("Customer already has an active subscription for this plan.", error.Message);
        Assert.Equal(1, await fixture.Db.CustomerSubscriptions.CountAsync());
    }

    [Fact]
    public async Task Subscribe_AllowsInactiveHistoryAndDifferentPlan()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = fixture.Controller(7);
        await controller.Subscribe(new SubscribeRequest(1), default);
        var stored = await fixture.Db.CustomerSubscriptions.SingleAsync();
        Set(stored, nameof(CustomerSubscription.Status), "EXPIRED");
        await fixture.Db.SaveChangesAsync();
        await controller.Subscribe(new SubscribeRequest(1), default);
        await controller.Subscribe(new SubscribeRequest(2), default);
        var response = Assert.IsType<OkObjectResult>(await controller.GetMySubscriptions(default));
        Assert.Equal(3, Assert.IsAssignableFrom<IReadOnlyList<CustomerSubscriptionDto>>(response.Value).Count);
    }

    [Theory]
    [InlineData("missing customer")]
    [InlineData("inactive customer")]
    [InlineData("blocked customer")]
    [InlineData("missing plan")]
    [InlineData("inactive plan")]
    [InlineData("future plan")]
    [InlineData("expired plan")]
    [InlineData("invalid duration")]
    public async Task Subscribe_RejectsInvalidCustomerOrPlan(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync();
        var customer = await fixture.Db.Customers.SingleAsync(x => x.CustomerId == 7);
        var plan = await fixture.Db.SubscriptionPlans.SingleAsync(x => x.SubscriptionId == 1);
        switch (scenario)
        {
            case "inactive customer": Set(customer, nameof(CustomerEntity.IsActive), false); break;
            case "blocked customer": Set(customer, nameof(CustomerEntity.IsBlocked), true); break;
            case "inactive plan": Set(plan, nameof(SubscriptionPlan.IsActive), false); break;
            case "future plan": Set(plan, nameof(SubscriptionPlan.EffectiveFrom), DateTime.UtcNow.AddDays(1)); break;
            case "expired plan": Set(plan, nameof(SubscriptionPlan.EffectiveTo), DateTime.UtcNow.AddDays(-1)); break;
            case "invalid duration": Set(plan, nameof(SubscriptionPlan.DurationValue), 0); break;
        }
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Controller(scenario == "missing customer" ? 99 : 7)
            .Subscribe(new SubscribeRequest(scenario == "missing plan" ? 99 : 1), default));
        Assert.Empty(await fixture.Db.CustomerSubscriptions.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Subscribe_RejectsInvalidPlanId(int id)
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Controller(7).Subscribe(new SubscribeRequest(id), default));
        Assert.Empty(await fixture.Db.CustomerSubscriptions.ToListAsync());
    }

    [Fact]
    public async Task GetSubscriptions_ReturnsEmptyForRegisteredCustomer_RejectsUnknownCustomer()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.Empty(await fixture.Sender.Send(new GetCustomerSubscriptionsQuery(7)));
        await Assert.ThrowsAsync<DomainException>(() => fixture.Sender.Send(new GetCustomerSubscriptionsQuery(99)));
    }

    [Fact]
    public async Task Subscribe_RollsBackWhenFailureOccursAfterDatabaseSave()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Failure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Controller(7).Subscribe(new SubscribeRequest(1), default));
        Assert.Empty(await fixture.Db.CustomerSubscriptions.AsNoTracking().ToListAsync());
        fixture.Failure.Enabled = false;
        await fixture.Controller(7).Subscribe(new SubscribeRequest(1), default);
        Assert.Equal(1, await fixture.Db.CustomerSubscriptions.CountAsync());
    }

    [Fact]
    public async Task Operations_PropagateCancellation()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Controller(7).Subscribe(new SubscribeRequest(1), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Controller(7).GetMySubscriptions(cancellation.Token));
    }

    [Theory]
    [InlineData(nameof(SubscriptionController.Subscribe))]
    [InlineData(nameof(SubscriptionController.GetMySubscriptions))]
    public void NewEndpoints_RequireCustomerPolicy(string method)
    {
        var policy = Assert.Single(typeof(SubscriptionController).GetMethod(method)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>());
        Assert.Equal(MartAuthorizationPolicies.MobileCustomer, policy.Policy);
    }

    private static void Set(object entity, string name, object value) =>
        entity.GetType().GetProperty(name)!.SetValue(entity, value);

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => Enabled
            ? throw new InvalidOperationException("Simulated failure after save.")
            : ValueTask.FromResult(result);
    }

    private sealed class Fixture(ServiceProvider provider, SqliteConnection connection, FailAfterSave failure) : IAsyncDisposable
    {
        private readonly AsyncServiceScope _scope = provider.CreateAsyncScope();
        public ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        public ISender Sender => _scope.ServiceProvider.GetRequiredService<ISender>();
        public FailAfterSave Failure => failure;

        public SubscriptionController Controller(long customerId) => new(Sender,
            new MartUserContext(new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(MartTokenClaims.UserId, customerId.ToString()),
                        new Claim(MartTokenClaims.LoginType, "customer")], "Test"))
                }
            }));

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.CreateFunction("sysutcdatetime", () => DateTime.UtcNow);
            await connection.OpenAsync();
            var failure = new FailAfterSave();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection).AddInterceptors(failure));
            foreach (var (contract, name) in new[]
            {
                (typeof(ICustomerRepository), "CustomerRepository"),
                (typeof(ICustomerSubscriptionRepository), "CustomerSubscriptionRepository"),
                (typeof(IUnitOfWork), "UnitOfWork")
            })
                services.AddScoped(contract, typeof(ApplicationDbContext).Assembly.GetType(
                    $"Mart.Customer.Persistence.Repositories.{name}", true)!);
            var fixture = new Fixture(services.BuildServiceProvider(), connection, failure);
            await fixture.Db.Database.EnsureCreatedAsync();
            foreach (var id in new long[] { 7, 8 })
            {
                var customer = (CustomerEntity)Activator.CreateInstance(typeof(CustomerEntity), true)!;
                Set(customer, nameof(CustomerEntity.CustomerId), id);
                Set(customer, nameof(CustomerEntity.MobileNumber), $"900000000{id}");
                Set(customer, nameof(CustomerEntity.IsActive), true);
                fixture.Db.Customers.Add(customer);
            }
            foreach (var id in new[] { 1, 2 })
            {
                var plan = (SubscriptionPlan)Activator.CreateInstance(typeof(SubscriptionPlan), true)!;
                Set(plan, nameof(SubscriptionPlan.SubscriptionId), id);
                Set(plan, nameof(SubscriptionPlan.PlanName), "Monthly plan");
                Set(plan, nameof(SubscriptionPlan.SubscriptionFee), 499m);
                Set(plan, nameof(SubscriptionPlan.DurationValue), 1);
                Set(plan, nameof(SubscriptionPlan.DurationType), "MONTH");
                Set(plan, nameof(SubscriptionPlan.ExtraPointPercentage), 20m);
                Set(plan, nameof(SubscriptionPlan.FeeToWalletPercentage), 30m);
                Set(plan, nameof(SubscriptionPlan.WalletTypeId), 1);
                Set(plan, nameof(SubscriptionPlan.IsActive), true);
                Set(plan, nameof(SubscriptionPlan.EffectiveFrom), DateTime.UtcNow.AddDays(-1));
                Set(plan, nameof(SubscriptionPlan.EffectiveTo), DateTime.UtcNow.AddDays(1));
                fixture.Db.SubscriptionPlans.Add(plan);
            }
            await fixture.Db.SaveChangesAsync();
            fixture.Db.ChangeTracker.Clear();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await provider.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
