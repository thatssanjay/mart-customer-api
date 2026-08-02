using System.ComponentModel.DataAnnotations;

namespace Mart.Customer.Api.Contracts.AppProviders;

public sealed class CreateAppCustomerRequest
{
    [Required, MaxLength(200)]
    public string? DisplayName { get; init; }

    [Required, MaxLength(30)]
    public string? MobileNumber { get; init; }

    [Required, MaxLength(10)]
    public string? Otp { get; init; }
}
