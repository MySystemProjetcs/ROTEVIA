using System.Text.Json.Serialization;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

// Error403PollingView: o 403 do polling lista as lojas cuja autorização caiu,
// em vez de falhar em bloco. Serve para suspender só esses vínculos.
internal sealed record IFoodPollingErrorResponse(
    [property: JsonPropertyName("error")] IFoodPollingErrorDetail Error);

internal sealed record IFoodPollingErrorDetail(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("unauthorizedMerchants")] IReadOnlyList<Guid>? UnauthorizedMerchants);
