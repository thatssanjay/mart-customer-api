namespace Mart.Customer.Application.Customers.Dtos;

public sealed record CustomerContactDto(
    string MobileNumber,
    string? Email,
    string? FirstName,
    string? LastName);
