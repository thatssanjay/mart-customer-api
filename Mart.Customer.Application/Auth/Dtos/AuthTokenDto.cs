namespace Mart.Customer.Application.Auth.Dtos;

public sealed record AuthTokenDto(
    string UserId,
    string? Name,
    string Role,
    string Exp,
    string AccessToken,
    string TokenType,
    DateTime ExpiresOn,
    int ExpiresInSeconds);
