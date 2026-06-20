using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CustomerEntity = Mart.Customer.Domain.Customers.Customer;

namespace Mart.Customer.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerEntity>
{
    public void Configure(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.ToTable("CustomerMaster", "customer");

        builder.HasKey(customer => customer.CustomerId);

        builder.Property(customer => customer.CustomerId)
            .ValueGeneratedOnAdd();

        builder.Property(customer => customer.CustomerCode)
            .HasMaxLength(50);

        builder.Property(customer => customer.FirstName)
            .HasMaxLength(100);

        builder.Property(customer => customer.LastName)
            .HasMaxLength(100);

        builder.Property(customer => customer.DisplayName)
            .HasMaxLength(200);

        builder.Property(customer => customer.MobileNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(customer => customer.MobileNumber)
            .IsUnique();

        builder.Property(customer => customer.Email)
            .HasMaxLength(256);

        builder.Property(customer => customer.Gender)
            .HasMaxLength(30);

        builder.Property(customer => customer.AddressLine1)
            .HasMaxLength(250);

        builder.Property(customer => customer.AddressLine2)
            .HasMaxLength(250);

        builder.Property(customer => customer.City)
            .HasMaxLength(100);

        builder.Property(customer => customer.State)
            .HasMaxLength(100);

        builder.Property(customer => customer.Country)
            .HasMaxLength(100);

        builder.Property(customer => customer.PinCode)
            .HasMaxLength(20);

        builder.Property(customer => customer.PreferredLanguage)
            .HasMaxLength(50);

        builder.Property(customer => customer.RegistrationSource)
            .HasMaxLength(100);
    }
}
