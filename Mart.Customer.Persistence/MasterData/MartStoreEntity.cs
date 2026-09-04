namespace Mart.Customer.Persistence.MasterData;

public sealed class MartStoreEntity
{
    public long StoreId { get; private set; }
    public string? StoreCode { get; private set; }
    public long FranchiseId { get; private set; }
    public string StoreName { get; private set; } = string.Empty;
    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? Country { get; private set; }
    public string? PinCode { get; private set; }
    public bool IsActive { get; private set; }
}
