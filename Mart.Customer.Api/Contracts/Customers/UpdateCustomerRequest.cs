using System.ComponentModel.DataAnnotations;

namespace Mart.Customer.Api.Contracts.Customers;

public sealed class UpdateCustomerRequest
{
    [Required]
    public string? DisplayName { get; init; }

    [Required]
    public string? MobileNumber { get; init; }

    public string? Email { get; init; }

    public string? AddressLine1 { get; init; }
}
