namespace DeliveryHub.Infrastructure.Integrations.IFood.Auth;

// Credenciais do app tipo Distribuído — diferente do Centralizado, é o que
// permite N restaurantes de terceiro se autoconectarem.
public sealed class IFoodDistributedOptions
{
    public const string SectionName = "IFoodDistributed";

    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
}
