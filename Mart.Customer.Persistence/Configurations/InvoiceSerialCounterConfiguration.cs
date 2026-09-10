using Mart.Customer.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class InvoiceSerialCounterConfiguration : IEntityTypeConfiguration<InvoiceSerialCounter>
{
    public void Configure(EntityTypeBuilder<InvoiceSerialCounter> builder)
    {
        builder.ToTable("InvoiceSerialCounter", "customer", tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_InvoiceSerialCounter_CurrentSerial",
                $"[CurrentSerial] >= 0 AND [CurrentSerial] <= {InvoiceSerialCounter.MaximumSerial}"));
        builder.HasKey(counter => counter.CounterName);
        builder.Property(counter => counter.CounterName)
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.Property(counter => counter.CurrentSerial).IsRequired();

        builder.HasData(new
        {
            CounterName = InvoiceSerialCounter.InvoiceCounterName,
            CurrentSerial = 0L
        });
    }
}
