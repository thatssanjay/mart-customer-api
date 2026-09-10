using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Mart.Customer.Api.Auth;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Application.Auth.Queries.GetMartUserAccessScope;
using Mart.Customer.Application.Wallets.Engine;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mart.Customer.Shared.Auth;
using Xunit;

namespace Mart.Customer.Tests.Wallets;

public sealed class OrderWalletCreditEndpointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RouteAcceptsOnlyOrderIdAndUsesServerAccess(bool sendUntrustedBody)
    {
        await using var factory = new OrderCreditApiFactory();
        using var client = factory.CreateClient();
        using var response = sendUntrustedBody
            ? await client.PostAsJsonAsync("/api/v1/wallet-engine/orders/10001/credit", new
            { customerId = 999, storeId = 999, amount = 99999, walletId = 99, percentage = 100, conversionRate = 99 })
            : await client.PostAsync("/api/v1/wallet-engine/orders/10001/credit", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(10001, factory.Engine.OrderId);
        Assert.Equal(new OrderWalletAccess(41, 7, 5), factory.Engine.Access);
        var result = await response.Content.ReadFromJsonAsync<OrderWalletCreditResult>();
        Assert.Equal(1000m, result!.PaidAmount);
    }

    [Fact]
    public async Task CustomerTokenIsForbiddenAndDoesNotReachCreditEngine()
    {
        await using var factory = new OrderCreditApiFactory("customer", "CUSTOMER");
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/v1/wallet-engine/orders/10001/credit", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(factory.Engine.Access);
    }
}

internal sealed class OrderCreditApiFactory(
    string loginType = "internalUser",
    string role = "MA") : WebApplicationFactory<Program>
{
    public RecordingOrderEngine Engine { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWalletEngineService>();
            services.AddSingleton<IWalletEngineService>(Engine);
            services.RemoveAll<IMartUserContext>();
            services.AddSingleton<IMartUserContext>(new OrderTestUser(loginType));
            services.RemoveAll<IRequestHandler<GetMartUserAccessScopeQuery, MartUserAccessScopeDto>>();
            services.AddSingleton<IRequestHandler<GetMartUserAccessScopeQuery, MartUserAccessScopeDto>, OrderTestAccess>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = OrderCreditAuthenticationHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = OrderCreditAuthenticationHandler.AuthenticationScheme;
                options.DefaultForbidScheme = OrderCreditAuthenticationHandler.AuthenticationScheme;
            }).AddScheme<OrderCreditAuthenticationOptions, OrderCreditAuthenticationHandler>(
                OrderCreditAuthenticationHandler.AuthenticationScheme,
                options =>
                {
                    options.LoginType = loginType;
                    options.Role = role;
                });
        });
    }
}

internal sealed class OrderCreditAuthenticationOptions : AuthenticationSchemeOptions
{
    public string LoginType { get; set; } = "internalUser";
    public string Role { get; set; } = "MA";
}

internal sealed class OrderCreditAuthenticationHandler : AuthenticationHandler<OrderCreditAuthenticationOptions>
{
    public const string AuthenticationScheme = "OrderCreditTest";

    public OrderCreditAuthenticationHandler(
        IOptionsMonitor<OrderCreditAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(MartTokenClaims.UserId, "41"),
            new Claim(MartTokenClaims.LoginType, Options.LoginType),
            new Claim(ClaimTypes.Role, Options.Role)
        ], AuthenticationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

internal sealed class OrderTestAccess : IRequestHandler<GetMartUserAccessScopeQuery, MartUserAccessScopeDto>
{
    public Task<MartUserAccessScopeDto> Handle(GetMartUserAccessScopeQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new MartUserAccessScopeDto(7, 5));
}

internal sealed class OrderTestUser(string loginType) : IMartUserContext
{
    public long UserId => 41;
    public long FranchiseId => 7;
    public long StoreId => 5;
    public string? UserName => "test";
    public string? Role => "Cashier";
    public string? LoginType => loginType;
}

internal sealed class RecordingOrderEngine : IWalletEngineService
{
    public long OrderId { get; private set; }
    public OrderWalletAccess? Access { get; private set; }
    public Task<OrderWalletCreditResult> CreditPaidOrderAsync(long orderId, OrderWalletAccess access, CancellationToken cancellationToken = default)
    {
        OrderId = orderId;
        Access = access;
        return Task.FromResult(new OrderWalletCreditResult(orderId, 101, access.StoreId, 1000m, false, []));
    }
    public Task<WalletEngineResult> PostAsync(WalletPostingRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
