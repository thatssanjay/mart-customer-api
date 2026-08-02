namespace Mart.Customer.Application.Customers.Dtos;

public sealed record CustomerListItemDto(
    long CustomerId,
    string? CustomerCode,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string MobileNumber,
    string? EmailId,
    string? Gender,
    string? Address1,
    string? City,
    bool IsActive);
