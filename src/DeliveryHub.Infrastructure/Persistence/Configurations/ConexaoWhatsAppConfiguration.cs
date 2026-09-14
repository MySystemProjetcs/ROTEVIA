using System.Text.Json;
using DeliveryHub.Domain.Merchants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryHub.Infrastructure.Persistence.Configurations;

// Arquivo separado do MerchantConfiguration de propósito: nada aqui precisa do
// ICredentialCipher (status/telefone/histórico não são segredo — é conversa
// de convite de motoboy, não credencial), então entra normal no
// ApplyConfigurationsFromAssembly — sem tocar na exclusão manual que já existe
// pra MerchantConfiguration.
internal sealed class ConexaoWhatsAppConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> builder)
    {
        builder.OwnsOne(x => x.ConexaoWhatsApp, conexao =>
        {
            conexao.Property(x => x.Status).HasColumnName("whatsapp_status").HasConversion<string>().HasMaxLength(30);
            conexao.Property(x => x.Telefone).HasColumnName("whatsapp_telefone").HasMaxLength(20);
            conexao.Property(x => x.ConectadoEm).HasColumnName("whatsapp_conectado_em");

            // Sem tabela de mensagens: enviada e recebida ficam no mesmo
            // documento jsonb, na ordem em que aconteceram. Volume de
            // conversa de convite não justifica uma tabela própria agora.
            conexao.Property(x => x.Historico)
                .HasColumnName("whatsapp_historico")
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<MensagemWhatsApp>>(v, (JsonSerializerOptions?)null) ?? new List<MensagemWhatsApp>())
                .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<MensagemWhatsApp>>(
                    (a, b) => (a ?? Array.Empty<MensagemWhatsApp>()).SequenceEqual(b ?? Array.Empty<MensagemWhatsApp>()),
                    v => v.Aggregate(0, (hash, m) => HashCode.Combine(hash, m.ExternalId)),
                    v => v.ToList()));
        });

        builder.Navigation(x => x.ConexaoWhatsApp).IsRequired(false);
    }
}
