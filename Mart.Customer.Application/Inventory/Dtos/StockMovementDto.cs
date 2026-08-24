namespace Mart.Customer.Application.Inventory.Dtos;

public sealed record StockMovementDto(
    long StockMovementId,
    long ProductId,
    string ProductCode,
    string ProductName,
    string MovementType,
    string? ReferenceType,
    long? ReferenceId,
    decimal Quantity,
    decimal? PreviousQuantity,
    decimal? NewQuantity,
    string? Remarks,
    long? CreatedBy,
    DateTime CreatedOn);
