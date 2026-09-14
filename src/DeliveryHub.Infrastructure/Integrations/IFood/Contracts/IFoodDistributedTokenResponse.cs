using System.Text.Json.Serialization;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

// POST /authentication/v1.0/oauth/token com grantType=authorization_code ou
// refresh_token. A doc lista accessToken/type/expiresIn na tabela de campos,
// mas o texto confirma refresh token — RefreshToken fica opcional para não
// quebrar se algum grant não o incluir.
internal sealed class IFoodDistributedTokenResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; set; }
}
