using Mart.Customer.Shared.Auth;

namespace Mart.Customer.Api.Auth;

internal static class WalletStoreContext
{
    public static long? GetStoreId(HttpContext context)
    {
        if (!context.User.HasClaim(claim => claim.Type == MartTokenClaims.StoreId ||
                                          claim.Type == MartTokenClaims.LegacyCamelCaseStoreId))
            return null;

        return context.RequestServices.GetRequiredService<IMartUserContext>().StoreId;
    }
}
