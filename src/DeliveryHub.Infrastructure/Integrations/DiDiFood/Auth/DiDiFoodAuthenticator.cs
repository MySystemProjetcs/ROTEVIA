using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Contracts;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood.Auth;

public interface IDiDiFoodAuthenticator
{
    // Token da 99Food é por app_shop_id, não global do app como no iFood
    // Centralizado — a doc do /v1/auth/authtoken/get diz "returns the auth
    // token for the shop specified with app_shop_id".
    //
    // appId nulo cai no AppId global de configuração (DiDiFood:AppId); quando a
    // loja traz o próprio app_id_99 (coluna merchants.app_id_99), ele tem
    // precedência — permite rodar várias lojas atendendo apps distintos sem
    // trocar as credenciais globais.
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

    // Um cache por (effectiveAppId, appShopId): duas lojas com app_id distinto
    // não podem compartilhar entrada ainda que o app_shop_id coincida.
    // SemaphoreSlim por chave evita autenticar em dobro sob corrida, sem
    // travar lojas diferentes entre si.
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
        var effectiveAppId = ResolverAppId(appId);
        var chave = ChaveDoCache(effectiveAppId, appShopId);

        if (_cache.TryGetValue(chave, out var existente) && _timeProvider.GetUtcNow() < existente.RenovarEm)
            return existente.Token;

        var trava = _locks.GetOrAdd(chave, _ => new SemaphoreSlim(1, 1));
        await trava.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(chave, out existente) && _timeProvider.GetUtcNow() < existente.RenovarEm)
                return existente.Token;

            return await AutenticarAsync(effectiveAppId, appShopId, chave, ct);
        }
        finally
        {
            trava.Release();
        }
    }

    public void InvalidateCache(string appShopId, string? appId) =>
        _cache.TryRemove(ChaveDoCache(ResolverAppId(appId), appShopId), out _);

    private string ResolverAppId(string? appId) =>
        string.IsNullOrWhiteSpace(appId) ? _options.AppId.ToString() : appId.Trim();

    private static string ChaveDoCache(string effectiveAppId, string appShopId) =>
        $"{effectiveAppId}:{appShopId}";

    private async Task<string> AutenticarAsync(
        string effectiveAppId, string appShopId, string chave, CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient(nameof(DiDiFoodAuthenticator));

        // /v1/auth/authtoken/get só RETORNA um token já gerado — nunca gera.
        // Para loja recém-vinculada, data vem sem auth_token e precisamos
        // disparar /refresh (que cria um novo) antes de tentar o /get de novo
        // ("If you don't have a auth token yet or the auth token expired,
        //  please call auth/authtoken/refresh api first" — doc oficial).
        var payload = await ChamarGetAsync(http, effectiveAppId, appShopId, ct);
        if (string.IsNullOrWhiteSpace(payload?.AuthToken))
        {
            await ChamarRefreshAsync(http, effectiveAppId, appShopId, ct);
            payload = await ChamarGetAsync(http, effectiveAppId, appShopId, ct);
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.AuthToken))
            throw new InvalidOperationException(
                $"99Food não retornou auth_token para a loja {appShopId} (appId={effectiveAppId}) mesmo após /refresh.");

        var renovarEm = CalcularRenovacao(payload.TokenExpirationTime);
        _cache[chave] = (payload.AuthToken, renovarEm);
        return payload.AuthToken;
    }

    private DateTimeOffset CalcularRenovacao(long tokenExpirationTime)
    {
        var agora = _timeProvider.GetUtcNow();
        if (tokenExpirationTime <= 0)
            return agora + DuracaoAssumida - MargemDeRenovacao;

        // token_expiration_time é unix timestamp ABSOLUTO em segundos
        // (não duração). Confirmado pelo retorno real da 99 e pelo swagger
        // oficial. Guard contra timestamp já no passado (relógio
        // dessincronizado ou response fora de ordem).
        var expiracao = DateTimeOffset.FromUnixTimeSeconds(tokenExpirationTime);
        if (expiracao <= agora)
            return agora + DuracaoAssumida - MargemDeRenovacao;

        return expiracao - MargemDeRenovacao;
    }

    private async Task<DiDiAuthTokenData?> ChamarGetAsync(
        HttpClient http, string effectiveAppId, string appShopId, CancellationToken ct)
    {
        var uri = "/v1/auth/authtoken/get"
            + $"?app_id={Uri.EscapeDataString(effectiveAppId)}"
            + $"&app_secret={Uri.EscapeDataString(_options.AppSecret)}"
            + $"&app_shop_id={Uri.EscapeDataString(appShopId)}";

        return await ExecutarAsync<DiDiAuthTokenData>(http, HttpMethod.Get, uri, "authtoken/get", ct);
    }

    private async Task ChamarRefreshAsync(
        HttpClient http, string effectiveAppId, string appShopId, CancellationToken ct)
    {
        var uri = "/v1/auth/authtoken/refresh"
            + $"?app_id={Uri.EscapeDataString(effectiveAppId)}"
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

    // Corpo do data em /v1/auth/authtoken/get. token_expiration_time é unix
    // timestamp ABSOLUTO em segundos (ex. 1791779425 = 2026-10-12 11:50 UTC) —
    // não é duração relativa como no iFood (expires_in).
    internal sealed record DiDiAuthTokenData(
        [property: JsonPropertyName("app_id")] long AppId,
        [property: JsonPropertyName("app_shop_id")] string? AppShopId,
        [property: JsonPropertyName("auth_token")] string? AuthToken,
        [property: JsonPropertyName("token_expiration_time")] long TokenExpirationTime);
}
