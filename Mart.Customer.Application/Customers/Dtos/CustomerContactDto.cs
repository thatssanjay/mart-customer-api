namespace Mart.Customer.Application.Customers.Dtos;

public sealed record CustomerContactDto(
    long CustomerId,
    string MobileNumber,
    string? Email,
    string? FirstName,
    string? LastName);
