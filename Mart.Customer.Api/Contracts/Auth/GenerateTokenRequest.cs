using System.ComponentModel;

namespace Mart.Customer.Api.Contracts.Auth;

public sealed class GenerateTokenRequest
{
    [DefaultValue("customer")]
    public string LoginType { get; init; } = string.Empty;

    public string? MobileNumber { get; init; }

    public string? Otp { get; init; }

    public string? UserId { get; init; }

    public string? Password { get; init; }
}
