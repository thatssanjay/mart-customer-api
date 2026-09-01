namespace Mart.Customer.Application.Payments.Dtos;

public sealed record WalletPaymentRequestDto(
    string PaymentReference,
    string PaymentToken,
    string Status,
    decimal Amount,
    DateTime ExpiresOn);

public sealed record WalletPaymentStatusDto(
    string PaymentReference,
    string Status,
    decimal Amount,
    DateTime ExpiresOn);

public sealed record WalletPaymentCartDto(
    string PaymentReference,
    string Status,
    decimal Amount,
    DateTime ExpiresOn,
    long CustomerCartId,
    string CartNumber,
    long CustomerId,
    long MartStoreId,
    IReadOnlyList<WalletPaymentCartItemDto> Items);

public sealed record WalletPaymentCartItemDto(
    long CustomerCartItemId,
    long ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);
