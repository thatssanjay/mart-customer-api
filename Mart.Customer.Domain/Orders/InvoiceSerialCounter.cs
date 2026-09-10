namespace Mart.Customer.Domain.Orders;

public sealed class InvoiceSerialCounter
{
    public const string InvoiceCounterName = "Invoice";
    public const long MaximumSerial = 99_999_999;

    private InvoiceSerialCounter() { }

    public string CounterName { get; private set; } = string.Empty;
    public long CurrentSerial { get; private set; }
}
