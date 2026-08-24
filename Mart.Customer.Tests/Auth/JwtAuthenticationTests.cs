using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Mart.Customer.Api.Auth;
using Mart.Customer.Application.Abstractions.Auth;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Auth.Commands.GenerateInternalUserToken;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Infrastructure.Auth;
using Mart.Customer.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Mart.Customer.Tests.Auth;

public sealed class JwtAuthenticationTests
{
    [Fact]
    public void JwtTokenService_UsesCanonicalMartClaims()
    {
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "tests",
            Audience = "tests",
            SigningKey = "a-test-signing-key-that-is-at-least-32-characters"
        }));

        var result = service.GenerateToken(new TokenSubject(
            "41",
            "Mart User",
            "CASHIER",
            "internalUser",
            new Dictionary<string, string>
            {
                [MartTokenClaims.FranchiseId] = "7",
                [MartTokenClaims.StoreId] = "11"
            }));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        Assert.Equal("41", token.Claims.Single(claim => claim.Type == MartTokenClaims.UserId).Value);
        Assert.Equal("7", token.Claims.Single(claim => claim.Type == MartTokenClaims.FranchiseId).Value);
        Assert.Equal("11", token.Claims.Single(claim => claim.Type == MartTokenClaims.StoreId).Value);
        Assert.Equal("CASHIER", token.Claims.Single(claim => claim.Type == MartTokenClaims.Role).Value);
        Assert.Equal("Mart User", token.Claims.Single(claim => claim.Type == MartTokenClaims.UserName).Value);
    }

    [Fact]
    public async Task InternalLogin_AddsAssignedFranchiseAndStoreToTokenSubject()
    {
        var repository = new StubInternalUserRepository(new InternalUserAccountDto(
            "41", "cashier", "Mart User", "CASHIER", "hash", "salt", 7, 11));
        var tokenService = new CapturingTokenService();
        var handler = new GenerateInternalUserTokenCommandHandler(
            repository,
            new AcceptingPasswordVerifier(),
            tokenService);

        await handler.Handle(new GenerateInternalUserTokenCommand("cashier", "password"), CancellationToken.None);

        Assert.NotNull(tokenService.Subject);
        Assert.Equal("7", tokenService.Subject.Claims[MartTokenClaims.FranchiseId]);
        Assert.Equal("11", tokenService.Subject.Claims[MartTokenClaims.StoreId]);
        Assert.Equal("cashier", tokenService.Subject.Claims[MartTokenClaims.UserName]);
    }

    [Fact]
    public void MartUserContext_ReadsCanonicalClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(MartTokenClaims.UserId, "41"),
            new Claim(MartTokenClaims.FranchiseId, "7"),
            new Claim(MartTokenClaims.StoreId, "11"),
            new Claim(MartTokenClaims.UserName, "cashier"),
            new Claim(MartTokenClaims.Role, "CASHIER")
        ], "test"));
        var context = new DefaultHttpContext { User = principal };
        var userContext = new MartUserContext(new HttpContextAccessor { HttpContext = context });

        Assert.Equal(41L, userContext.UserId);
        Assert.Equal(7L, userContext.FranchiseId);
        Assert.Equal(11L, userContext.StoreId);
        Assert.Equal("cashier", userContext.UserName);
        Assert.Equal("CASHIER", userContext.Role);
    }

    private sealed class StubInternalUserRepository(InternalUserAccountDto account) : IInternalUserRepository
    {
        public Task<InternalUserAccountDto?> GetByLoginIdAsync(
            string loginId,
            CancellationToken cancellationToken = default) => Task.FromResult<InternalUserAccountDto?>(account);

        public Task<MartUserAccessScopeDto?> GetAccessScopeAsync(
            long userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MartUserAccessScopeDto?>(new MartUserAccessScopeDto(7, 11));
    }

    private sealed class AcceptingPasswordVerifier : IPasswordVerifier
    {
        public bool Verify(string password, string storedHash, string storedSalt) => true;
    }

    private sealed class CapturingTokenService : IJwtTokenService
    {
        public TokenSubject? Subject { get; private set; }

        public AuthTokenDto GenerateToken(TokenSubject subject)
        {
            Subject = subject;
            return new AuthTokenDto(
                subject.UserId,
                subject.Name,
                subject.Role,
                "24 hours",
                "token",
                "Bearer",
                DateTime.UtcNow.AddHours(24),
                86400);
        }
    }
}
