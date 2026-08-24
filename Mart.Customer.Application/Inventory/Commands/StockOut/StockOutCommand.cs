namespace Mart.Customer.Application.Inventory.Commands.StockOut;

public sealed record StockOutCommand(
    long UserId,
    long ProductId,
    decimal Quantity,
    string MovementType,
    string? BatchNumber,
    string Reason,
    string? Remarks);
