namespace DeliveryHub.Application.Abstractions;

public sealed record CodigoDeVinculoIFood(
    string UserCode,
    string VerificationUrlComplete,
    string AuthorizationCodeVerifier,
    DateTimeOffset ExpiraEm);

public sealed record TokenDistribuidoIFood(string AccessToken, string RefreshToken, string Type, DateTimeOffset ExpiraEm);

// Porta do fluxo Distribuído (CLAUDE.md §5, IOrderSource é sobre operar
// pedido — esta é sobre a própria origem conceder acesso). Cada restaurante
// autoriza individualmente pelo Portal do Parceiro; não existe redirect HTTP,
// é o dono da loja levando um código de um lugar para o outro.
public interface IIFoodMerchantConnector
{
    Task<CodigoDeVinculoIFood> SolicitarCodigoAsync(CancellationToken ct);

    Task<TokenDistribuidoIFood> TrocarPorTokenAsync(
        string authorizationCode, string authorizationCodeVerifier, CancellationToken ct);

    Task<TokenDistribuidoIFood> RenovarTokenAsync(string refreshToken, CancellationToken ct);

    // A loja concedida só é conhecida depois da troca — é aqui que ela aparece.
    Task<Guid> DescobrirMerchantIdAsync(string accessToken, CancellationToken ct);
}
