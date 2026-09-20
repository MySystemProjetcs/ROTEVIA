using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Merchants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryHub.Infrastructure.Persistence.Configurations;

internal sealed class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    private readonly ICredentialCipher _cipher;

    public MerchantConfiguration(ICredentialCipher cipher)
    {
        _cipher = cipher;
    }

    public void Configure(EntityTypeBuilder<Merchant> builder)
    {
        builder.ToTable("merchants");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IFoodMerchantId).HasColumnName("ifood_merchant_id");
        builder.Property(x => x.NoventaENoveAppShopId).HasColumnName("noventa_e_nove_app_shop_id").HasMaxLength(64);
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.TaxaPadraoPorEntrega).HasColumnName("taxa_padrao_por_entrega").IsRequired();

        // Nulo até o restaurante autorizar pelo Portal do Parceiro — por isso
        // sem IsRequired, e o índice único convive bem com múltiplos nulos.
        builder.HasIndex(x => x.IFoodMerchantId)
            .IsUnique()
            .HasDatabaseName("ux_merchants_ifood_id");

        // Mesmo raciocínio do índice do iFood: nulo até a loja ser cadastrada
        // na 99Food, e múltiplos nulos convivem bem com índice único no Postgres.
        builder.HasIndex(x => x.NoventaENoveAppShopId)
            .IsUnique()
            .HasDatabaseName("ux_merchants_noventa_e_nove_app_shop_id");

        // Endereço da loja por table splitting, igual ao endereço de entrega do
        // pedido: é dado do merchant, não entidade com vida própria.
        builder.OwnsOne(x => x.Endereco, endereco =>
        {
            endereco.Property(x => x.Logradouro).HasColumnName("endereco_logradouro").HasMaxLength(200);
            endereco.Property(x => x.Numero).HasColumnName("endereco_numero").HasMaxLength(20);
            endereco.Property(x => x.Bairro).HasColumnName("endereco_bairro").HasMaxLength(120);
            endereco.Property(x => x.Cidade).HasColumnName("endereco_cidade").HasMaxLength(120);
            endereco.Property(x => x.Estado).HasColumnName("endereco_estado").HasMaxLength(2);
            endereco.Property(x => x.Cep).HasColumnName("endereco_cep").HasMaxLength(20);
            endereco.Property(x => x.Complemento).HasColumnName("endereco_complemento").HasMaxLength(200);
            endereco.Property(x => x.Referencia).HasColumnName("endereco_referencia").HasMaxLength(200);
            endereco.Property(x => x.Latitude).HasColumnName("endereco_latitude");
            endereco.Property(x => x.Longitude).HasColumnName("endereco_longitude");
        });

        builder.OwnsOne(x => x.ConexaoIFood, conexao =>
        {
            conexao.Property(x => x.UserCode).HasColumnName("ifood_user_code").HasMaxLength(20);
            conexao.Property(x => x.CodigoExpiraEm).HasColumnName("ifood_codigo_expira_em");

            // access/refresh token cifrados em repouso (AES-GCM): um dump do
            // banco sozinho não dá acesso a nenhuma loja.
            conexao.Property(x => x.AuthorizationCodeVerifier)
                .HasColumnName("ifood_authorization_code_verifier")
                .HasConversion(v => v == null ? null : _cipher.Proteger(v), v => v == null ? null : _cipher.Desproteger(v));

            conexao.Property(x => x.AccessToken)
                .HasColumnName("ifood_access_token")
                .HasConversion(v => v == null ? null : _cipher.Proteger(v), v => v == null ? null : _cipher.Desproteger(v));

            conexao.Property(x => x.RefreshToken)
                .HasColumnName("ifood_refresh_token")
                .HasConversion(v => v == null ? null : _cipher.Proteger(v), v => v == null ? null : _cipher.Desproteger(v));

            conexao.Property(x => x.TipoToken).HasColumnName("ifood_tipo_token").HasMaxLength(20);
            conexao.Property(x => x.TokenExpiraEm).HasColumnName("ifood_token_expira_em");
            conexao.Property(x => x.ConectadoEm).HasColumnName("ifood_conectado_em");
        });

        builder.Navigation(x => x.ConexaoIFood).IsRequired(false);
    }
}
