namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record UpdatedCustomerWalletStatusDto(
    long CustomerWalletId,
    long CustomerId,
    int WalletTypeId,
    bool IsActive,
    DateTime? ModifiedOn);
