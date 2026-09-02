namespace Mart.Customer.Application.Orders.Dtos;

public sealed record OrderSearchCriteria(
    long FranchiseId,
    long MartStoreId,
    string? CustomerName,
    string? MobileNumber,
    string? InvoiceNumber,
    int PageNumber,
    int PageSize);
