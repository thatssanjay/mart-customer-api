namespace Mart.Customer.Application.Orders.Dtos;

public sealed record InvoiceDetailsDto(
    long CustomerOrderId,
    string InvoiceNumber,
    DateTime OrderDate,
    string OrderStatus,
    InvoiceCustomerSnapshotDto Customer,
    InvoiceStoreSnapshotDto Store,
    IReadOnlyList<InvoiceDetailsItemDto> Items,
    IReadOnlyList<InvoiceDetailsPaymentDto> Payments,
    InvoiceTaxTotalsDto Totals,
    InvoiceRewardRedemptionDto Rewards);

public sealed record InvoiceCustomerSnapshotDto(
    long CustomerId,
    string? CustomerCode,
    string? CustomerName,
    string? MobileNumber,
    string? Address);

public sealed record InvoiceStoreSnapshotDto(
    long FranchiseId,
    long MartStoreId,
    string StoreName,
    string StoreAddress,
    string? GSTIN,
    string? StateCode);

public sealed record InvoiceDetailsItemDto(
    long CustomerOrderItemId,
    long ProductId,
    string? ProductCode,
    string? HSNCode,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal MRP,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal GSTPercent,
    decimal CGSTAmount,
    decimal SGSTAmount,
    decimal IGSTAmount,
    decimal GSTAmount,
    decimal LineTotal);

public sealed record InvoiceDetailsPaymentDto(
    long CustomerOrderPaymentId,
    string PaymentMode,
    decimal Amount,
    string? TransactionReference,
    DateTime PaidOn);

public sealed record InvoiceTaxTotalsDto(
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal CGSTAmount,
    decimal SGSTAmount,
    decimal IGSTAmount,
    decimal GSTAmount,
    decimal RoundOffAmount,
    decimal NetAmount,
    decimal FinalPayableAmount);

public sealed record InvoiceRewardRedemptionDto(
    int? RedemptionWalletTypeId,
    decimal RedeemPointsUsed,
    decimal RedemptionAmount,
    decimal RewardEarned,
    decimal CashbackEarned);

public enum InvoiceDetailsResultStatus
{
    Found,
    NotFound,
    Forbidden
}

public sealed record InvoiceDetailsResult(
    InvoiceDetailsResultStatus Status,
    InvoiceDetailsDto? Invoice)
{
    public static InvoiceDetailsResult Found(InvoiceDetailsDto invoice) =>
        new(InvoiceDetailsResultStatus.Found, invoice);

    public static InvoiceDetailsResult NotFound() => new(InvoiceDetailsResultStatus.NotFound, null);

    public static InvoiceDetailsResult Forbidden() => new(InvoiceDetailsResultStatus.Forbidden, null);
}
