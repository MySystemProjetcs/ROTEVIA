using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryHub.Infrastructure.Persistence.Configurations;

internal sealed class IntegrationInboxEventConfiguration : IEntityTypeConfiguration<IntegrationInboxEvent>
{
    public void Configure(EntityTypeBuilder<IntegrationInboxEvent> builder)
    {
        builder.ToTable("integration_inbox");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(32).IsRequired();
        builder.Property(x => x.ExternalEventId).HasColumnName("external_event_id").HasMaxLength(128).IsRequired();
        builder.Property(x => x.ExternalMerchantId).HasColumnName("external_merchant_id").IsRequired();
        builder.Property(x => x.MerchantId).HasColumnName("merchant_id");
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at").IsRequired();
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");

        builder.Ignore(x => x.EmQuarentena);

        // A única garantia real de idempotência (ENGINEERING-GUIDE §4): é esta
        // constraint que faz o ON CONFLICT DO NOTHING funcionar.
        builder.HasIndex(x => new { x.Source, x.ExternalEventId })
            .IsUnique()
            .HasDatabaseName("ux_inbox_event");

        // Fila do processamento em background: cresce só com o que está
        // pendente, não com o histórico.
        builder.HasIndex(x => x.ReceivedAt)
            .HasFilter("processed_at IS NULL")
            .HasDatabaseName("ix_inbox_pendentes");
    }
}
