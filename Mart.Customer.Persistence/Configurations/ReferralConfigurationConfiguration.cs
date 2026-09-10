using Mart.Customer.Domain.Referrals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class ReferralConfigurationConfiguration : IEntityTypeConfiguration<ReferralConfiguration>
{
    public void Configure(EntityTypeBuilder<ReferralConfiguration> builder)
    {
        builder.ToTable("ReferralConfig", "Referral");
        builder.HasKey(configuration => configuration.Id);
        builder.Property(configuration => configuration.MinimumPurchaseAmount).HasPrecision(18, 2);
        builder.Property(configuration => configuration.ReferrerRewardPoint).HasPrecision(18, 2);
        builder.Property(configuration => configuration.ReferredCustomerRewardPoint).HasPrecision(18, 2);
    }
}
