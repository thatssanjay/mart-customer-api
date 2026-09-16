using Mart.Customer.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mart.Customer.Persistence.Configurations;

public sealed class CustomerOrderConfiguration : IEntityTypeConfiguration<CustomerOrder>
{
    public void Configure(EntityTypeBuilder<CustomerOrder> builder)
    {
        builder.ToTable("CustomerOrder", "customer");
        builder.HasKey(order => order.CustomerOrderId);
        builder.Property(order => order.InvoiceNumber).HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.HasIndex(order => order.InvoiceNumber).IsUnique();
        builder.Property(order => order.VerificationCode)
            .HasConversion(value => Guid.Parse(value), value => value.ToString("N"))
            .IsRequired();
        builder.HasIndex(order => order.VerificationCode).IsUnique();
        builder.HasIndex(order => order.CustomerCartId).IsUnique();
        builder.HasOne<Mart.Customer.Domain.Carts.CustomerCart>()
            .WithOne()
            .HasForeignKey<CustomerOrder>(order => order.CustomerCartId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(order => order.OrderDate)
            .HasColumnName("InvoiceDate")
            .HasConversion(
                value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
                value => value.UtcDateTime);
        builder.Ignore(order => order.TotalItemCount);
        builder.Property(order => order.GrossAmount).HasColumnName("SubTotalAmount").HasPrecision(18, 2);
        builder.Property(order => order.DiscountAmount).HasPrecision(18, 2);
        builder.Property(order => order.TaxableAmount).HasPrecision(18, 2);
        builder.Property(order => order.CGSTAmount).HasPrecision(18, 2);
        builder.Property(order => order.SGSTAmount).HasPrecision(18, 2);
        builder.Property(order => order.IGSTAmount).HasPrecision(18, 2);
        builder.Property(order => order.GSTAmount).HasPrecision(18, 2);
        builder.Property(order => order.RoundOffAmount).HasPrecision(18, 2);
        builder.Ignore(order => order.NetAmount);
        builder.Ignore(order => order.RedemptionWalletTypeId);
        builder.Property(order => order.RedeemPointsUsed).HasPrecision(18, 2);
        builder.Property(order => order.RedemptionAmount).HasColumnName("RedeemAmount").HasPrecision(18, 2);
        builder.Property(order => order.FinalPayableAmount).HasPrecision(18, 2);
        builder.Property(order => order.RewardEarned).HasColumnName("RewardPointsEarned").HasPrecision(18, 2);
        builder.Property(order => order.CashbackEarned).HasColumnName("CashbackAmount").HasPrecision(18, 2);
        builder.Property(order => order.OrderStatus).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Ignore(order => order.InvoiceStatus);
        builder.Ignore(order => order.InvoiceArchivePath);
        builder.Property(order => order.InvoiceTemplateVersion)
            .HasConversion(
                value => short.Parse(value.TrimStart('v', 'V')),
                value => $"v{value}")
            .IsRequired();
        builder.Property(order => order.IsPointsAwarded)
            .HasColumnName("IsPointsAwarded")
            .IsRequired()
            .HasDefaultValue(false);
        builder.Property(order => order.PointsAwardedDate).HasColumnType("datetime2");
        builder.Property(order => order.PointsAwardedBy).HasMaxLength(10);
        builder.Property(order => order.Status).IsRequired();
        builder.Property(order => order.Remarks).HasMaxLength(500);
        builder.Property(order => order.CreatedBy).HasColumnName("CashierUserId");
        builder.Property(order => order.CustomerCodeSnapshot).HasMaxLength(50).IsUnicode(false);
        builder.Property(order => order.CustomerNameSnapshot).HasMaxLength(300);
        builder.Property(order => order.CustomerMobileSnapshot).HasMaxLength(20).IsUnicode(false);
        builder.Property(order => order.CustomerAddressSnapshot).HasMaxLength(1000);
        builder.Property(order => order.StoreNameSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(order => order.StoreGSTINSnapshot).HasMaxLength(20).IsUnicode(false);
        builder.Property(order => order.StoreAddressSnapshot).HasMaxLength(1000).IsRequired();
        builder.Property(order => order.StoreStateCodeSnapshot).HasMaxLength(2).IsFixedLength().IsUnicode(false);

        builder.HasMany(order => order.Items)
            .WithOne(item => item.CustomerOrder)
            .HasForeignKey(item => item.CustomerOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(order => order.Payments)
            .WithOne(payment => payment.CustomerOrder)
            .HasForeignKey(payment => payment.CustomerOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(order => order.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(order => order.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
