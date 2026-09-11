using System.Text.Json.Serialization;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

// Corpo de erro padrão da API do iFood: { "error": { "code": "...", "message": "..." } }
internal sealed class IFoodErrorResponse
{
    [JsonPropertyName("error")]
    public IFoodErrorDetail Error { get; set; } = new();
}

internal sealed class IFoodErrorDetail
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
