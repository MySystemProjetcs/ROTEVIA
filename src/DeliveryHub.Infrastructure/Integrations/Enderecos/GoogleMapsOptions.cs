namespace DeliveryHub.Infrastructure.Integrations.Enderecos;

// Vinculado à seção "GoogleMaps" da configuração — valor real só em
// user-secrets (dev) ou variável de ambiente (produção), nunca em
// appsettings.json (mesma regra do IFoodOptions).
//
// Diferente do IFoodOptions, a chave é OPCIONAL: ausente, o resolvedor
// simplesmente não tenta o Google e segue só com a cadeia gratuita
// (ViaCEP + Nominatim + AwesomeAPI) — o cadastro de loja continua
// funcionando sem credencial nenhuma, só com menos precisão em ruas que o
// OpenStreetMap não indexa.
public sealed class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    public string ApiKey { get; set; } = string.Empty;
}
