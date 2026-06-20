using Mart.Customer.Domain.Common;

namespace Mart.Customer.Domain.Customers;

public sealed class Customer : Entity
{
    private Customer()
    {
    }

    private Customer(string firstName, string lastName, string email, string? phoneNumber)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        Status = CustomerStatus.Active;
    }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? PhoneNumber { get; private set; }

    public CustomerStatus Status { get; private set; }

    public static Customer Create(string firstName, string lastName, string email, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new DomainException("Customer first name is required.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("Customer last name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Customer email is required.");
        }

        return new Customer(firstName.Trim(), lastName.Trim(), email.Trim().ToLowerInvariant(), phoneNumber?.Trim());
    }

    public void UpdateContact(string email, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Customer email is required.");
        }

        Email = email.Trim().ToLowerInvariant();
        PhoneNumber = phoneNumber?.Trim();
        MarkUpdated();
    }

    public void Deactivate()
    {
        Status = CustomerStatus.Inactive;
        MarkUpdated();
    }
}
