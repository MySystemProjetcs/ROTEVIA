using System.Net.Http.Headers;
using System.Net.Http.Json;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Auth;

// POST /authentication/v1.0/oauth/token, grantType=client_credentials.
// App Centralizado não recebe refresh token (CLAUDE.md §7) — a única forma
// de renovar é reautenticar com clientId/clientSecret antes do token expirar.
internal sealed class IFoodAuthenticator : IIFoodAuthenticator
{
    // A renovação sai do expiresIn de cada resposta, e a margem é proporcional,
    // nunca absoluta: o iFood avisa que pode mudar os prazos a qualquer momento,
    // e uma margem fixa maior que o expiresIn jogaria _renewAt para o passado,
    // fazendo toda chamada reautenticar — excesso de requisição bloqueia o app.
    private const double RenewAtLifetimeFraction = 0.9;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFoodOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private AuthenticationHeaderValue? _cachedHeader;
    private DateTimeOffset _renewAt = DateTimeOffset.MinValue;

    // Precisa ser singleton para o cache do token sobreviver entre chamadas; por
    // isso recebe a factory e cria o HttpClient por requisição, em vez de segurar
    // um HttpClient para sempre.
    public IFoodAuthenticator(IHttpClientFactory httpClientFactory, IOptions<IFoodOptions> options, TimeProvider timeProvider)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<AuthenticationHeaderValue> GetAuthorizationHeaderAsync(CancellationToken ct)
    {
        if (_cachedHeader is not null && _timeProvider.GetUtcNow() < _renewAt)
            return _cachedHeader;

        await _refreshLock.WaitAsync(ct);
        try
        {
            if (_cachedHeader is not null && _timeProvider.GetUtcNow() < _renewAt)
                return _cachedHeader;

            return await AuthenticateAsync(ct);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void InvalidateCache()
    {
        _cachedHeader = null;
        _renewAt = DateTimeOffset.MinValue;
    }

    private async Task<AuthenticationHeaderValue> AuthenticateAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grantType"] = "client_credentials",
                ["clientId"] = _options.ClientId,
                ["clientSecret"] = _options.ClientSecret
            })
        };

        var httpClient = _httpClientFactory.CreateClient(IFoodHttpClients.Authentication);
        using var response = await httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorCode = default(string);
            var errorMessage = $"Falha ao autenticar na API do iFood (HTTP {(int)response.StatusCode}).";

            var errorBody = await response.Content.ReadFromJsonAsync<IFoodErrorResponse>(cancellationToken: ct);
            if (errorBody is not null)
            {
                errorCode = errorBody.Error.Code;
                errorMessage = errorBody.Error.Message;
            }

            throw new IFoodAuthenticationException(errorMessage, errorCode);
        }

        var token = await response.Content.ReadFromJsonAsync<IFoodTokenResponse>(cancellationToken: ct)
            ?? throw new IFoodAuthenticationException("Resposta de autenticação do iFood veio vazia.");

        // O esquema vem da resposta ("bearer"), não hardcoded.
        _cachedHeader = new AuthenticationHeaderValue(token.Type, token.AccessToken);
        _renewAt = _timeProvider.GetUtcNow() + TimeSpan.FromSeconds(token.ExpiresIn) * RenewAtLifetimeFraction;

        return _cachedHeader;
    }
}
