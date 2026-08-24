namespace Mart.Customer.Application.Auth.Dtos;

public sealed record InternalUserAccountDto(
    string UserId,
    string Username,
    string? DisplayName,
    string Role,
    string PasswordHash,
    string PasswordSalt,
    long? FranchiseId,
    long? StoreId);
