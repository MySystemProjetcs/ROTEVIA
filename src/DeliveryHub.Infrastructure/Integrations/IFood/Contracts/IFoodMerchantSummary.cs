using System.Text.Json.Serialization;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

// GET /merchant/v1.0/merchants — lista as lojas que o token enxerga. No fluxo
// Distribuído, uma autorização concede uma loja; é assim que descobrimos qual.
internal sealed class IFoodMerchantSummary
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("corporateName")]
    public string CorporateName { get; set; } = string.Empty;
}
