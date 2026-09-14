using System.Net.Http.Headers;
using System.Net.Http.Json;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Infrastructure.Integrations.IFood.Auth;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Merchants;

// Implementa a porta IIFoodMerchantConnector contra o app Distribuído: sem
// TimeProvider fixo aqui porque cada chamada devolve seu próprio expiresIn,
// direto do iFood — não há cache a manter, quem guarda o token é o Merchant.
internal sealed class IFoodMerchantConnector : IIFoodMerchantConnector
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFoodDistributedOptions _options;
    private readonly TimeProvider _timeProvider;

    public IFoodMerchantConnector(
        IHttpClientFactory httpClientFactory, IOptions<IFoodDistributedOptions> options, TimeProvider timeProvider)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<CodigoDeVinculoIFood> SolicitarCodigoAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(IFoodHttpClients.AuthenticationDistributed);

        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/userCode")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["clientId"] = _options.ClientId
            })
        };

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var corpo = await response.Content.ReadFromJsonAsync<IFoodUserCodeResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Resposta de userCode do iFood veio vazia.");

        return new CodigoDeVinculoIFood(
            corpo.UserCode,
            corpo.VerificationUrlComplete,
            corpo.AuthorizationCodeVerifier,
            _timeProvider.GetUtcNow().AddSeconds(corpo.ExpiresIn));
    }

    public Task<TokenDistribuidoIFood> TrocarPorTokenAsync(
        string authorizationCode, string authorizationCodeVerifier, CancellationToken ct) =>
        SolicitarTokenAsync(new Dictionary<string, string>
        {
            ["grantType"] = "authorization_code",
            ["clientId"] = _options.ClientId,
            ["clientSecret"] = _options.ClientSecret,
            ["authorizationCode"] = authorizationCode,
            ["authorizationCodeVerifier"] = authorizationCodeVerifier
        }, ct);

    public Task<TokenDistribuidoIFood> RenovarTokenAsync(string refreshToken, CancellationToken ct) =>
        SolicitarTokenAsync(new Dictionary<string, string>
        {
            ["grantType"] = "refresh_token",
            ["clientId"] = _options.ClientId,
            ["clientSecret"] = _options.ClientSecret,
            ["refreshToken"] = refreshToken
        }, ct);

    private async Task<TokenDistribuidoIFood> SolicitarTokenAsync(Dictionary<string, string> campos, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(IFoodHttpClients.AuthenticationDistributed);

        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/token")
        {
            Content = new FormUrlEncodedContent(campos)
        };

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var corpo = await response.Content.ReadFromJsonAsync<IFoodDistributedTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Resposta de token do iFood veio vazia.");

        if (corpo.RefreshToken is null)
            throw new InvalidOperationException("iFood não devolveu refreshToken para o app Distribuído.");

        return new TokenDistribuidoIFood(
            corpo.AccessToken, corpo.RefreshToken, corpo.Type, _timeProvider.GetUtcNow().AddSeconds(corpo.ExpiresIn));
    }

    public async Task<Guid> DescobrirMerchantIdAsync(string accessToken, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(IFoodHttpClients.MerchantDistributed);

        using var request = new HttpRequestMessage(HttpMethod.Get, "merchants")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("bearer", accessToken) }
        };

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var lojas = await response.Content.ReadFromJsonAsync<List<IFoodMerchantSummary>>(cancellationToken: ct);

        // Uma autorização concede uma loja. Mais de uma, ou nenhuma, é estado
        // que não deveria acontecer no fluxo Distribuído — melhor falhar alto
        // do que adivinhar qual loja o dono quis conectar.
        if (lojas is not { Count: 1 })
        {
            throw new InvalidOperationException(
                $"Esperava exatamente 1 loja autorizada, o iFood devolveu {lojas?.Count ?? 0}.");
        }

        return lojas[0].Id;
    }
}
