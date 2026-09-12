using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

// GET /events/v1.0/events:polling — item do array de resposta.
// O schema OpenAPI da referência omite merchantId e salesChannel, mas o guia de
// Eventos de pedido lista os dois como obrigatórios e os exemplos os trazem. É
// o merchantId que permite resolver o tenant já na ingestão, sem depender de
// buscar o detalhe do pedido antes.
internal sealed record IFoodEvent(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("merchantId")] Guid MerchantId,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("fullCode")] string FullCode,
    [property: JsonPropertyName("salesChannel")] string? SalesChannel,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    // Muda de forma a cada tipo de evento — guardado cru para ir como jsonb no inbox.
    [property: JsonPropertyName("metadata")] JsonElement? Metadata)
{
    // Campo novo que o iFood adicione sem avisar sobrevive à ida e volta para
    // JSON, para o inbox guardar o payload fiel e não a nossa interpretação dele.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? CamposNaoMapeados { get; init; }
}

// POST /events/v1.0/events/acknowledgment — item do array enviado.
internal sealed record IFoodAckEvent(
    [property: JsonPropertyName("id")] Guid Id);
