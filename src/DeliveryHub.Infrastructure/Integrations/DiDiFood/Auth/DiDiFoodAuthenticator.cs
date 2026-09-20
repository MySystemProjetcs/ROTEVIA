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
    Task<string> GetAuthTokenAsync(string appShopId, CancellationToken ct);

    void InvalidateCache(string appShopId);
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

        var uri = "/v1/auth/authtoken/get"
            + $"?app_id={_options.AppId}"
            + $"&app_secret={Uri.EscapeDataString(_options.AppSecret)}"
            + $"&app_shop_id={Uri.EscapeDataString(appShopId)}";

        using var response = await http.GetAsync(uri, ct);
        response.EnsureSuccessStatusCode();

        var corpo = await response.Content.ReadFromJsonAsync<DiDiAuthTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Resposta vazia ao obter auth_token da 99Food.");

        if (string.IsNullOrWhiteSpace(corpo.AuthToken))
            throw new InvalidOperationException("99Food não retornou auth_token para a loja informada.");

        var duracao = corpo.ExpiresIn > 0 ? TimeSpan.FromSeconds(corpo.ExpiresIn) : DuracaoAssumida;
        var renovarEm = _timeProvider.GetUtcNow() + duracao - MargemDeRenovacao;

        _cache[appShopId] = (corpo.AuthToken, renovarEm);
        return corpo.AuthToken;
    }

    // internal (não Contracts/) de propósito: é resposta de autenticação, não
    // dado de pedido — o teste de arquitetura que isola DiDiFood.Contracts não
    // precisa cobrir isto, mas nada aqui deve vazar para Domain/Application
    // do mesmo jeito.
    internal sealed record DiDiAuthTokenResponse(
        [property: JsonPropertyName("auth_token")] string? AuthToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
