using Mart.Customer.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlan", "Subscription");
        builder.HasKey(plan => plan.SubscriptionId);
        builder.Property(plan => plan.SubscriptionId).ValueGeneratedOnAdd();
        builder.Property(plan => plan.PlanName).HasMaxLength(100).IsRequired();
        builder.Property(plan => plan.SubscriptionFee).HasPrecision(18, 2);
        builder.Property(plan => plan.DurationType).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(plan => plan.ExtraPointPercentage).HasPrecision(8, 2);
        builder.Property(plan => plan.FeeToWalletPercentage).HasPrecision(8, 2);
    }
}
