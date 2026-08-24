namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record WalletTypeDto(
    int Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive);
