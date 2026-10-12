using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood.Auth;

public interface IDiDiFoodAuthenticator
{
    // O guia da 99Food só documenta os caminhos dos dois endpoints (GET
    // /v1/auth/authtoken/get e /refresh) e diz que o token "retorna para a
    // loja solicitada" — ou seja, por app_shop_id, não um token único do app
    // inteiro como no iFood Centralizado. Os nomes exatos dos campos do
    // corpo/resposta abaixo (app_id/app_secret/app_shop_id na ida,
    // auth_token/expires_in na volta) são inferidos do vocabulário já
    // estabelecido pelo próprio guia (seção "Identificando as Credenciais") —
    // precisam ser confirmados contra uma chamada real de sandbox antes da
    // validação viva (CLAUDE.md §2 passo 4).
    // appId nulo cai no AppId global de configuração (DiDiFood:AppId); quando a
    // loja traz o próprio app_id_99, ele tem precedência.
    Task<string> GetAuthTokenAsync(string appShopId, string? appId, CancellationToken ct);

    void InvalidateCache(string appShopId, string? appId);
}

internal sealed class DiDiFoodAuthenticator : IDiDiFoodAuthenticator
{
    // Fallback para quando token_expiration_time não vem ou vem zerado.
    // Conservador: menor que a validade real documentada, nunca usa expirado.
    private static readonly TimeSpan DuracaoAssumida = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MargemDeRenovacao = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DiDiFoodOptions _options;
    private readonly TimeProvider _timeProvider;

    // Um cache por loja — o token não é global do app. SemaphoreSlim por chave
    // evita que duas requisições concorrentes para a mesma loja autentiquem em
    // dobro, sem travar lojas diferentes entre si.
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset RenovarEm)> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public DiDiFoodAuthenticator(
        IHttpClientFactory httpClientFactory, IOptions<DiDiFoodOptions> options, TimeProvider timeProvider)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<string> GetAuthTokenAsync(string appShopId, string? appId, CancellationToken ct)
    {
        var chave = ChaveDoCache(appShopId, appId);

        if (_cache.TryGetValue(chave, out var existente) && _timeProvider.GetUtcNow() < existente.RenovarEm)
            return existente.Token;

        var trava = _locks.GetOrAdd(chave, _ => new SemaphoreSlim(1, 1));
        await trava.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(chave, out existente) && _timeProvider.GetUtcNow() < existente.RenovarEm)
                return existente.Token;

            return await AutenticarAsync(appShopId, appId, chave, ct);
        }
        finally
        {
            trava.Release();
        }
    }

    public void InvalidateCache(string appShopId, string? appId) =>
        _cache.TryRemove(ChaveDoCache(appShopId, appId), out _);

    // app_id entra na chave: duas lojas com app_id diferente não podem
    // compartilhar entrada de cache, ainda que o app_shop_id coincidisse.
    private string ChaveDoCache(string appShopId, string? appId) =>
        $"{(string.IsNullOrWhiteSpace(appId) ? _options.AppId.ToString() : appId)}:{appShopId}";

    private async Task<string> AutenticarAsync(string appShopId, string? appId, string chave, CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient(nameof(DiDiFoodAuthenticator));

        var idDoApp = string.IsNullOrWhiteSpace(appId) ? _options.AppId.ToString() : appId;

        var uri = "/v1/auth/authtoken/get"
            + $"?app_id={Uri.EscapeDataString(idDoApp)}"
            + $"&app_secret={Uri.EscapeDataString(_options.AppSecret)}"
            + $"&app_shop_id={Uri.EscapeDataString(appShopId)}";

        return await ExecutarAsync<DiDiAuthTokenData>(http, HttpMethod.Get, uri, "authtoken/get", ct);
    }

    private async Task ChamarRefreshAsync(HttpClient http, string appShopId, CancellationToken ct)
    {
        var uri = "/v1/auth/authtoken/refresh"
            + $"?app_id={_options.AppId}"
            + $"&app_secret={Uri.EscapeDataString(_options.AppSecret)}"
            + $"&app_shop_id={Uri.EscapeDataString(appShopId)}";

        // /refresh devolve data = true/false segundo a doc; o importante é que
        // errno == 0. O token em si vem na próxima chamada ao /get.
        await ExecutarAsync<bool?>(http, HttpMethod.Get, uri, "authtoken/refresh", ct);
    }

    private static async Task<T?> ExecutarAsync<T>(
        HttpClient http, HttpMethod method, string uri, string op, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, uri);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<DiDiStandardResponse<T>>(cancellationToken: ct)
            ?? throw new InvalidOperationException($"Resposta vazia da 99Food em {op}.");

        // Usa o token_expiration_time retornado pela API (em segundos desde o epoch).
        // Se o campo for 0 ou negativo, cai na duração assumida como fallback.
        long duracaoSegundos = corpo.TokenExpirationTimeUnix > 0
            ? corpo.TokenExpirationTimeUnix
            : corpo.ExpiresIn;

        var duracao = duracaoSegundos > 0
            ? TimeSpan.FromSeconds(duracaoSegundos)
            : DuracaoAssumida;

        var renovarEm = _timeProvider.GetUtcNow() + duracao - MargemDeRenovacao;

        _cache[chave] = (corpo.AuthToken, renovarEm);
        return corpo.AuthToken;
    }

    // Envelope padrão da 99Food em todas as respostas da OpenAPI
    // (StandardResponse no swagger: errno + errmsg + data).
    internal sealed record DiDiStandardResponse<T>(
        [property: JsonPropertyName("errno")] int Errno,
        [property: JsonPropertyName("errmsg")] string? Errmsg,
        [property: JsonPropertyName("data")] T? Data);

    // Corpo do data em /v1/auth/authtoken/get. Expiração é unix timestamp
    // ABSOLUTO em segundos — não é duração relativa como no iFood.
    internal sealed record DiDiAuthTokenData(
        [property: JsonPropertyName("app_id")] long AppId,
        [property: JsonPropertyName("app_shop_id")] string? AppShopId,
        [property: JsonPropertyName("auth_token")] string? AuthToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_expiration_time")] long TokenExpirationTimeUnix);
}
