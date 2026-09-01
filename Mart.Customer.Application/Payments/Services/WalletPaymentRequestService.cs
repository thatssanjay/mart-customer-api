using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Orders.Services;
using Mart.Customer.Application.Payments.Dtos;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Domain.Common;

namespace Mart.Customer.Application.Payments.Services;

public sealed class WalletPaymentRequestService : IWalletPaymentRequestService
{
    private static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(2);
    private readonly ICustomerCartRepository _cartRepository;
    private readonly IOrderCheckoutCalculator _checkoutCalculator;
    private readonly IUnitOfWork _unitOfWork;

    public WalletPaymentRequestService(
        ICustomerCartRepository cartRepository,
        IOrderCheckoutCalculator checkoutCalculator,
        IUnitOfWork unitOfWork)
    {
        _cartRepository = cartRepository;
        _checkoutCalculator = checkoutCalculator;
        _unitOfWork = unitOfWork;
    }

    public async Task<WalletPaymentRequestDto?> CreateAsync(
        string cartNumber,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepository.GetByCartNumberAsync(
            cartNumber.Trim(),
            franchiseId,
            martStoreId,
            cancellationToken);
        if (cart is null)
        {
            return null;
        }

        var preview = await _checkoutCalculator.CalculateAsync(cart, null, null, cancellationToken);
        var secret = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var paymentToken = string.Create(
            CultureInfo.InvariantCulture,
            $"{cart.CustomerCartId}.{secret}");
        var reference = $"WLT-{Guid.NewGuid():N}".ToUpperInvariant();
        var expiresOn = DateTime.UtcNow.Add(PaymentWindow);
        var attempt = cart.BeginWalletPaymentAttempt(
            reference,
            HashToken(paymentToken),
            preview.FinalPayableAmount,
            expiresOn);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToRequestDto(attempt, paymentToken);
    }

    public async Task<WalletPaymentStatusDto?> GetStatusAsync(
        string paymentToken,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default)
    {
        var cartId = ParseCartId(paymentToken);
        var cart = await _cartRepository.GetByIdAsync(
            cartId,
            franchiseId,
            martStoreId,
            cancellationToken);
        if (cart is null)
        {
            return null;
        }

        var attempt = ValidateToken(cart, paymentToken);
        attempt = await ExpireIfNeededAsync(cart, attempt, cancellationToken);
        return ToStatusDto(attempt);
    }

    public async Task<WalletPaymentCartDto?> GetCartForCustomerAsync(
        string paymentToken,
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var cartId = ParseCartId(paymentToken);
        var cart = await _cartRepository.GetByIdForWalletAsync(cartId, cancellationToken);
        if (cart is null)
        {
            return null;
        }

        if (cart.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("This wallet payment does not belong to the authenticated customer.");
        }

        var attempt = ValidateToken(cart, paymentToken);
        attempt = await ExpireIfNeededAsync(cart, attempt, cancellationToken);

        return new WalletPaymentCartDto(
            attempt.Reference,
            NormalizeStatus(attempt.Status),
            attempt.Amount,
            attempt.ExpiresOn,
            cart.CustomerCartId,
            cart.CartNumber,
            cart.CustomerId,
            cart.MartStoreId,
            cart.Items.Select(item => new WalletPaymentCartItemDto(
                item.CustomerCartItemId,
                item.ProductId,
                item.ProductNameSnapshot,
                item.Quantity,
                item.UnitPrice,
                item.LineTotal)).ToList());
    }

    public Task<string> ValidateForCheckoutAsync(
        CustomerCart cart,
        string? paymentToken,
        decimal finalPayableAmount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(paymentToken))
        {
            throw new DomainException("A wallet payment token is required.");
        }

        if (ParseCartId(paymentToken) != cart.CustomerCartId)
        {
            throw new DomainException("The wallet payment does not match this cart.");
        }

        var attempt = ValidateToken(cart, paymentToken);
        if (attempt.ExpiresOn <= DateTime.UtcNow)
        {
            throw new DomainException("The wallet payment request has expired.");
        }

        if (!string.Equals(attempt.Status, "PAID", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("The wallet payment has not been paid.");
        }

        if (attempt.Amount != finalPayableAmount)
        {
            throw new DomainException("The wallet payment amount no longer matches the cart payable amount.");
        }

        return Task.FromResult(attempt.Reference);
    }

    private async Task<WalletPaymentAttempt> ExpireIfNeededAsync(
        CustomerCart cart,
        WalletPaymentAttempt attempt,
        CancellationToken cancellationToken)
    {
        if (attempt.ExpiresOn > DateTime.UtcNow ||
            string.Equals(attempt.Status, "EXPIRED", StringComparison.OrdinalIgnoreCase))
        {
            return attempt;
        }

        attempt = cart.ExpireWalletPaymentAttempt(attempt);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return attempt;
    }

    private static WalletPaymentAttempt ValidateToken(CustomerCart cart, string paymentToken)
    {
        var attempt = cart.GetWalletPaymentAttempt()
            ?? throw new DomainException("The wallet payment request is not available.");
        var suppliedHash = Convert.FromHexString(HashToken(paymentToken));
        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(attempt.TokenHash);
        }
        catch (FormatException)
        {
            throw new DomainException("The wallet payment request is invalid.");
        }

        if (suppliedHash.Length != expectedHash.Length ||
            !CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash))
        {
            throw new DomainException("The wallet payment token is invalid.");
        }

        return attempt;
    }

    private static long ParseCartId(string paymentToken)
    {
        var separator = paymentToken.IndexOf('.');
        if (separator <= 0 ||
            !long.TryParse(paymentToken.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var cartId) ||
            cartId <= 0 ||
            separator == paymentToken.Length - 1)
        {
            throw new DomainException("The wallet payment token is invalid.");
        }

        return cartId;
    }

    private static string HashToken(string paymentToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(paymentToken)));

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string NormalizeStatus(string status) => status.Trim().ToUpperInvariant() switch
    {
        "PAID" => "PAID",
        "EXPIRED" => "EXPIRED",
        _ => "PENDING"
    };

    private static WalletPaymentRequestDto ToRequestDto(WalletPaymentAttempt attempt, string paymentToken) =>
        new(attempt.Reference, paymentToken, NormalizeStatus(attempt.Status), attempt.Amount, attempt.ExpiresOn);

    private static WalletPaymentStatusDto ToStatusDto(WalletPaymentAttempt attempt) =>
        new(attempt.Reference, NormalizeStatus(attempt.Status), attempt.Amount, attempt.ExpiresOn);
}
