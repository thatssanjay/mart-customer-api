namespace Mart.Customer.Application.Customers.Dtos;

public sealed record CustomerLookupDto(
    string? DisplayName,
    string MobileNumber);
