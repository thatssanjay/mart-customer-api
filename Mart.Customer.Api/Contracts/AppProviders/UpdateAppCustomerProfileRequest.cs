using System.ComponentModel.DataAnnotations;

namespace Mart.Customer.Api.Contracts.AppProviders;

public sealed class UpdateAppCustomerProfileRequest
{
    [Required, MaxLength(200)]
    public string? DisplayName { get; init; }

    [EmailAddress, MaxLength(256)]
    public string? EmailAddress { get; init; }

    [MaxLength(250)]
    public string? Address { get; init; }
}
