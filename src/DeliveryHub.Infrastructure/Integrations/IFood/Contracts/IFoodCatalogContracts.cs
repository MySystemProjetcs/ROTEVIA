using System.Text.Json.Serialization;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

// Contratos do módulo Catalog do iFood. Ficam separados dos demais porque o
// envelope de erro é outro: Auth/Order/Events devolvem
// { "error": { code, message } } (ver IFoodErrorResponse), enquanto Catalog
// devolve o objeto plano com requestId e details[]. Desserializar um com o
// contrato do outro devolve campos nulos silenciosamente.

internal sealed class IFoodCatalog
{
    [JsonPropertyName("catalogId")]
    public string CatalogId { get; init; } = string.Empty;

    // AVAILABLE é o único valor confirmado na doc até agora. String em vez de
    // enum para não quebrar quando o iFood introduzir outro status — o mapper
    // decide o que fazer com valor desconhecido.
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    // Onde o catálogo vale: DEFAULT (delivery) e INDOOR (consumo no local) são
    // os valores vistos na doc. Um merchant pode ter catálogo só para INDOOR,
    // que não interessa ao despacho de entrega.
    [JsonPropertyName("context")]
    public IReadOnlyList<string> Context { get; init; } = [];

    [JsonPropertyName("modifiedAt")]
    public DateTimeOffset? ModifiedAt { get; init; }

    [JsonPropertyName("groupId")]
    public string? GroupId { get; init; }
}

// Envelope de erro do módulo Catalog.
internal sealed class IFoodCatalogErrorResponse
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    // UUID do request. O suporte do iFood pede este valor para rastrear a
    // chamada, então precisa sobreviver até o log.
    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    [JsonPropertyName("details")]
    public IReadOnlyList<IFoodCatalogErrorDetail> Details { get; init; } = [];

    // Texto pronto para log e para a mensagem da exceção: sem os details, um
    // 400 de validação não diz qual campo recusou.
    public string Resumir()
    {
        var baseMsg = $"{Code ?? "(sem código)"}: {Message ?? "(sem mensagem)"}";
        if (RequestId is not null)
            baseMsg += $" [requestId={RequestId}]";

        if (Details.Count == 0)
            return baseMsg;

        var campos = string.Join("; ", Details.Select(d => $"{d.Field}={d.Code} ({d.Message})"));
        return $"{baseMsg} — {campos}";
    }
}

internal sealed class IFoodCatalogErrorDetail
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("field")]
    public string? Field { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
