namespace Mart.Customer.Persistence.Auth;

public sealed class UserMasterEntity
{
    public long UserId { get; set; }

    public string UserCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? MobileNumber { get; set; }

    public string Username { get; set; } = string.Empty;

    public string? PasswordHash { get; set; }

    public string? PasswordSalt { get; set; }

    public string? ProfileImageUrl { get; set; }

    public bool IsActive { get; set; }

    public bool IsFranchiseAdmin { get; set; }

    public bool IsMartAdmin { get; set; }

    public bool IsEmailVerified { get; set; }

    public bool IsMobileVerified { get; set; }

    public bool PasswordMustChangeOnNextLogin { get; set; }

    public string? PasswordResetTokenHash { get; set; }

    public DateTime? PasswordResetTokenExpiresOn { get; set; }

    public DateTime? PasswordResetTokenUsedOn { get; set; }

    public DateTime? LastPasswordChangeOn { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? CreatedOn { get; set; }

    public long? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public ICollection<UserRoleEntity> UserRoles { get; } = new List<UserRoleEntity>();
}
