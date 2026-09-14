using DeliveryHub.Domain.Couriers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryHub.Infrastructure.Persistence.Configurations;

internal sealed class CourierConfiguration : IEntityTypeConfiguration<Courier>
{
    public void Configure(EntityTypeBuilder<Courier> builder)
    {
        builder.ToTable("couriers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Cpf).HasColumnName("cpf").HasMaxLength(11).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Telefone).HasColumnName("telefone").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ModeloDaMoto).HasColumnName("modelo_da_moto").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Placa).HasColumnName("placa").HasMaxLength(10).IsRequired();
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.DisponivelParaEntrega).HasColumnName("disponivel_para_entrega").IsRequired();

        // Identidade global por CPF (CLAUDE.md §6): é o que impede recadastro
        // do mesmo motoboy em cada restaurante que o convida.
        builder.HasIndex(x => x.Cpf).IsUnique().HasDatabaseName("ux_couriers_cpf");
    }
}

internal sealed class CourierMerchantLinkConfiguration : IEntityTypeConfiguration<CourierMerchantLink>
{
    public void Configure(EntityTypeBuilder<CourierMerchantLink> builder)
    {
        builder.ToTable("courier_merchant_links");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CourierId).HasColumnName("courier_id").IsRequired();
        builder.Property(x => x.MerchantId).HasColumnName("merchant_id").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(256).IsRequired();

        // Hash, nunca o token em claro — mesma lógica de senha_hash em usuarios.
        builder.Property(x => x.TokenConviteHash).HasColumnName("token_convite_hash").HasMaxLength(256);
        builder.Property(x => x.ConviteExpiraEm).HasColumnName("convite_expira_em");
        builder.Property(x => x.VinculadoEm).HasColumnName("vinculado_em").IsRequired();
        builder.Property(x => x.AtivadoEm).HasColumnName("ativado_em");

        // Mesmo CPF não pode ter dois vínculos com a mesma loja — evita
        // convite duplicado enquanto o anterior ainda está pendente.
        builder.HasIndex(x => new { x.CourierId, x.MerchantId })
            .IsUnique()
            .HasDatabaseName("ux_courier_merchant_links_courier_merchant");
    }
}
