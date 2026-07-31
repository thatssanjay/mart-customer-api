namespace Mart.Customer.Persistence.Auth;

public sealed class RoleMasterEntity
{
    public long RoleId { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public ICollection<UserRoleEntity> UserRoles { get; } = new List<UserRoleEntity>();
}
