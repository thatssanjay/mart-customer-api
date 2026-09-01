namespace Mart.Customer.Application.Orders.Dtos;

public sealed record OrderSearchCriteria(
    long FranchiseId,
    long MartStoreId,
    string? CustomerName,
    string? MobileNumber,
    string? InvoiceNumber,
    DateTime? FromDate,
    DateTime? ToDate,
    int PageNumber,
    int PageSize);
