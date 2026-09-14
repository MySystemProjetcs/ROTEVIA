using System.Text.Json.Serialization;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

// POST /authentication/v1.0/oauth/userCode — só existe no fluxo Distribuído.
internal sealed class IFoodUserCodeResponse
{
    [JsonPropertyName("userCode")]
    public string UserCode { get; set; } = string.Empty;

    [JsonPropertyName("authorizationCodeVerifier")]
    public string AuthorizationCodeVerifier { get; set; } = string.Empty;

    [JsonPropertyName("verificationUrlComplete")]
    public string VerificationUrlComplete { get; set; } = string.Empty;

    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; set; }
}
