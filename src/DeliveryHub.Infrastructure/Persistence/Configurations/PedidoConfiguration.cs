using DeliveryHub.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryHub.Infrastructure.Persistence.Configurations;

internal sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("pedidos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MerchantId).HasColumnName("merchant_id").IsRequired();
        builder.Property(x => x.IdExterno).HasColumnName("id_externo").HasMaxLength(128).IsRequired();
        builder.Property(x => x.EhTeste).HasColumnName("eh_teste").IsRequired();
        builder.Property(x => x.RecebidoEm).HasColumnName("recebido_em").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();

        // Evento duplicado não pode criar pedido duplicado, mesmo que passe pelo
        // inbox: a garantia final é aqui.
        builder.HasIndex(x => x.IdExterno)
            .IsUnique()
            .HasDatabaseName("ux_pedidos_id_externo");

        // Painel de pedidos ativos: o índice cresce com os pedidos abertos, não
        // com o histórico (ENGINEERING-GUIDE §7).
        builder.HasIndex(x => new { x.MerchantId, x.RecebidoEm })
            .HasFilter("status IN ('Recebido','Confirmado','EmPreparo','Pronto','Despachado')")
            .HasDatabaseName("ix_pedidos_ativos_por_merchant");
    }
}
