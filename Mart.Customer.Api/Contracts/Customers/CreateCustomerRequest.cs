namespace Mart.Customer.Api.Contracts.Customers;

public sealed record CreateCustomerRequest(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber);
