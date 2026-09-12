namespace DeliveryHub.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    // Nunca em appsettings.json: user-secrets no dev, variável de ambiente em
    // produção (ENGINEERING-GUIDE §10).
    public required string ChaveAssinatura { get; set; }

    public required string Emissor { get; set; }
    public required string Audiencia { get; set; }

    // Bearer token de vida curta. Não usamos cookie porque a origem
    // capacitor:// quebra SameSite (CLAUDE.md §3).
    public int DuracaoEmHoras { get; set; } = 8;
}

public static class ClaimsDeliveryHub
{
    // Loja ativa. É deste claim, e só dele, que o ITenantContext resolve o
    // tenant — endpoint nenhum lê claim direto.
    public const string MerchantId = "merchant_id";
}
