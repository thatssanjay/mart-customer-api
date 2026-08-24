using Mart.Customer.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class CustomerOrderInvoiceDocumentConfiguration
    : IEntityTypeConfiguration<CustomerOrderInvoiceDocument>
{
    public void Configure(EntityTypeBuilder<CustomerOrderInvoiceDocument> builder)
    {
        builder.ToTable("CustomerOrderInvoiceDocument", "customer");
        builder.HasKey(document => document.CustomerOrderInvoiceDocumentId);
        builder.Ignore(document => document.DocumentType);
        builder.Ignore(document => document.InvoiceTemplateVersion);
        builder.Property(document => document.FileName).HasMaxLength(255).IsRequired();
        builder.Property(document => document.MimeType).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(document => document.StoragePath).HasMaxLength(1000).IsRequired();
        builder.Property(document => document.Sha256Hash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(document => document.CreatedOn)
            .HasColumnName("GeneratedOn")
            .HasConversion(
                value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
                value => value.UtcDateTime);
        builder.HasIndex(document => document.CustomerOrderId).IsUnique();
        builder.HasOne<CustomerOrder>()
            .WithMany()
            .HasForeignKey(document => document.CustomerOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
