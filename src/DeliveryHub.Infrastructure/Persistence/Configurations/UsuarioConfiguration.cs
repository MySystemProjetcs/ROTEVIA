using DeliveryHub.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryHub.Infrastructure.Persistence.Configurations;

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        builder.Property(x => x.SenhaHash).HasColumnName("senha_hash").HasMaxLength(256).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Papel).HasColumnName("papel").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.DeveTrocarSenha).HasColumnName("deve_trocar_senha").IsRequired();
        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        // Sem HasMaxLength: o teto de tamanho é regra do domínio (DefinirFoto),
        // e text no Postgres não custa mais que varchar(n).
        builder.Property(x => x.FotoBase64).HasColumnName("foto_base64");

        // Consultado a cada login, e duplicidade de e-mail permitiria dois
        // usuários disputando o mesmo acesso.
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_usuarios_email");
    }
}

internal sealed class UsuarioMerchantConfiguration : IEntityTypeConfiguration<UsuarioMerchant>
{
    public void Configure(EntityTypeBuilder<UsuarioMerchant> builder)
    {
        builder.ToTable("usuario_merchants");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(x => x.MerchantId).HasColumnName("merchant_id").IsRequired();
        builder.Property(x => x.VinculadoEm).HasColumnName("vinculado_em").IsRequired();

        builder.HasIndex(x => new { x.UsuarioId, x.MerchantId })
            .IsUnique()
            .HasDatabaseName("ux_usuario_merchant");
    }
}
