namespace Mart.Customer.Application.Customers.Dtos;

public sealed record PagedResultDto<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
