namespace Mart.Customer.Domain.Inventory;

public sealed class StockMovement
{
    private StockMovement()
    {
    }

    public long StockMovementId { get; private set; }

    public long FranchiseId { get; private set; }

    public long MartStoreId { get; private set; }

    public long ProductId { get; private set; }

    public string MovementType { get; private set; } = string.Empty;

    public string? ReferenceType { get; private set; }

    public long? ReferenceId { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal? PreviousQuantity { get; private set; }

    public decimal? NewQuantity { get; private set; }

    public string? Remarks { get; private set; }

    public long? CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public static StockMovement CreateStockIn(
        long franchiseId,
        long martStoreId,
        long productId,
        string movementType,
        string? referenceType,
        long? referenceId,
        decimal quantity,
        decimal previousQuantity,
        decimal newQuantity,
        string? remarks,
        long createdBy,
        DateTime createdOn)
    {
        return new StockMovement
        {
            FranchiseId = franchiseId,
            MartStoreId = martStoreId,
            ProductId = productId,
            MovementType = movementType,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Quantity = quantity,
            PreviousQuantity = previousQuantity,
            NewQuantity = newQuantity,
            Remarks = remarks,
            CreatedBy = createdBy,
            CreatedOn = createdOn
        };
    }

    public static StockMovement CreateStockOut(
        long franchiseId,
        long martStoreId,
        long productId,
        string movementType,
        decimal quantity,
        decimal previousQuantity,
        decimal newQuantity,
        string? remarks,
        long createdBy,
        DateTime createdOn)
    {
        return new StockMovement
        {
            FranchiseId = franchiseId,
            MartStoreId = martStoreId,
            ProductId = productId,
            MovementType = movementType,
            Quantity = quantity,
            PreviousQuantity = previousQuantity,
            NewQuantity = newQuantity,
            Remarks = remarks,
            CreatedBy = createdBy,
            CreatedOn = createdOn
        };
    }

    public static StockMovement CreateSale(
        long franchiseId,
        long martStoreId,
        long productId,
        long customerOrderId,
        decimal quantity,
        decimal previousQuantity,
        decimal newQuantity,
        long createdBy,
        DateTime createdOn)
    {
        return new StockMovement
        {
            FranchiseId = franchiseId,
            MartStoreId = martStoreId,
            ProductId = productId,
            MovementType = "SALE",
            ReferenceType = "ORDER",
            ReferenceId = customerOrderId,
            Quantity = quantity,
            PreviousQuantity = previousQuantity,
            NewQuantity = newQuantity,
            Remarks = $"Sale order {customerOrderId}",
            CreatedBy = createdBy,
            CreatedOn = createdOn
        };
    }
}
