using DeliveryHub.Domain.Merchants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryHub.Infrastructure.Persistence.Configurations;

internal sealed class ImpressoraDaLojaConfiguration : IEntityTypeConfiguration<ImpressoraDaLoja>
{
    public void Configure(EntityTypeBuilder<ImpressoraDaLoja> builder)
    {
        builder.ToTable("impressoras_da_loja");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MerchantId).HasColumnName("merchant_id").IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(60).IsRequired();

        // Enum como string: resistente a reordenação (ENGINEERING-GUIDE §8).
        builder.Property(x => x.Modelo)
            .HasColumnName("modelo").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Largura)
            .HasColumnName("largura").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Setor)
            .HasColumnName("setor").HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(x => x.ImprimirAutomaticamente).HasColumnName("imprimir_automaticamente").IsRequired();
        builder.Property(x => x.Ativa).HasColumnName("ativa").IsRequired();
        builder.Property(x => x.CriadaEm).HasColumnName("criada_em").IsRequired();

        // Listagem por loja é a leitura dominante; a de automáticas é um
        // subconjunto filtrado pelo painel ao decidir se imprime um pedido.
        builder.HasIndex(x => x.MerchantId, "ix_impressoras_merchant");
    }
}
