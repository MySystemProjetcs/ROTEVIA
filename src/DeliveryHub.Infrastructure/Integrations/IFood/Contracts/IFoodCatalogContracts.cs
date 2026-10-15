using System.Text.Json.Serialization;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

// Envelope de erro do módulo Catalog. Fica separado dos demais porque é
// outro formato: Auth/Order/Events devolvem { "error": { code, message } }
// (ver IFoodErrorResponse), enquanto Catalog devolve o objeto plano com
// requestId e details[]. Desserializar um com o contrato do outro devolve
// campos nulos silenciosamente. As respostas de sucesso do Catalog são lidas
// campo a campo no IFoodCatalogClient.
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
