using System.Text.Json;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Subscriptions.Dtos;
using Mart.Customer.Domain.Subscriptions;
using Mart.Customer.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Subscriptions;

public sealed class SubscriptionPlanRepositoryTests
{
    private static readonly DateTime CurrentDate = new(2026, 9, 4);

    [Fact]
    public async Task GetActivePlansAsync_FiltersInactiveAndInvalidDates_IncludesBothBoundaries()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedAsync(dbContext,
            CreatePlan(6, true, CurrentDate, CurrentDate),
            CreatePlan(5, true, CurrentDate.AddDays(-1), CurrentDate),
            CreatePlan(4, true, CurrentDate, CurrentDate.AddDays(1)),
            CreatePlan(3, true, CurrentDate.AddDays(1), CurrentDate.AddDays(2)),
            CreatePlan(2, true, CurrentDate.AddDays(-2), CurrentDate.AddDays(-1)),
            CreatePlan(1, false, CurrentDate.AddDays(-1), CurrentDate.AddDays(1)));

        var repository = scope.ServiceProvider.GetRequiredService<ICustomerSubscriptionRepository>();
        var result = await repository.GetActivePlansAsync(CurrentDate.AddHours(12));

        Assert.Equal([4, 5, 6], result.Select(plan => plan.SubscriptionId));
        Assert.Equal(new SubscriptionPlanDto(4, "Test Plan", 499m, 1, "YEAR", 20m, 30m, 1,
            new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 5)), result[0]);
        Assert.Empty(dbContext.ChangeTracker.Entries());

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("2026-09-04", json.RootElement[0].GetProperty("effectiveFrom").GetString());
        Assert.Equal("2026-09-05", json.RootElement[0].GetProperty("effectiveTo").GetString());
        Assert.Equal(10, json.RootElement[0].EnumerateObject().Count());
    }

    [Fact]
    public async Task GetActivePlansAsync_WhenNoPlansMatch_ReturnsEmptyList()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedAsync(dbContext, CreatePlan(1, false, CurrentDate, CurrentDate));
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerSubscriptionRepository>();

        Assert.Empty(await repository.GetActivePlansAsync(CurrentDate));
    }

    [Fact]
    public async Task GetActivePlansAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        await using var scope = CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerSubscriptionRepository>();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetActivePlansAsync(CurrentDate, cancellation.Token));
    }

    private static AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var repositoryType = typeof(ApplicationDbContext).Assembly.GetType(
            "Mart.Customer.Persistence.Repositories.CustomerSubscriptionRepository", throwOnError: true)!;
        services.AddScoped(typeof(ICustomerSubscriptionRepository), repositoryType);
        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private static async Task SeedAsync(ApplicationDbContext dbContext, params SubscriptionPlan[] plans)
    {
        dbContext.SubscriptionPlans.AddRange(plans);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    private static SubscriptionPlan CreatePlan(int id, bool isActive, DateTime from, DateTime to)
    {
        var plan = (SubscriptionPlan)Activator.CreateInstance(typeof(SubscriptionPlan), nonPublic: true)!;
        SetProperty(plan, nameof(SubscriptionPlan.SubscriptionId), id);
        SetProperty(plan, nameof(SubscriptionPlan.PlanName), "Test Plan");
        SetProperty(plan, nameof(SubscriptionPlan.SubscriptionFee), 499m);
        SetProperty(plan, nameof(SubscriptionPlan.DurationValue), 1);
        SetProperty(plan, nameof(SubscriptionPlan.DurationType), "YEAR");
        SetProperty(plan, nameof(SubscriptionPlan.ExtraPointPercentage), 20m);
        SetProperty(plan, nameof(SubscriptionPlan.FeeToWalletPercentage), 30m);
        SetProperty(plan, nameof(SubscriptionPlan.WalletTypeId), 1);
        SetProperty(plan, nameof(SubscriptionPlan.IsActive), isActive);
        SetProperty(plan, nameof(SubscriptionPlan.EffectiveFrom), from);
        SetProperty(plan, nameof(SubscriptionPlan.EffectiveTo), to);
        return plan;
    }

    private static void SetProperty<T>(SubscriptionPlan plan, string name, T value)
    {
        typeof(SubscriptionPlan).GetProperty(name)!.SetValue(plan, value);
    }
}
