namespace Mart.Customer.Domain.Customers;

using Mart.Customer.Domain.Common;

public sealed class Customer
{
    private Customer()
    {
    }

    private Customer(
        string? customerCode,
        string? firstName,
        string? lastName,
        string? displayName,
        string mobileNumber,
        string? email,
        string? gender,
        DateTime? dateOfBirth,
        string? addressLine1,
        string? addressLine2,
        string? city,
        string? state,
        string? country,
        string? pinCode,
        string? preferredLanguage,
        string? registrationSource,
        bool isMobileVerified,
        bool isEmailVerified,
        bool isActive,
        bool isBlocked,
        DateTime? lastLoginOn,
        DateTime? createdOn,
        DateTime? modifiedOn)
    {
        CustomerCode = Normalize(customerCode);
        FirstName = Normalize(firstName);
        LastName = Normalize(lastName);
        DisplayName = Normalize(displayName) ?? BuildDisplayName(firstName, lastName);
        MobileNumber = NormalizeRequired(mobileNumber, "Mobile number is required.");
        Email = Normalize(email)?.ToLowerInvariant();
        Gender = Normalize(gender);
        DateOfBirth = dateOfBirth;
        AddressLine1 = Normalize(addressLine1);
        AddressLine2 = Normalize(addressLine2);
        City = Normalize(city);
        State = Normalize(state);
        Country = Normalize(country);
        PinCode = Normalize(pinCode);
        PreferredLanguage = Normalize(preferredLanguage);
        RegistrationSource = Normalize(registrationSource);
        IsMobileVerified = isMobileVerified;
        IsEmailVerified = isEmailVerified;
        IsActive = isActive;
        IsBlocked = isBlocked;
        LastLoginOn = lastLoginOn;
        CreatedOn = createdOn ?? DateTime.UtcNow;
        ModifiedOn = modifiedOn;
    }

    public int CustomerId { get; private set; }

    public string? CustomerCode { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public string? DisplayName { get; private set; }

    public string MobileNumber { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? Gender { get; private set; }

    public DateTime? DateOfBirth { get; private set; }

    public string? AddressLine1 { get; private set; }

    public string? AddressLine2 { get; private set; }

    public string? City { get; private set; }

    public string? State { get; private set; }

    public string? Country { get; private set; }

    public string? PinCode { get; private set; }

    public string? PreferredLanguage { get; private set; }

    public string? RegistrationSource { get; private set; }

    public bool IsMobileVerified { get; private set; }

    public bool IsEmailVerified { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsBlocked { get; private set; }

    public DateTime? LastLoginOn { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public DateTime? ModifiedOn { get; private set; }

    public static Customer Create(
        string? customerCode,
        string? firstName,
        string? lastName,
        string? displayName,
        string mobileNumber,
        string? email,
        string? gender,
        DateTime? dateOfBirth,
        string? addressLine1,
        string? addressLine2,
        string? city,
        string? state,
        string? country,
        string? pinCode,
        string? preferredLanguage,
        string? registrationSource,
        bool isMobileVerified,
        bool isEmailVerified,
        bool isActive,
        bool isBlocked,
        DateTime? lastLoginOn,
        DateTime? createdOn,
        DateTime? modifiedOn)
    {
        return new Customer(
            customerCode,
            firstName,
            lastName,
            displayName,
            mobileNumber,
            email,
            gender,
            dateOfBirth,
            addressLine1,
            addressLine2,
            city,
            state,
            country,
            pinCode,
            preferredLanguage,
            registrationSource,
            isMobileVerified,
            isEmailVerified,
            isActive,
            isBlocked,
            lastLoginOn,
            createdOn,
            modifiedOn);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeRequired(string value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(errorMessage);
        }

        return value.Trim();
    }

    private static string? BuildDisplayName(string? firstName, string? lastName)
    {
        var displayName = string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
        return string.IsNullOrWhiteSpace(displayName) ? null : displayName;
    }
}
