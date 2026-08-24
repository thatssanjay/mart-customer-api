namespace Mart.Customer.Api.Auth;

public interface IMartUserContext
{
    long UserId { get; }

    long FranchiseId { get; }

    long StoreId { get; }

    string? UserName { get; }

    string? Role { get; }

    string? LoginType { get; }
}
