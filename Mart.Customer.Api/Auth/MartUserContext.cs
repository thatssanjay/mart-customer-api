using System.Globalization;
using System.Security.Claims;
using Mart.Customer.Domain.Common;
using Mart.Customer.Shared.Auth;

namespace Mart.Customer.Api.Auth;

public sealed class MartUserContext : IMartUserContext
{
    private readonly ClaimsPrincipal _user;

    public MartUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _user = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("There is no active HTTP request.");
    }

    public long UserId => GetRequiredLongClaim(
        MartTokenClaims.UserId,
        ClaimTypes.NameIdentifier,
        MartTokenClaims.LegacyUserId,
        MartTokenClaims.LegacyCamelCaseUserId);

    public long FranchiseId => GetRequiredLongClaim(
        MartTokenClaims.FranchiseId,
        MartTokenClaims.LegacyFranchieseId,
        MartTokenClaims.LegacyCamelCaseFranchiseId);

    public long StoreId => GetRequiredLongClaim(MartTokenClaims.StoreId, MartTokenClaims.LegacyCamelCaseStoreId);

    public string? UserName =>
        _user.FindFirstValue(MartTokenClaims.UserName) ??
        _user.FindFirstValue(ClaimTypes.Name) ??
        _user.FindFirstValue(MartTokenClaims.LegacyUserName) ??
        _user.FindFirstValue(MartTokenClaims.LegacyCamelCaseUserName);

    public string? Role =>
        _user.FindFirstValue(MartTokenClaims.Role) ??
        _user.FindFirstValue(ClaimTypes.Role) ??
        _user.FindFirstValue(MartTokenClaims.LegacyUserRole) ??
        _user.FindFirstValue(MartTokenClaims.LegacyCamelCaseRole);

    public string? LoginType => _user.FindFirstValue(MartTokenClaims.LoginType);

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
