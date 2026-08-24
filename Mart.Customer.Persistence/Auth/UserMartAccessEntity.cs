namespace Mart.Customer.Persistence.Auth;

public sealed class UserMartAccessEntity
{
    public long UserMartAccessId { get; set; }

    public long UserId { get; set; }

    public long? FranchiseId { get; set; }

    public long? MartStoreId { get; set; }

    public bool CanAccess { get; set; }
}
