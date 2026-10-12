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
    // Sem confirmação do prazo real de expiração — 10 minutos é conservador o
    // bastante para nunca usar um token vencido, e barato o bastante (uma
    // chamada extra a cada 10 min por loja ativa) para não pesar na API deles
    // enquanto o valor real não é confirmado.
    private static readonly TimeSpan DuracaoAssumida = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MargemDeRenovacao = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DiDiFoodOptions _options;
    private readonly TimeProvider _timeProvider;

    // Um cache por loja — o token não é global do app como no iFood
    // Centralizado, é por app_shop_id. SemaphoreSlim por chave evita que duas
    // requisições concorrentes para a mesma loja autentiquem em dobro, sem
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

        using var response = await http.GetAsync(uri, ct);
        response.EnsureSuccessStatusCode();

        var corpo = await response.Content.ReadFromJsonAsync<DiDiAuthTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Resposta vazia ao obter auth_token da 99Food.");

        if (string.IsNullOrWhiteSpace(corpo.AuthToken))
            throw new InvalidOperationException("99Food não retornou auth_token para a loja informada.");

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

    // internal (não Contracts/) de propósito: é resposta de autenticação, não
    // dado de pedido — o teste de arquitetura que isola DiDiFood.Contracts não
    // precisa cobrir isto, mas nada aqui deve vazar para Domain/Application
    // do mesmo jeito.
    internal sealed record DiDiAuthTokenResponse(
        [property: JsonPropertyName("auth_token")] string? AuthToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_expiration_time")] long TokenExpirationTimeUnix);
}
