namespace Mart.Customer.Application.Customers.Dtos;

public sealed record AppCustomerProfileDto(
    long UserId,
    string MobileNumber,
    string? DisplayName,
    string? EmailAddress,
    string? Address);
