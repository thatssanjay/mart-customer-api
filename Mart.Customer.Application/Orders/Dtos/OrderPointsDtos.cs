namespace Mart.Customer.Application.Orders.Dtos;

public sealed record PendingPointsOrderDto(
    long CustomerOrderId,
    string InvoiceNumber,
    DateTime OrderDate,
    int TotalItemCount,
    decimal FinalPayableAmount,
    string OrderStatus);

public sealed record OrderPointsAwardStateDto(
    long OrderId,
    long FranchiseId,
    long StoreId,
    bool IsPointsAwarded,
    DateTime? PointsAwardedDate,
    string? PointsAwardedBy);

public sealed record OrderPointsAwardedDto(
    long OrderId,
    bool IsPointsAwarded,
    DateTime? PointsAwardedDate,
    string? PointsAwardedBy);

public enum MarkOrderPointsAwardedStatus
{
    Updated,
    NotFound,
    Forbidden,
    AlreadyAwarded
}

public sealed record MarkOrderPointsAwardedResult(
    MarkOrderPointsAwardedStatus Status,
    OrderPointsAwardedDto? Order = null);
