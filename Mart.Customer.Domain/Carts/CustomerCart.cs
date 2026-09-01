using System.Text.Json;

namespace Mart.Customer.Domain.Carts;

public sealed class CustomerCart
{
    private readonly List<CustomerCartItem> _items = [];

    private CustomerCart()
    {
    }

    public long CustomerCartId { get; private set; }
    public long CustomerId { get; private set; }
    public long FranchiseId { get; private set; }
    public long MartStoreId { get; private set; }
    public string CartNumber { get; private set; } = string.Empty;
    public string CartStatus { get; private set; } = string.Empty;
    public int TotalItemCount { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal GSTAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal RewardPointsToRedeem { get; private set; }
    public decimal RedeemAmount { get; private set; }
    public decimal FinalPayableAmount { get; private set; }
    public long AddedByCashierId { get; private set; }
    public DateTime? CustomerApprovedOn { get; private set; }
    public DateTime? PaidOn { get; private set; }
    public DateTime? CancelledOn { get; private set; }
    public string? Remarks { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public DateTime? ModifiedOn { get; private set; }
    public IReadOnlyCollection<CustomerCartItem> Items => _items;

    public static CustomerCart Create(
        long customerId,
        long franchiseId,
        long martStoreId,
        string cartNumber,
        long addedByCashierId)
    {
        var cart = new CustomerCart
        {
            CustomerId = customerId,
            FranchiseId = franchiseId,
            MartStoreId = martStoreId,
            CartNumber = cartNumber,
            CartStatus = "Active",
            TotalItemCount = 0,
            GrossAmount = 0,
            DiscountAmount = 0,
            GSTAmount = 0,
            NetAmount = 0,
            RewardPointsToRedeem = 0,
            RedeemAmount = 0,
            FinalPayableAmount = 0,
            AddedByCashierId = addedByCashierId,
            Remarks = null,
            CreatedOn = DateTime.UtcNow
        };

        return cart;
    }

    public CustomerCartItem AddItem(
        long productId,
        string productNameSnapshot,
        decimal quantity,
        decimal unitPrice,
        decimal mrp,
        decimal discountAmount,
        decimal gstPercent,
        long addedByCashierId)
    {
        if (!string.Equals(CartStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new Common.DomainException("Items can only be added to an active cart.");
        }

        var item = CustomerCartItem.Create(
            productId,
            productNameSnapshot,
            quantity,
            unitPrice,
            mrp,
            discountAmount,
            gstPercent,
            addedByCashierId);

        _items.Add(item);
        RecalculateTotals();
        ModifiedOn = DateTime.UtcNow;

        return item;
    }

    public CustomerCartItem? UpdateItem(
        long customerCartItemId,
        string productNameSnapshot,
        decimal quantity,
        decimal unitPrice,
        decimal mrp,
        decimal discountAmount,
        decimal gstPercent)
    {
        if (!string.Equals(CartStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new Common.DomainException("Items can only be updated in an active cart.");
        }

        var item = _items.SingleOrDefault(cartItem =>
            cartItem.CustomerCartItemId == customerCartItemId);

        if (item is null)
        {
            return null;
        }

        if (quantity == 0)
        {
            item.Update(
                productNameSnapshot,
                quantity,
                unitPrice,
                mrp,
                discountAmount,
                gstPercent);
            _items.Remove(item);
            RecalculateTotals();
            ModifiedOn = DateTime.UtcNow;

            return item;
        }

        item.Update(
            productNameSnapshot,
            quantity,
            unitPrice,
            mrp,
            discountAmount,
            gstPercent);

        RecalculateTotals();
        ModifiedOn = DateTime.UtcNow;

        return item;
    }

    public bool RemoveItemsByProductId(long productId)
    {
        if (!string.Equals(CartStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new Common.DomainException("Items can only be removed from an active cart.");
        }

        var itemsToRemove = _items
            .Where(item => item.ProductId == productId)
            .ToList();

        if (itemsToRemove.Count == 0)
        {
            return false;
        }

        foreach (var item in itemsToRemove)
        {
            _items.Remove(item);
        }

        RecalculateTotals();
        ModifiedOn = DateTime.UtcNow;

        return true;
    }

    private void RecalculateTotals()
    {
        TotalItemCount = _items.Count;
        GrossAmount = _items.Sum(cartItem => cartItem.GrossAmount);
        DiscountAmount = _items.Sum(cartItem => cartItem.DiscountAmount);
        GSTAmount = _items.Sum(cartItem => cartItem.GSTAmount);
        NetAmount = _items.Sum(cartItem => cartItem.LineTotal);
        FinalPayableAmount = NetAmount - RedeemAmount;
    }

    public void ChangeStatus(string status)
    {
        var normalizedStatus = status.Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => "Active",
            "PAYMENTPENDING" => "PaymentPending",
            "CUSTOMERAPPROVED" => "CustomerApproved",
            "PAID" => "Paid",
            "CANCELLED" => "Cancelled",
            _ => throw new Common.DomainException("Invalid cart status. Allowed values are Active, PaymentPending, CustomerApproved, Paid, and Cancelled.")
        };

        if (string.Equals(CartStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var transitionIsAllowed = CartStatus switch
        {
            "Active" => normalizedStatus is "PaymentPending" or "CustomerApproved" or "Cancelled",
            "PaymentPending" => normalizedStatus is "Active" or "Paid" or "Cancelled",
            "CustomerApproved" => normalizedStatus is "Paid" or "Cancelled",
            _ => false
        };

        if (!transitionIsAllowed)
        {
            throw new Common.DomainException($"Cart status cannot be changed from {CartStatus} to {normalizedStatus}.");
        }

        var changedOn = DateTime.UtcNow;
        CartStatus = normalizedStatus;
        ModifiedOn = changedOn;

        switch (normalizedStatus)
        {
            case "CustomerApproved":
                CustomerApprovedOn = changedOn;
                break;
            case "Paid":
                PaidOn = changedOn;
                break;
            case "Cancelled":
                CancelledOn = changedOn;
                break;
        }
    }

    public void Cancel(string? remarks, DateTime cancelledOn)
    {
        if (!string.Equals(CartStatus, "Active", StringComparison.OrdinalIgnoreCase) ||
            PaidOn.HasValue)
        {
            throw new Common.DomainException("Only an active unpaid cart can be cancelled.");
        }

        CartStatus = "Cancelled";
        CancelledOn = cancelledOn;
        Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
    }

    public void MarkPaid(decimal redemptionAmount, DateTime paidOn)
    {
        if (!string.Equals(CartStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new Common.DomainException("Only an active cart can be checked out.");
        }

        if (redemptionAmount < 0 || redemptionAmount > NetAmount)
        {
            throw new Common.DomainException("Invalid checkout redemption amount.");
        }

        RewardPointsToRedeem = redemptionAmount;
        RedeemAmount = redemptionAmount;
        FinalPayableAmount = NetAmount - redemptionAmount;
        CartStatus = "Paid";
        PaidOn = paidOn;
        ModifiedOn = paidOn;
    }

    public WalletPaymentAttempt BeginWalletPaymentAttempt(
        string reference,
        string tokenHash,
        decimal amount,
        DateTime expiresOn)
    {
        if (!string.Equals(CartStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new Common.DomainException("Wallet payment can only be raised for an active cart.");
        }

        var currentAttempt = GetWalletPaymentAttempt();
        if (currentAttempt is not null &&
            string.Equals(currentAttempt.Status, "PAID", StringComparison.OrdinalIgnoreCase))
        {
            throw new Common.DomainException("The current wallet payment is already paid and must be confirmed.");
        }

        var attempt = new WalletPaymentAttempt(
            reference,
            "PENDING",
            tokenHash,
            amount,
            expiresOn);
        Remarks = JsonSerializer.Serialize(attempt, WalletPaymentAttempt.JsonOptions);
        ModifiedOn = DateTime.UtcNow;
        return attempt;
    }

    public WalletPaymentAttempt? GetWalletPaymentAttempt()
    {
        if (string.IsNullOrWhiteSpace(Remarks) || !Remarks.TrimStart().StartsWith('{'))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WalletPaymentAttempt>(
                Remarks,
                WalletPaymentAttempt.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public WalletPaymentAttempt ExpireWalletPaymentAttempt(WalletPaymentAttempt attempt)
    {
        var expiredAttempt = attempt with { Status = "EXPIRED" };
        Remarks = JsonSerializer.Serialize(expiredAttempt, WalletPaymentAttempt.JsonOptions);
        ModifiedOn = DateTime.UtcNow;
        return expiredAttempt;
    }
}

public sealed record WalletPaymentAttempt(
    string Reference,
    string Status,
    string TokenHash,
    decimal Amount,
    DateTime ExpiresOn)
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
