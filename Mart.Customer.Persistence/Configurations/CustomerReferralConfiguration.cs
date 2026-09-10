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
        builder.Property(referral => referral.CustomerReferralId)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd();
        builder.Property(referral => referral.ReferredMobileNumber)
            .HasColumnName("ReferredCustomerMobile")
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(referral => referral.ReferralCode).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(referral => referral.Status).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(referral => referral.ReferralConfigId).IsRequired();
        builder.Property(referral => referral.MinimumPurchaseAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(referral => referral.ReferrerRewardPoint).HasPrecision(18, 2).IsRequired();
        builder.Property(referral => referral.ReferredCustomerRewardPoint).HasPrecision(18, 2).IsRequired();
        builder.Property(referral => referral.CreatedOn).HasColumnName("CreatedDate");
        builder.Ignore(referral => referral.IsActive);
        builder.Ignore(referral => referral.OnboardedOn);
        builder.HasIndex(referral => referral.ReferralCode).IsUnique();
        builder.HasOne<ReferralConfiguration>()
            .WithMany()
            .HasForeignKey(referral => referral.ReferralConfigId)
            .OnDelete(DeleteBehavior.Restrict);
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
