using DeliveryHub.Domain.Merchants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryHub.Infrastructure.Persistence.Configurations;

internal sealed class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> builder)
    {
        builder.ToTable("merchants");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IFoodMerchantId).HasColumnName("ifood_merchant_id").IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        // Resolvido a cada evento da ingestão: tem que ser único e indexado.
        builder.HasIndex(x => x.IFoodMerchantId)
            .IsUnique()
            .HasDatabaseName("ux_merchants_ifood_id");
    }
}
