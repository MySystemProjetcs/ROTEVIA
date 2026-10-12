using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood.Auth;

public interface IDiDiFoodAuthenticator
{
    // Token da 99Food é por app_shop_id, não global do app como no iFood
    // Centralizado — a doc do /v1/auth/authtoken/get diz "returns the auth
    // token for the shop specified with app_shop_id".
    Task<string> GetAuthTokenAsync(string appShopId, CancellationToken ct);

    void InvalidateCache(string appShopId);
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

    public async Task<string> GetAuthTokenAsync(string appShopId, CancellationToken ct)
    {
        if (_cache.TryGetValue(appShopId, out var existente) && _timeProvider.GetUtcNow() < existente.RenovarEm)
            return existente.Token;

        var trava = _locks.GetOrAdd(appShopId, _ => new SemaphoreSlim(1, 1));
        await trava.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(appShopId, out existente) && _timeProvider.GetUtcNow() < existente.RenovarEm)
                return existente.Token;

            return await AutenticarAsync(appShopId, ct);
        }
        finally
        {
            trava.Release();
        }
    }

    public void InvalidateCache(string appShopId) => _cache.TryRemove(appShopId, out _);

    private async Task<string> AutenticarAsync(string appShopId, CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient(nameof(DiDiFoodAuthenticator));

        // /v1/auth/authtoken/get só RETORNA um token já gerado — nunca gera.
        // Para loja recém-vinculada, data vem sem auth_token e precisamos
        // disparar /refresh (que cria um novo) antes de tentar o /get de novo
        // ("If you don't have a auth token yet or the auth token expired,
        //  please call auth/authtoken/refresh api first" — doc oficial).
        var payload = await ChamarGetAsync(http, appShopId, ct);
        if (string.IsNullOrWhiteSpace(payload?.AuthToken))
        {
            await ChamarRefreshAsync(http, appShopId, ct);
            payload = await ChamarGetAsync(http, appShopId, ct);
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.AuthToken))
            throw new InvalidOperationException(
                "99Food não retornou auth_token para a loja informada mesmo após /refresh.");

        var renovarEm = CalcularRenovacao(payload.TokenExpirationTime);
        _cache[appShopId] = (payload.AuthToken, renovarEm);
        return payload.AuthToken;
    }

    private DateTimeOffset CalcularRenovacao(long tokenExpirationTime)
    {
        var agora = _timeProvider.GetUtcNow();
        if (tokenExpirationTime <= 0)
            return agora + DuracaoAssumida - MargemDeRenovacao;

        var expiracao = DateTimeOffset.FromUnixTimeSeconds(tokenExpirationTime);
        // Guarda contra timestamp já no passado (relógio dessincronizado ou
        // response fora de ordem): usa o fallback em vez de token vencido.
        if (expiracao <= agora)
            return agora + DuracaoAssumida - MargemDeRenovacao;

        return expiracao - MargemDeRenovacao;
    }

    private async Task<DiDiAuthTokenData?> ChamarGetAsync(HttpClient http, string appShopId, CancellationToken ct)
    {
        var uri = "/v1/auth/authtoken/get"
            + $"?app_id={_options.AppId}"
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

        if (envelope.Errno != 0)
            throw new InvalidOperationException(
                $"99Food recusou {op}: errno={envelope.Errno} errmsg={envelope.Errmsg ?? "(sem mensagem)"}.");

        return envelope.Data;
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
        [property: JsonPropertyName("token_expiration_time")] long TokenExpirationTime);
}
