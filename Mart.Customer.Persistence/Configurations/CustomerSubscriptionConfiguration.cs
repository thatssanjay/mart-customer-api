using Mart.Customer.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class CustomerSubscriptionConfiguration : IEntityTypeConfiguration<CustomerSubscription>
{
    public void Configure(EntityTypeBuilder<CustomerSubscription> builder)
    {
        builder.ToTable("CustomerSubscription", "Subscription");
        builder.HasKey(subscription => subscription.Id);
        builder.Property(subscription => subscription.Id).ValueGeneratedOnAdd();
        builder.Property(subscription => subscription.SubscriptionAmount).HasPrecision(18, 2);
        builder.Property(subscription => subscription.Status).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(subscription => subscription.ExtraPointPercentage).HasPrecision(8, 2);
        builder.Property(subscription => subscription.FeeToWalletPercentage).HasPrecision(8, 2);
        builder.Property(subscription => subscription.WalletCreditAmount).HasPrecision(18, 2);

        builder.HasOne<SubscriptionPlan>()
            .WithMany()
            .HasForeignKey(subscription => subscription.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
