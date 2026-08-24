namespace Mart.Customer.Application.Inventory.Commands.StockIn;

public sealed record StockInCommand(
    long UserId,
    long ProductId,
    decimal Quantity,
    string MovementType,
    string? ReferenceType,
    long? ReferenceId,
    string? BatchNumber,
    DateTime? ManufacturingDate,
    DateTime? ExpiryDate,
    decimal? PurchasePrice,
    decimal? SellingPrice,
    decimal? Mrp,
    string? Remarks,
    string? AdjustmentReason);
