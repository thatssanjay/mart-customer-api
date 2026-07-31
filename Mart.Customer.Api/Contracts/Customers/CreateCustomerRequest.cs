using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Mart.Customer.Api.Contracts.Customers;

public sealed class CreateCustomerRequest
{
    [Required]
    public string? DisplayName { get; init; }

    [Required]
    public string? MobileNumber { get; init; }

    [DefaultValue(nameof(Email))]
    public string? Email { get; init; } = nameof(Email);

    [DefaultValue(nameof(AddressLine1))]
    public string? AddressLine1 { get; init; } = nameof(AddressLine1);
}
