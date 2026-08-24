namespace Mart.Customer.Domain.Wallets;

public sealed class WalletType
{
    private WalletType()
    {
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public int? CreatedBy { get; private set; }

    public DateTime? ModifiedDate { get; private set; }

    public int? ModifiedBy { get; private set; }
}
