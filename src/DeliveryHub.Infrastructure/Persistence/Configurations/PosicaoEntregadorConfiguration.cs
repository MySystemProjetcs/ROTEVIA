using DeliveryHub.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryHub.Infrastructure.Persistence.Configurations;

internal sealed class PosicaoEntregadorConfiguration : IEntityTypeConfiguration<PosicaoEntregador>
{
    public void Configure(EntityTypeBuilder<PosicaoEntregador> builder)
    {
        builder.ToTable("posicoes_entregador");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MerchantId).HasColumnName("merchant_id").IsRequired();
        builder.Property(x => x.EntregadorId).HasColumnName("entregador_id").IsRequired();
        builder.Property(x => x.PedidoId).HasColumnName("pedido_id").IsRequired();
        builder.Property(x => x.Latitude).HasColumnName("latitude").IsRequired();
        builder.Property(x => x.Longitude).HasColumnName("longitude").IsRequired();
        builder.Property(x => x.PrecisaoEmMetros).HasColumnName("precisao_metros").IsRequired();
        builder.Property(x => x.CapturadoEm).HasColumnName("capturado_em").IsRequired();
        builder.Property(x => x.RecebidoEm).HasColumnName("recebido_em").IsRequired();

        // A leitura que importa é sempre "últimos pings deste pedido", então o
        // índice segue essa ordem — pedido primeiro, tempo decrescente depois.
        builder.HasIndex(x => new { x.PedidoId, x.CapturadoEm })
            .HasDatabaseName("ix_posicoes_por_pedido");
    }
}
