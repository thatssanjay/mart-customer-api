namespace Mart.Customer.Application.Auth.Dtos;

public sealed record TokenSubject(
    string UserId,
    string? Name,
    string Role,
    string LoginType,
    IReadOnlyDictionary<string, string> Claims);
