using DeliveryHub.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryHub.Infrastructure.Persistence.Configurations;

internal sealed class ItemPedidoConfiguration : IEntityTypeConfiguration<ItemPedido>
{
    public void Configure(EntityTypeBuilder<ItemPedido> builder)
    {
        builder.ToTable("pedido_itens");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PedidoId).HasColumnName("pedido_id").IsRequired();
        builder.Property(x => x.MerchantId).HasColumnName("merchant_id").IsRequired();
        builder.Property(x => x.Indice).HasColumnName("indice").IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Quantidade).HasColumnName("quantidade").IsRequired();
        builder.Property(x => x.Unidade).HasColumnName("unidade").HasMaxLength(10).IsRequired();
        builder.Property(x => x.PrecoUnitario).HasColumnName("preco_unitario").IsRequired();
        builder.Property(x => x.PrecoTotal).HasColumnName("preco_total").IsRequired();
        builder.Property(x => x.Observacoes).HasColumnName("observacoes").HasMaxLength(500);

        builder.HasIndex(x => x.PedidoId).HasDatabaseName("ix_pedido_itens_pedido");
    }
}
