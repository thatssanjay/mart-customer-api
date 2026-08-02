using System.Globalization;
using System.Security.Claims;
using Mart.Customer.Domain.Common;

namespace Mart.Customer.Api.Auth;

public sealed class MartUserContext : IMartUserContext
{
    private readonly ClaimsPrincipal _user;

    public MartUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _user = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("There is no active HTTP request.");
    }

    public long UserId => GetRequiredLongClaim(ClaimTypes.NameIdentifier, MartTokenClaims.UserId, "userId");

    public long FranchiseId => GetRequiredLongClaim(
        MartTokenClaims.FranchiseId,
        MartTokenClaims.LegacyFranchieseId,
        "franchiseId");

    public long StoreId => GetRequiredLongClaim(MartTokenClaims.StoreId, "storeId");

    public string? UserName => _user.FindFirstValue(ClaimTypes.Name) ?? _user.FindFirstValue("User name");

    public string? Role => _user.FindFirstValue(ClaimTypes.Role) ?? _user.FindFirstValue(MartTokenClaims.UserRole);

    private long GetRequiredLongClaim(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = _user.FindFirstValue(claimType);
            if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) && result > 0)
            {
                return result;
            }
        }

        throw new DomainException($"The authenticated token does not contain a valid {claimTypes[0]} claim.");
    }
}
