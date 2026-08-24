using Mart.Customer.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class CustomerOrderPaymentConfiguration : IEntityTypeConfiguration<CustomerOrderPayment>
{
    public void Configure(EntityTypeBuilder<CustomerOrderPayment> builder)
    {
        builder.ToTable("CustomerOrderPayment", "customer");
        builder.HasKey(payment => payment.CustomerOrderPaymentId);
        builder.Property(payment => payment.PaymentMode).HasColumnName("PaymentModeCode").HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.TransactionReference).HasColumnName("ReferenceNumber").HasMaxLength(100).IsUnicode(false);
        builder.Property(payment => payment.PaidOn)
            .HasConversion(
                value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
                value => value.UtcDateTime);
    }
}
