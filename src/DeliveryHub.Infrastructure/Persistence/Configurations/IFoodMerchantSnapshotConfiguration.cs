using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryHub.Infrastructure.Persistence.Configurations;

internal sealed class IFoodMerchantSnapshotConfiguration : IEntityTypeConfiguration<IFoodMerchantSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<IFoodMerchantSnapshotEntity> builder)
    {
        builder.ToTable("ifood_merchant_snapshots");
        builder.HasKey(x => x.MerchantId);

        builder.Property(x => x.MerchantId).HasColumnName("merchant_id");
        builder.Property(x => x.DetailsJson).HasColumnName("details").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.StatusJson).HasColumnName("status").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.OpeningHoursJson).HasColumnName("opening_hours").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<DeliveryHub.Domain.Merchants.Merchant>()
            .WithMany()
            .HasForeignKey(x => x.MerchantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}