using Mart.Customer.Persistence.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class MartStoreConfiguration : IEntityTypeConfiguration<MartStoreEntity>
{
    public void Configure(EntityTypeBuilder<MartStoreEntity> builder)
    {
        builder.ToTable("Store", "mart");
        builder.HasKey(store => store.StoreId);
        builder.Property(store => store.StoreName).HasMaxLength(200).IsRequired();
        builder.Property(store => store.AddressLine1).HasMaxLength(250);
        builder.Property(store => store.AddressLine2).HasMaxLength(250);
        builder.Property(store => store.City).HasMaxLength(100);
        builder.Property(store => store.State).HasMaxLength(100);
        builder.Property(store => store.Country).HasMaxLength(100);
        builder.Property(store => store.PinCode).HasMaxLength(20);
    }
}
