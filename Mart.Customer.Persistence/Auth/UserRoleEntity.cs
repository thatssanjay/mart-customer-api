namespace Mart.Customer.Persistence.Auth;

public sealed class UserRoleEntity
{
    public long UserRoleId { get; set; }

    public long UserId { get; set; }

    public long RoleId { get; set; }

    public bool IsActive { get; set; }

    public int? OrganizationId { get; set; }

    public long? AssignedBy { get; set; }

    public DateTime AssignedOn { get; set; }

    public long? RemovedBy { get; set; }

    public DateTime? RemovedOn { get; set; }

    public UserMasterEntity User { get; set; } = null!;

    public RoleMasterEntity Role { get; set; } = null!;
}
