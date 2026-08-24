namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record ProvisionCustomerWalletsResultDto(
    long CustomerId,
    IReadOnlyList<CustomerWalletProvisioningItemDto> Created,
    IReadOnlyList<CustomerWalletProvisioningItemDto> Skipped);
