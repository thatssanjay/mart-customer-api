namespace Mart.Customer.Application.Carts.Dtos;

public sealed record ChangedCartStatusDto(
    string CartNumber,
    string CartStatus,
    DateTime? CustomerApprovedOn,
    DateTime? PaidOn,
    DateTime? CancelledOn,
    DateTime? ModifiedOn);
