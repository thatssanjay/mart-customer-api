using Mart.Customer.Domain.Referrals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CustomerEntity = Mart.Customer.Domain.Customers.Customer;

namespace Mart.Customer.Persistence.Configurations;

public sealed class CustomerReferralConfiguration : IEntityTypeConfiguration<CustomerReferral>
{
    public void Configure(EntityTypeBuilder<CustomerReferral> builder)
    {
        builder.ToTable("CustomerReferral", "Referral");
        builder.HasKey(referral => referral.CustomerReferralId);
        builder.Property(referral => referral.CustomerReferralId).ValueGeneratedOnAdd();
        builder.Property(referral => referral.ReferredMobileNumber).HasMaxLength(30).IsRequired();
        builder.Property(referral => referral.ReferralCode).HasMaxLength(32).IsUnicode(false).IsRequired();
        builder.Property(referral => referral.Status).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.HasIndex(referral => referral.ReferralCode).IsUnique();
        builder.HasIndex(referral => new { referral.ReferrerCustomerId, referral.ReferredMobileNumber })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
        builder.HasOne<CustomerEntity>()
            .WithMany()
            .HasForeignKey(referral => referral.ReferrerCustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerEntity>()
            .WithMany()
            .HasForeignKey(referral => referral.ReferredCustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
