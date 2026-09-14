namespace DeliveryHub.Domain.Merchants;

// Estado do vínculo OAuth Distribuído com o iFood. UserCode/AuthorizationCodeVerifier
// só existem enquanto a conexão está pendente — são limpos assim que o token chega,
// para não deixar sobrando um código de vínculo já usado.
public sealed record ConexaoIFood(
    string? UserCode,
    string? AuthorizationCodeVerifier,
    DateTimeOffset? CodigoExpiraEm,
    string? AccessToken,
    string? RefreshToken,
    string? TipoToken,
    DateTimeOffset? TokenExpiraEm,
    DateTimeOffset? ConectadoEm)
{
    public bool PendenteDeAutorizacao => UserCode is not null && AccessToken is null;
    public bool Conectado => AccessToken is not null;
}
