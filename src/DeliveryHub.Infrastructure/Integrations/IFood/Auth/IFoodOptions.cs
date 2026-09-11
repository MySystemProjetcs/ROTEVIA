namespace DeliveryHub.Infrastructure.Integrations.IFood.Auth;

// Vinculado à seção "IFood" da configuração — valores reais só em
// user-secrets (dev) ou variável de ambiente (produção), nunca em
// appsettings.json (CLAUDE.md §7 e ENGINEERING-GUIDE.md §10).
public sealed class IFoodOptions
{
    public const string SectionName = "IFood";

    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
}
