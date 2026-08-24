namespace Mart.Customer.Domain.Orders;

public sealed class CustomerOrder
{
    private readonly List<CustomerOrderItem> _items = [];
    private readonly List<CustomerOrderPayment> _payments = [];

    private CustomerOrder() { }

    public long CustomerOrderId { get; private set; }
    public long CustomerCartId { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public string VerificationCode { get; private set; } = string.Empty;
    public long CustomerId { get; private set; }
    public long FranchiseId { get; private set; }
    public long MartStoreId { get; private set; }
    public DateTime OrderDate { get; private set; }
    public int TotalItemCount { get; private set; }
    public string? CustomerCodeSnapshot { get; private set; }
    public string? CustomerNameSnapshot { get; private set; }
    public string? CustomerMobileSnapshot { get; private set; }
    public string? CustomerAddressSnapshot { get; private set; }
    public string StoreNameSnapshot { get; private set; } = string.Empty;
    public string? StoreGSTINSnapshot { get; private set; }
    public string StoreAddressSnapshot { get; private set; } = string.Empty;
    public string? StoreStateCodeSnapshot { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal CGSTAmount { get; private set; }
    public decimal SGSTAmount { get; private set; }
    public decimal IGSTAmount { get; private set; }
    public decimal GSTAmount { get; private set; }
    public decimal RoundOffAmount { get; private set; }
    public decimal NetAmount => TaxableAmount + GSTAmount + RoundOffAmount;
    public int? RedemptionWalletTypeId { get; private set; }
    public decimal RedeemPointsUsed { get; private set; }
    public decimal RedemptionAmount { get; private set; }
    public decimal FinalPayableAmount { get; private set; }
    public decimal RewardEarned { get; private set; }
    public decimal CashbackEarned { get; private set; }
    public string OrderStatus { get; private set; } = string.Empty;
    public string InvoiceStatus { get; private set; } = string.Empty;
    public string? InvoiceArchivePath { get; private set; }
    public string InvoiceTemplateVersion { get; private set; } = string.Empty;
    public long CreatedBy { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public IReadOnlyCollection<CustomerOrderItem> Items => _items;
    public IReadOnlyCollection<CustomerOrderPayment> Payments => _payments;

    public static CustomerOrder Create(
        long customerCartId,
        string invoiceNumber,
        long customerId,
        long franchiseId,
        long martStoreId,
        DateTime orderDate,
        int totalItemCount,
        decimal grossAmount,
        decimal discountAmount,
        decimal gstAmount,
        decimal netAmount,
        int? redemptionWalletTypeId,
        decimal redemptionAmount,
        decimal finalPayableAmount,
        long createdBy,
        string storeNameSnapshot = "Store",
        string storeAddressSnapshot = "Address unavailable",
        string? customerCodeSnapshot = null,
        string? customerNameSnapshot = null,
        string? customerMobileSnapshot = null,
        string? customerAddressSnapshot = null,
        string invoiceTemplateVersion = "v1",
        string? verificationCode = null)
    {
        return new CustomerOrder
        {
            CustomerCartId = customerCartId,
            InvoiceNumber = invoiceNumber.Trim(),
            VerificationCode = string.IsNullOrWhiteSpace(verificationCode)
                ? Guid.NewGuid().ToString("N")
                : verificationCode.Trim(),
            CustomerId = customerId,
            FranchiseId = franchiseId,
            MartStoreId = martStoreId,
            OrderDate = orderDate,
            TotalItemCount = totalItemCount,
            CustomerCodeSnapshot = customerCodeSnapshot,
            CustomerNameSnapshot = customerNameSnapshot,
            CustomerMobileSnapshot = customerMobileSnapshot,
            CustomerAddressSnapshot = customerAddressSnapshot,
            StoreNameSnapshot = storeNameSnapshot.Trim(),
            StoreAddressSnapshot = storeAddressSnapshot.Trim(),
            GrossAmount = grossAmount,
            DiscountAmount = discountAmount,
            TaxableAmount = netAmount - gstAmount,
            GSTAmount = gstAmount,
            RedemptionWalletTypeId = redemptionWalletTypeId,
            RedemptionAmount = redemptionAmount,
            FinalPayableAmount = finalPayableAmount,
            RewardEarned = 0,
            CashbackEarned = 0,
            OrderStatus = "Paid",
            InvoiceStatus = "Pending",
            InvoiceTemplateVersion = string.IsNullOrWhiteSpace(invoiceTemplateVersion)
                ? "v1"
                : invoiceTemplateVersion.Trim(),
            CreatedBy = createdBy,
            CreatedOn = orderDate
        };
    }

    public void AddItem(
        long customerCartItemId,
        long productId,
        string productName,
        decimal quantity,
        decimal unitPrice,
        decimal mrp,
        decimal grossAmount,
        decimal discountAmount,
        decimal gstPercent,
        decimal gstAmount,
        decimal lineTotal)
    {
        _items.Add(CustomerOrderItem.Create(
            customerCartItemId, productId, productName, quantity, unitPrice, mrp,
            grossAmount, discountAmount, gstPercent, gstAmount, lineTotal));
    }

    public void AddPayment(string paymentMode, decimal amount, string? transactionReference)
    {
        _payments.Add(CustomerOrderPayment.Create(paymentMode, amount, transactionReference, OrderDate));
    }

    public void SetCashbackEarned(decimal amount) => CashbackEarned = amount;

    public void SetRewardEarned(decimal amount) => RewardEarned = amount;

    public void MarkInvoiceArchived(string archivePath)
    {
        InvoiceStatus = "Archived";
        InvoiceArchivePath = archivePath;
    }

    public void MarkInvoicePending()
    {
        InvoiceStatus = "Pending";
        InvoiceArchivePath = null;
    }

    public void RestorePersistenceState(
        int totalItemCount,
        string? invoiceArchivePath,
        int? redemptionWalletTypeId = null)
    {
        TotalItemCount = totalItemCount;
        RedemptionWalletTypeId = redemptionWalletTypeId;
        if (string.IsNullOrWhiteSpace(invoiceArchivePath))
        {
            MarkInvoicePending();
            return;
        }

        MarkInvoiceArchived(invoiceArchivePath);
    }

    public void SetMasterSnapshots(
        string storeName,
        string storeAddress,
        string? customerCode,
        string? customerName,
        string? customerMobile,
        string? customerAddress)
    {
        StoreNameSnapshot = storeName.Trim();
        StoreAddressSnapshot = storeAddress.Trim();
        CustomerCodeSnapshot = customerCode;
        CustomerNameSnapshot = customerName;
        CustomerMobileSnapshot = customerMobile;
        CustomerAddressSnapshot = customerAddress;
    }

    public void ApplyTaxSplit(bool isIntraState)
    {
        foreach (var item in _items)
        {
            item.ApplyTaxSplit(isIntraState);
        }

        CGSTAmount = _items.Sum(item => item.CGSTAmount);
        SGSTAmount = _items.Sum(item => item.SGSTAmount);
        IGSTAmount = _items.Sum(item => item.IGSTAmount);
    }
}
